using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
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
            ApprovalTaskTemplateKey = reader.IsDBNull(4) ? null : reader.GetString(4).Trim(),
            RequiresTargetPerson = reader.GetBoolean(5),
            IsActive = reader.GetBoolean(6)
        };

        if (!record.IsActive)
        {
            throw new InvalidOperationException($"Der Prozesstyp '{record.Name}' ist deaktiviert.");
        }

        WorkflowStatusRules.EnsureApprovalTaskConfiguration(record.Name, record.RequiresSupervisorStep, record.ApprovalTaskTemplateKey);
        return record;
    }

    private static async Task<bool> HasProcessTypeManagerCreationColumn(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'process_types'
      AND column_name = 'allows_manager_creation'
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<TargetPersonRecord> LoadTargetPerson(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long targetPersonId)
    {
        const string sql = @"
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name), COALESCE(p.last_name, latest.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(latest.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    latest.position_role_id,
    r.name AS role_name,
    COALESCE(latest.employee_number, p.employee_number) AS employee_number,
    COALESCE(latest.badge_number, p.badge_number) AS badge_number,
    COALESCE(p.first_name, latest.first_name) AS first_name,
    COALESCE(p.last_name, latest.last_name) AS last_name
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
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
       OR (p.employee_number IS NOT NULL AND w.employee_number = p.employee_number)
       OR (
            w.employee_number > 0
            AND TRIM(COALESCE(w.first_name, '') || ' ' || COALESCE(w.last_name, '')) =
                COALESCE(
                    NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                    u.display_name,
                    'Person #' || p.id::text
                )
       )
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
}
