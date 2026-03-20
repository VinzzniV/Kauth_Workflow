using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository
{
    // Diese Regeln definieren den erlaubten Lebenszyklus einzelner Aufgaben.
    private static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done",
        "skipped",
        "cancelled"
    };

    private static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done",
        "skipped",
        "cancelled"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTaskTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "in_progress", "blocked", "done", "skipped", "cancelled" },
        ["ready"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "blocked", "done", "skipped", "cancelled" },
        ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done", "blocked", "skipped", "cancelled" },
        ["blocked"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "cancelled" },
        ["done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ["skipped"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ["cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    private const string SupervisorRequirementTaskKey = "supervisor_fills_document";
    private const string LegacySupervisorHandoverTaskKey = "onboarding_doc_sent_to_supervisor";

    private enum TaskGenerationStage
    {
        Initial,
        AfterSupervisor
    }

    // Die Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
    }

    // Stammdaten fuer die HR-Erfassung.
    public async Task<List<DepartmentDto>> GetDepartments()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT id, name
FROM departments
ORDER BY name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var departments = new List<DepartmentDto>();
        while (await reader.ReadAsync())
        {
            departments.Add(new DepartmentDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return departments;
    }

    public async Task<List<RoleDto>> GetRoles()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT r.id, r.department_id, d.name, r.name, r.is_active
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
  AND r.is_active = TRUE
ORDER BY d.name, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var roles = new List<RoleDto>();
        while (await reader.ReadAsync())
        {
            roles.Add(new RoleDto
            {
                Id = reader.GetInt32(0),
                DepartmentId = reader.GetInt32(1),
                DepartmentName = reader.GetString(2),
                Name = reader.GetString(3),
                IsActive = reader.GetBoolean(4)
            });
        }

        return roles;
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(int? roleId = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await LoadRequirements(connection, null);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        if (roleId.HasValue && !await RoleExists(connection, null, roleId.Value))
        {
            return null;
        }

        var requirements = await LoadRequirements(connection, null);
        var roleRecommendations = roleId.HasValue
            ? await LoadRoleRecommendations(connection, null, roleId.Value)
            : await LoadAllRoleRecommendations(connection, null);

        return new WorkflowConfigDto
        {
            Requirements = requirements,
            RoleRecommendations = roleRecommendations
        };
    }

    private static async Task<RoleRecommendationsDto> LoadAllRoleRecommendations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = @"
SELECT id
FROM app_roles
WHERE role_kind = 'position'
  AND is_active = TRUE
ORDER BY id;";

        var roleIds = new List<int>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roleIds.Add(reader.GetInt32(0));
            }
        }

        var recommendedRequirementIds = new SortedSet<int>();
        var defaultValues = new List<RoleRecommendationDefaultValueDto>();
        var defaultValueKeys = new HashSet<(int RequirementId, bool? ValueBoolean, string? ValueText, decimal? ValueNumber)>();
        var defaultSelectedOptions = new List<RoleRecommendationSelectedOptionsDto>();
        var defaultSelectedOptionKeys = new HashSet<(int RequirementId, int? SelectedOptionId, string SelectedOptionIdsKey)>();

        foreach (var currentRoleId in roleIds)
        {
            var roleRecommendations = await LoadRoleRecommendations(connection, transaction, currentRoleId);

            foreach (var requirementId in roleRecommendations.RecommendedRequirementIds)
            {
                recommendedRequirementIds.Add(requirementId);
            }

            foreach (var defaultValue in roleRecommendations.DefaultValues)
            {
                var key = (
                    defaultValue.RequirementId,
                    defaultValue.ValueBoolean,
                    defaultValue.ValueText,
                    defaultValue.ValueNumber);

                if (!defaultValueKeys.Add(key))
                {
                    continue;
                }

                defaultValues.Add(defaultValue);
            }

            foreach (var selectedOptions in roleRecommendations.DefaultSelectedOptions)
            {
                var key = (
                    selectedOptions.RequirementId,
                    selectedOptions.SelectedOptionId,
                    string.Join(",", selectedOptions.SelectedOptionIds.OrderBy(id => id)));

                if (!defaultSelectedOptionKeys.Add(key))
                {
                    continue;
                }

                defaultSelectedOptions.Add(selectedOptions);
            }
        }

        return new RoleRecommendationsDto
        {
            RecommendedRequirementIds = recommendedRequirementIds.ToList(),
            DefaultValues = defaultValues
                .OrderBy(item => item.RequirementId)
                .ToList(),
            DefaultSelectedOptions = defaultSelectedOptions
                .OrderBy(item => item.RequirementId)
                .ThenBy(item => item.SelectedOptionId)
                .ToList()
        };
    }

    // Erstellt den Workflow, initialisiert Benachrichtigungen und liefert anschliessend die neue UID zurueck.
    public async Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await EnsureValidPositionRole(connection, transaction, request.RoleId, request.DepartmentId);

        const string workflowInsertSql = @"
