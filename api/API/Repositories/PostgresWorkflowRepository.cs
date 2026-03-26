using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository
{
    private sealed class ProcessTypeCreateRecord
    {
        public required int Id { get; init; }
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public string? ApprovalTaskTemplateKey { get; init; }
        public required bool RequiresTargetPerson { get; init; }
        public required bool IsActive { get; init; }
    }

    private sealed class TargetPersonRecord
    {
        public required long PersonId { get; init; }
        public required string DisplayName { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public int? RoleId { get; init; }
        public string? RoleName { get; init; }
        public int? EmployeeNumber { get; init; }
        public int? BadgeNumber { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
    }

    // Diese Regeln definieren den erlaubten Lebenszyklus einzelner Aufgaben.
    private static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done"
    };

    private static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTaskTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "in_progress", "blocked", "done" },
        ["ready"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "blocked", "done" },
        ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done", "blocked" },
        ["blocked"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready" },
        ["done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };

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

    public async Task<List<WorkflowProcessTypeDto>> GetActiveProcessTypes()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT key, name, requires_target_person
FROM process_types
WHERE is_active = TRUE
ORDER BY sort_order, name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var processTypes = new List<WorkflowProcessTypeDto>();
        while (await reader.ReadAsync())
        {
            processTypes.Add(new WorkflowProcessTypeDto
            {
                Key = reader.GetString(0),
                Name = reader.GetString(1),
                RequiresTargetPerson = reader.GetBoolean(2)
            });
        }

        return processTypes;
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(string? query, int limit = 20)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    p.id,
    u.display_name,
    COALESCE(latest.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    latest.position_role_id,
    r.name AS role_name,
    latest.employee_number,
    latest.badge_number,
    latest.first_name,
    latest.last_name
FROM people p
JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN LATERAL (
    SELECT
        w.department_id,
        w.position_role_id,
        w.employee_number,
        w.badge_number,
        w.first_name,
        w.last_name,
        w.created_at
    FROM workflows w
    WHERE w.target_person_id = p.id
       OR (w.employee_number > 0 AND TRIM(w.first_name || ' ' || w.last_name) = u.display_name)
    ORDER BY w.created_at DESC
    LIMIT 1
) latest ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(latest.department_id, p.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = latest.position_role_id
WHERE u.is_active = TRUE
  AND (
      @query = ''
      OR u.display_name ILIKE @pattern
      OR COALESCE(d.name, '') ILIKE @pattern
      OR COALESCE(r.name, '') ILIKE @pattern
      OR CAST(COALESCE(latest.employee_number, 0) AS TEXT) ILIKE @pattern
  )
ORDER BY u.display_name, p.id
LIMIT @limit;";

        await using var command = new NpgsqlCommand(sql, connection);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("query", normalizedQuery);
        command.Parameters.AddWithValue("pattern", $"%{normalizedQuery}%");
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 50));
        await using var reader = await command.ExecuteReaderAsync();

        var people = new List<WorkflowTargetPersonDto>();
        while (await reader.ReadAsync())
        {
            people.Add(new WorkflowTargetPersonDto
            {
                PersonId = reader.GetInt64(0),
                DisplayName = reader.GetString(1),
                DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
                RoleId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                RoleName = reader.IsDBNull(5) ? null : reader.GetString(5),
                EmployeeNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                BadgeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                FirstName = reader.IsDBNull(8) ? null : reader.GetString(8),
                LastName = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        return people;
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(string? processTypeKey = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, processTypeKey);
        return await LoadRequirements(connection, null, processTypeId);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string? processTypeKey = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, processTypeKey);
        if (roleId.HasValue && !await RoleExists(connection, null, roleId.Value))
        {
            return null;
        }

        var requirements = await LoadRequirements(connection, null, processTypeId);
        var roleRecommendations = roleId.HasValue
            ? await LoadRoleRecommendations(connection, null, roleId.Value, processTypeId)
            : await LoadAllRoleRecommendations(connection, null, processTypeId);

        return new WorkflowConfigDto
        {
            Requirements = requirements,
            RoleRecommendations = roleRecommendations
        };
    }

    private static async Task<RoleRecommendationsDto> LoadAllRoleRecommendations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int processTypeId)
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
            var roleRecommendations = await LoadRoleRecommendations(connection, transaction, currentRoleId, processTypeId);

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

    private static async Task<int> ResolveProcessTypeId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? processTypeKey)
    {
        const string sql = @"
SELECT id
FROM process_types
WHERE key = @processTypeKey
  AND is_active = TRUE
LIMIT 1;";

        var normalizedProcessTypeKey = string.IsNullOrWhiteSpace(processTypeKey)
            ? "onboarding"
            : processTypeKey.Trim().ToLowerInvariant();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", normalizedProcessTypeKey);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is int processTypeId)
        {
            return processTypeId;
        }

        throw new InvalidOperationException($"Unbekannter oder inaktiver Prozesstyp '{normalizedProcessTypeKey}'.");
    }

    // Erstellt den Workflow, initialisiert Benachrichtigungen und liefert anschliessend die neue UID zurueck.
    public async Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var processType = await LoadProcessTypeForCreate(connection, transaction, request.ProcessTypeKey);
        var requiresNewPersonFields = !processType.RequiresTargetPerson;

        if (processType.RequiresTargetPerson)
        {
            if (!request.TargetPersonId.HasValue)
            {
                throw new InvalidOperationException($"Der Prozesstyp '{processType.Name}' erfordert eine Zielperson.");
            }
        }
        else if (request.TargetPersonId.HasValue)
        {
            throw new InvalidOperationException($"Der Prozesstyp '{processType.Name}' darf keine bestehende Zielperson referenzieren.");
        }

        var targetPerson = request.TargetPersonId.HasValue
            ? await LoadTargetPerson(connection, transaction, request.TargetPersonId.Value)
            : null;

        var effectiveDepartmentId = request.DepartmentId;
        int? effectiveRoleId = request.RoleId;

        if (request.RoleId.HasValue)
        {
            if (!request.DepartmentId.HasValue)
            {
                throw new InvalidOperationException("Die Abteilung ist erforderlich, wenn eine Zielrolle direkt angegeben wird.");
            }

            await EnsureValidPositionRole(connection, transaction, request.RoleId.Value, request.DepartmentId.Value);
        }
        else if (targetPerson is not null)
        {
            if (!targetPerson.DepartmentId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Fuer die Zielperson '{targetPerson.DisplayName}' ist keine aktuelle Abteilung ableitbar.");
            }

            if (!targetPerson.RoleId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Fuer die Zielperson '{targetPerson.DisplayName}' ist keine aktuelle Stelle ableitbar.");
            }

            effectiveDepartmentId = targetPerson.DepartmentId.Value;
            effectiveRoleId = targetPerson.RoleId.Value;
        }

        if (!effectiveDepartmentId.HasValue)
        {
            throw new InvalidOperationException("Die Abteilung ist fuer diesen Prozesstyp erforderlich.");
        }

        if (!effectiveRoleId.HasValue)
        {
            throw new InvalidOperationException("Die Zielrolle ist fuer diesen Prozesstyp erforderlich.");
        }

        var roleId = effectiveRoleId.Value;
        var departmentId = effectiveDepartmentId.Value;

        if (request.RoleId.HasValue)
        {
            await EnsureValidPositionRole(connection, transaction, roleId, departmentId);
        }

        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        if (!requiresNewPersonFields && targetPerson is not null)
        {
            var derivedFirstName = targetPerson.FirstName;
            var derivedLastName = targetPerson.LastName;
            if (string.IsNullOrWhiteSpace(derivedFirstName) || string.IsNullOrWhiteSpace(derivedLastName))
            {
                (derivedFirstName, derivedLastName) = SplitDisplayName(targetPerson.DisplayName);
            }

            firstName ??= derivedFirstName;
            lastName ??= derivedLastName;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidOperationException("Der Vorname ist fuer diesen Prozesstyp erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("Der Nachname ist fuer diesen Prozesstyp erforderlich.");
        }

        var employeeNumber = request.EmployeeNumber ?? targetPerson?.EmployeeNumber;
        if (!employeeNumber.HasValue)
        {
            throw new InvalidOperationException(
                "Die Personalnummer ist erforderlich. Fuer diesen Prozesstyp konnte sie nicht aus der Zielperson oder einem vorhandenen Vorgang abgeleitet werden.");
        }

        var badgeNumber = request.BadgeNumber ?? targetPerson?.BadgeNumber;
        if (!badgeNumber.HasValue)
        {
            throw new InvalidOperationException(
                "Die Ausweisnummer ist erforderlich. Fuer diesen Prozesstyp konnte sie nicht aus der Zielperson oder einem vorhandenen Vorgang abgeleitet werden.");
        }

        const string workflowInsertSql = @"
INSERT INTO workflows (
    process_type_id,
    department_id,
    position_role_id,
    created_by_user_id,
    target_person_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    deadline_date,
    status,
    started_at
)
VALUES (
    @processTypeId,
    @departmentId,
    @roleId,
    @createdByUserId,
    @targetPersonId,
    @firstName,
    @lastName,
    @employeeNumber,
    @badgeNumber,
    @deadlineDate,
    'draft',
    NULL
)
RETURNING id, uid;";

        long workflowId;
        Guid workflowUid;

        await using (var workflowInsertCommand = new NpgsqlCommand(workflowInsertSql, connection, transaction))
        {
            workflowInsertCommand.Parameters.AddWithValue("processTypeId", processType.Id);
            workflowInsertCommand.Parameters.AddWithValue("departmentId", departmentId);
            workflowInsertCommand.Parameters.AddWithValue("roleId", roleId);
            workflowInsertCommand.Parameters.AddWithValue("createdByUserId", createdByUserId);
            workflowInsertCommand.Parameters.Add("targetPersonId", NpgsqlDbType.Bigint).Value =
                (object?)request.TargetPersonId ?? DBNull.Value;
            workflowInsertCommand.Parameters.AddWithValue("firstName", firstName);
            workflowInsertCommand.Parameters.AddWithValue("lastName", lastName);
            workflowInsertCommand.Parameters.AddWithValue("employeeNumber", employeeNumber.Value);
            workflowInsertCommand.Parameters.AddWithValue("badgeNumber", badgeNumber.Value);
            workflowInsertCommand.Parameters.Add("deadlineDate", NpgsqlDbType.Date).Value =
                (object?)request.DeadlineDate ?? DBNull.Value;

            await using var reader = await workflowInsertCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Workflow could not be created.");
            }

            workflowId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
        }

        Dictionary<string, StoredWorkflowAnswerRecord> answersByKey;
        if (request.SourceWorkflowUid.HasValue)
        {
            var sourceWorkflow = await LoadWorkflowLinkLookup(connection, transaction, request.SourceWorkflowUid.Value);
            if (sourceWorkflow is null)
            {
                throw new InvalidOperationException("Der angegebene Quell-Workflow wurde nicht gefunden.");
            }

            var targetWorkflow = new WorkflowLinkLookupRecord
            {
                WorkflowId = workflowId,
                WorkflowUid = workflowUid,
                ProcessTypeId = processType.Id,
                ProcessTypeKey = processType.Key,
                RequiresSupervisorStep = processType.RequiresSupervisorStep,
                DepartmentId = departmentId,
                RoleId = roleId
            };

            var createdLinkId = await InsertWorkflowLink(
                connection,
                transaction,
                sourceWorkflow.WorkflowId,
                workflowId,
                "derived_from",
                createdByUserId,
                null);
            if (!createdLinkId.HasValue)
            {
                throw new InvalidOperationException("Die automatische Workflow-Verknüpfung konnte nicht erstellt werden.");
            }

            await InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                createdByUserId,
                "workflow_linked",
                null,
                "derived_from",
                $"Linked to workflow {request.SourceWorkflowUid.Value} (derived_from)");

            answersByKey = await ApplyDerivedAnswersToWorkflow(
                connection,
                transaction,
                sourceWorkflow,
                targetWorkflow,
                createdByUserId,
                regenerateTasks: false);
        }
        else
        {
            answersByKey = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var generatedTaskCount = await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            departmentId,
            answersByKey,
            TaskGenerationStage.Initial);

        if (generatedTaskCount > 0)
        {
            await InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                createdByUserId,
                "tasks_generated",
                null,
                null,
                $"{generatedTaskCount} Aufgabe(n) initial erstellt");
        }

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, createdByUserId);
        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            createdByUserId,
            "workflow_created",
            null,
            null,
            $"{firstName} {lastName}");

        var notificationTargets = await CreateWorkflowNotifications(
            connection,
            transaction,
            workflowId,
            departmentId,
            processType.RequiresSupervisorStep,
            processType.Key,
            processType.Name);

        await transaction.CommitAsync();

        return new WorkflowCreationResult
        {
            WorkflowId = workflowId,
            Uid = workflowUid,
            NotificationTargets = notificationTargets
        };
    }

    private static async Task<ProcessTypeCreateRecord> LoadProcessTypeForCreate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("Der Prozesstyp ist erforderlich.");
        }

        const string sql = @"
