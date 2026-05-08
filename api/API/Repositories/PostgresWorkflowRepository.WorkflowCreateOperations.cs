using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Erstellt den Workflow, initialisiert Benachrichtigungen und liefert anschliessend die neue UID zurueck.
    public async Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId)
    {
        var processTypeKey = request.WorkflowDefinitionKey;
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("processTypeKey ist erforderlich.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var processType = await LoadProcessTypeForCreate(connection, transaction, processTypeKey);
        var requiresNewPersonFields = !processType.RequiresTargetPerson;

        if (!request.TargetPersonId.HasValue)
        {
            throw new InvalidOperationException($"Der Prozesstyp '{processType.Name}' erfordert eine Zielperson.");
        }

        var targetPerson = await PostgresRepositorySharedHelpers.LoadTargetPerson(connection, transaction, request.TargetPersonId.Value);

        var effectiveDepartmentId = request.DepartmentId ?? targetPerson.DepartmentId;
        int? effectiveRoleId = request.RoleId ?? targetPerson.RoleId;

        if (request.RoleId.HasValue)
        {
            if (!effectiveDepartmentId.HasValue)
            {
                throw new InvalidOperationException("Die Abteilung ist erforderlich, wenn eine Zielrolle direkt angegeben wird.");
            }

            await PostgresRepositorySharedHelpers.EnsureValidPositionRole(connection, transaction, request.RoleId.Value, effectiveDepartmentId.Value);
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
            await PostgresRepositorySharedHelpers.EnsureValidPositionRole(connection, transaction, roleId, departmentId);
        }

        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        if (!requiresNewPersonFields)
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
    workflow_definition_id,
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
            workflowInsertCommand.Parameters.AddWithValue("targetPersonId", request.TargetPersonId.Value);
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
                LegacyProcessTypeKey = processType.Key,
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

            await _auditWrite.InsertAuditEntry(
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

        var generatedTaskCount = await _taskGeneration.GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            departmentId,
            answersByKey,
            TaskGenerationStage.Initial);

        if (generatedTaskCount > 0)
        {
            await _auditWrite.InsertAuditEntry(
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

        await _statusCalculation.RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await _statusCalculation.RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, createdByUserId);
        await _auditWrite.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            createdByUserId,
            "workflow_created",
            null,
            null,
            $"{firstName} {lastName}");

        var notificationTargets = await _notificationDispatch.CreateWorkflowNotifications(
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

    internal static async Task<ProcessTypeCreateRecord> LoadProcessTypeForCreate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("Der Prozesstyp ist erforderlich.");
        }

        const string sql = @"
SELECT id, definition_key, name, requires_supervisor_step, approval_spec_key, requires_target_person
FROM workflow_definitions
WHERE definition_key = @processTypeKey
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
            ApprovalSpecKey = reader.IsDBNull(4) ? null : reader.GetString(4).Trim(),
            RequiresTargetPerson = reader.GetBoolean(5)
        };

        WorkflowStatusRules.EnsureApprovalTaskConfiguration(record.Name, record.RequiresSupervisorStep, record.ApprovalSpecKey);
        return record;
    }

    internal static (string FirstName, string LastName) SplitDisplayName(string displayName)
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
}