INSERT INTO workflows (
    department_id,
    onboarding_role_id,
    created_by_user_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    status,
    started_at
)
VALUES (
    @departmentId,
    @roleId,
    @createdByUserId,
    @firstName,
    @lastName,
    @employeeNumber,
    @badgeNumber,
    'draft',
    NULL
)
RETURNING id, uid;";

        long workflowId;
        Guid workflowUid;

        await using (var workflowInsertCommand = new NpgsqlCommand(workflowInsertSql, connection, transaction))
        {
            workflowInsertCommand.Parameters.AddWithValue("departmentId", request.DepartmentId);
            workflowInsertCommand.Parameters.AddWithValue("roleId", request.RoleId);
            workflowInsertCommand.Parameters.AddWithValue("createdByUserId", createdByUserId);
            workflowInsertCommand.Parameters.AddWithValue("firstName", request.FirstName);
            workflowInsertCommand.Parameters.AddWithValue("lastName", request.LastName);
            workflowInsertCommand.Parameters.AddWithValue("employeeNumber", request.EmployeeNumber);
            workflowInsertCommand.Parameters.AddWithValue("badgeNumber", request.BadgeNumber);

            await using var reader = await workflowInsertCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Workflow could not be created.");
            }

            workflowId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
        }

        await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            request.DepartmentId,
            new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase),
            TaskGenerationStage.Initial);

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId);

        var notificationTargets = await CreateWorkflowNotifications(
            connection,
            transaction,
            workflowId,
            request.DepartmentId);

        await transaction.CommitAsync();

        return new WorkflowCreationResult
        {
            WorkflowId = workflowId,
            Uid = workflowUid,
            NotificationTargets = notificationTargets
        };
    }

    // Der Schritt der Abteilungsleitung ersetzt vorhandene Antworten und erzeugt daraus den weiteren Aufgabenplan.
    public async Task<WorkflowDetailDto?> CompleteSupervisorStep(
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    id,
    onboarding_role_id,
    department_id,
    status
FROM workflows
WHERE uid = @workflowUid
LIMIT 1
FOR UPDATE;";

        long workflowId;
        int workflowRoleId;
        int workflowDepartmentId;
        string workflowStatus;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            workflowId = reader.GetInt64(0);
            workflowRoleId = reader.GetInt32(1);
            workflowDepartmentId = reader.GetInt32(2);
            workflowStatus = reader.GetString(3);
        }

        if (!string.Equals(workflowStatus, "waiting_for_supervisor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Der Schritt der Abteilungsleitung kann nur abgeschlossen werden, solange der Fall auf die Abteilungsleitung wartet.");
        }

        const string clearAnswersSql = @"
DELETE FROM workflow_answers
WHERE workflow_id = @workflowId;";

        await using (var clearAnswersCommand = new NpgsqlCommand(clearAnswersSql, connection, transaction))
        {
            clearAnswersCommand.Parameters.AddWithValue("workflowId", workflowId);
            await clearAnswersCommand.ExecuteNonQueryAsync();
        }

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, workflowRoleId);

        var storedAnswers = await PersistWorkflowAnswers(
            connection,
            transaction,
            workflowId,
            selections,
            answerDefinitions,
            roleDefaults);

        var answersByKey = storedAnswers
            .GroupBy(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        ValidateSupervisorSelections(answerDefinitions, answersByKey);

        if (!await WorkflowHasAnyTasks(connection, transaction, workflowId))
        {
            await BackfillLegacyInitialTasksForSupervisorCompletion(
                connection,
                transaction,
                workflowId,
                workflowDepartmentId);
        }
        else
        {
            await CompleteWorkflowTaskByKey(
                connection,
                transaction,
                workflowId,
                SupervisorRequirementTaskKey);
        }

        await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            answersByKey,
            TaskGenerationStage.AfterSupervisor);

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId);

        await transaction.CommitAsync();
        return await GetWorkflowByUid(workflowUid);
    }

    private static async Task<bool> RoleExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int roleId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE id = @roleId
      AND role_kind = 'position'
      AND is_active = TRUE
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task EnsureValidPositionRole(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId,
        int departmentId)
    {
        const string sql = @"
SELECT r.id
FROM app_roles r
WHERE r.id = @roleId
  AND r.department_id = @departmentId
  AND r.role_kind = 'position'
  AND r.is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Role is invalid for the selected department.");
        }
    }
}