SELECT id, key, name, requires_supervisor_step, approval_task_template_key, requires_target_person, is_active
FROM process_types
WHERE key = @processTypeKey
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", processTypeKey.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException($"Unbekannter Prozesstyp '{processTypeKey}'.");
        }

        var record = new ProcessTypeCreateRecord
        {
            Id = reader.GetInt32(0),
            Key = reader.GetString(1),
            Name = reader.GetString(2),
            RequiresSupervisorStep = reader.GetBoolean(3),
            ApprovalTaskTemplateKey = NormalizeTaskTemplateKey(reader.IsDBNull(4) ? null : reader.GetString(4)),
            RequiresTargetPerson = reader.GetBoolean(5),
            IsActive = reader.GetBoolean(6)
        };

        if (!record.IsActive)
        {
            throw new InvalidOperationException($"Der Prozesstyp '{record.Name}' ist deaktiviert.");
        }

        EnsureApprovalTaskConfiguration(record.Name, record.RequiresSupervisorStep, record.ApprovalTaskTemplateKey);
        return record;
    }

    private static string? NormalizeTaskTemplateKey(string? taskTemplateKey)
    {
        return string.IsNullOrWhiteSpace(taskTemplateKey)
            ? null
            : taskTemplateKey.Trim();
    }

    private static string? EnsureApprovalTaskConfiguration(
        string processTypeName,
        bool requiresSupervisorStep,
        string? approvalTaskTemplateKey)
    {
        var normalizedApprovalTaskTemplateKey = NormalizeTaskTemplateKey(approvalTaskTemplateKey);
        if (requiresSupervisorStep && string.IsNullOrWhiteSpace(normalizedApprovalTaskTemplateKey))
        {
            throw new InvalidOperationException(
                $"Der Prozesstyp '{processTypeName}' verlangt einen Supervisor-Schritt, aber kein Approval-Task ist konfiguriert.");
        }

        return normalizedApprovalTaskTemplateKey;
    }

    private static async Task<TargetPersonRecord> LoadTargetPerson(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long targetPersonId)
    {
        const string sql = @"
SELECT
    p.id,
    u.display_name,
    COALESCE(latest.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    latest.position_role_id,
    r.name AS role_name,
    latest.employee_number,
    latest.badge_number,
    latest.first_name,
    latest.last_name
FROM people p
JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN LATERAL (
    SELECT
        w.department_id,
        w.position_role_id,
        w.employee_number,
        w.badge_number,
        w.first_name,
        w.last_name,
        w.created_at
    FROM workflows w
    WHERE w.target_person_id = p.id
       OR (w.employee_number > 0 AND TRIM(w.first_name || ' ' || w.last_name) = u.display_name)
    ORDER BY w.created_at DESC
    LIMIT 1
) latest ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(latest.department_id, p.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = latest.position_role_id
WHERE p.id = @targetPersonId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetPersonId", targetPersonId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Die angegebene Zielperson wurde nicht gefunden.");
        }

        return new TargetPersonRecord
        {
            PersonId = reader.GetInt64(0),
            DisplayName = reader.GetString(1),
            DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
            RoleId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            RoleName = reader.IsDBNull(5) ? null : reader.GetString(5),
            EmployeeNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            BadgeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            FirstName = reader.IsDBNull(8) ? null : reader.GetString(8),
            LastName = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    private static (string FirstName, string LastName) SplitDisplayName(string displayName)
    {
        var normalizedName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return (string.Empty, string.Empty);
        }

        var parts = normalizedName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], parts[0]),
            _ => (parts[0], parts[1])
        };
    }

    // Der Schritt der Abteilungsleitung ersetzt vorhandene Antworten und erzeugt daraus den weiteren Aufgabenplan.
    public async Task<WorkflowDetailDto?> CompleteSupervisorStep(
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.position_role_id,
    w.process_type_id,
    w.department_id,
    w.status,
    pt.name,
    pt.requires_supervisor_step,
    pt.approval_task_template_key
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
WHERE w.uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;";

        long workflowId;
        int workflowRoleId;
        int workflowProcessTypeId;
        int workflowDepartmentId;
        string workflowStatus;
        string workflowProcessTypeName;
        bool requiresSupervisorStep;
        string? approvalTaskTemplateKey;

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
            workflowProcessTypeId = reader.GetInt32(2);
            workflowDepartmentId = reader.GetInt32(3);
            workflowStatus = reader.GetString(4);
            workflowProcessTypeName = reader.GetString(5);
            requiresSupervisorStep = reader.GetBoolean(6);
            approvalTaskTemplateKey = EnsureApprovalTaskConfiguration(
                workflowProcessTypeName,
                requiresSupervisorStep,
                reader.IsDBNull(7) ? null : reader.GetString(7));
        }

        if (!string.Equals(workflowStatus, "waiting_for_supervisor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Der Schritt der Abteilungsleitung kann nur abgeschlossen werden, solange der Fall auf die Abteilungsleitung wartet.");
        }

        if (!requiresSupervisorStep || string.IsNullOrWhiteSpace(approvalTaskTemplateKey))
        {
            throw new InvalidOperationException("Fuer diesen Prozesstyp ist kein Supervisor-Schritt konfiguriert.");
        }

        const string clearAnswersSql = @"
DELETE FROM workflow_answers
WHERE workflow_id = @workflowId;";

        await using (var clearAnswersCommand = new NpgsqlCommand(clearAnswersSql, connection, transaction))
        {
            clearAnswersCommand.Parameters.AddWithValue("workflowId", workflowId);
            await clearAnswersCommand.ExecuteNonQueryAsync();
        }

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction, workflowProcessTypeId);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, workflowRoleId, workflowProcessTypeId);

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
                workflowDepartmentId,
                actorUserId);
        }
        else
        {
            await CompleteWorkflowTaskByKey(
                connection,
                transaction,
                workflowId,
                approvalTaskTemplateKey,
                actorUserId);
        }

        var generatedTaskCount = await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            answersByKey,
            TaskGenerationStage.AfterSupervisor);

        if (generatedTaskCount > 0)
        {
            await InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "tasks_generated",
                null,
                null,
                $"{generatedTaskCount} Aufgabe(n) nach Anforderungsauswahl erstellt");
        }

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);
        var nextWorkflowStatus = await LoadWorkflowStatusForUpdate(connection, transaction, workflowId);
        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "supervisor_step_completed",
            workflowStatus,
            nextWorkflowStatus ?? workflowStatus,
            $"Anforderungen gespeichert: {selections.Count}");

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
