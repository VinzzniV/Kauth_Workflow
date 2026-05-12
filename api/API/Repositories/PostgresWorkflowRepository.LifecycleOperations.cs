using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Archivierung und Löschung als bewusst eingeschränkte Admin-/HR-Aktionen.
    public async Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string updateSql = @"
UPDATE workflows
SET archived_at = NOW()
WHERE uid = @uid
  AND status = 'completed'
  AND archived_at IS NULL
RETURNING id;";

        await using var updateCmd = new NpgsqlCommand(updateSql, connection, transaction);
        updateCmd.Parameters.AddWithValue("uid", workflowUid);
        var workflowId = await updateCmd.ExecuteScalarAsync();

        if (workflowId is null)
        {
            await transaction.RollbackAsync();
            return false;
        }

        const string auditSql = @"
INSERT INTO workflow_audit_log (workflow_id, actor_user_id, event_type, new_value)
VALUES (@workflowId, @actorUserId, 'workflow_archived', 'archived');";

        await using var auditCmd = new NpgsqlCommand(auditSql, connection, transaction);
        auditCmd.Parameters.AddWithValue("workflowId", (long)workflowId);
        auditCmd.Parameters.AddWithValue("actorUserId", actorUserId);
        await auditCmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteDraftWorkflow(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM workflows
WHERE uid = @uid
  AND status = 'draft'
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("uid", workflowUid);
        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    // Storno-Lookup: liefert nur Header (Department, Status, Person), damit der Aufrufer
    // AuthZ + Statusvorbedingung pruefen kann, bevor der eigentliche Cancel-Pfad transaktional laeuft.
    public async Task<WorkflowCancellationLookupDto?> LookupWorkflowForCancellation(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT id, department_id, status, target_person_id
FROM workflows
WHERE uid = @uid
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("uid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowCancellationLookupDto
        {
            WorkflowId = reader.GetInt64(0),
            DepartmentId = reader.GetInt32(1),
            WorkflowStatus = reader.GetString(2),
            TargetPersonId = reader.IsDBNull(3) ? null : reader.GetInt64(3)
        };
    }

    // Storno: aktiver Workflow wird terminal nach 'cancelled' ueberfuehrt, offene Tasks
    // werden mit-storniert, pending Notifications werden stillgelegt, und der Vorgang
    // erhaelt einen Audit-Eintrag inkl. ReasonCode + ReasonDetail (als JSON-Detail).
    public async Task<WorkflowCancellationResultDto?> CancelWorkflow(Guid workflowUid, string reasonCode, string? reasonDetail, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string lockWorkflowSql = @"
SELECT id, status
FROM workflows
WHERE uid = @uid
FOR UPDATE;";

        long workflowId;
        string previousStatus;
        await using (var lookupCmd = new NpgsqlCommand(lockWorkflowSql, connection, transaction))
        {
            lookupCmd.Parameters.AddWithValue("uid", workflowUid);
            await using var reader = await lookupCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await transaction.RollbackAsync();
                return null;
            }

            workflowId = reader.GetInt64(0);
            previousStatus = reader.GetString(1);
        }

        if (!WorkflowStatusRules.IsCancellable(previousStatus))
        {
            await transaction.RollbackAsync();
            return null;
        }

        var cancelledByPersonId = await ResolvePersonIdForActorUser(connection, transaction, actorUserId);

        const string applyCancelSql = @"
UPDATE workflows
SET status = 'cancelled',
    cancelled_at = NOW(),
    cancelled_by_person_id = @cancelledByPersonId,
    cancellation_reason_code = @reasonCode,
    cancellation_reason_detail = @reasonDetail
WHERE id = @id;";

        await using (var applyCmd = new NpgsqlCommand(applyCancelSql, connection, transaction))
        {
            applyCmd.Parameters.AddWithValue("id", workflowId);
            applyCmd.Parameters.AddWithValue("reasonCode", reasonCode);
            if (cancelledByPersonId.HasValue)
            {
                applyCmd.Parameters.AddWithValue("cancelledByPersonId", cancelledByPersonId.Value);
            }
            else
            {
                applyCmd.Parameters.Add("cancelledByPersonId", NpgsqlTypes.NpgsqlDbType.Bigint).Value = DBNull.Value;
            }
            if (reasonDetail is null)
            {
                applyCmd.Parameters.Add("reasonDetail", NpgsqlTypes.NpgsqlDbType.Text).Value = DBNull.Value;
            }
            else
            {
                applyCmd.Parameters.AddWithValue("reasonDetail", reasonDetail);
            }
            await applyCmd.ExecuteNonQueryAsync();
        }

        // Offene Tasks erst lesen (fuer Audit-Eintraege mit altem Status + Titel), dann en bloc stornieren.
        const string readOpenTasksSql = @"
SELECT id, status, title
FROM workflow_tasks
WHERE workflow_id = @workflowId
  AND status NOT IN ('done', 'cancelled')
FOR UPDATE;";

        var openTasks = new List<(long Id, string Status, string Title)>();
        await using (var readCmd = new NpgsqlCommand(readOpenTasksSql, connection, transaction))
        {
            readCmd.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await readCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                openTasks.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        var cancelledTaskCount = 0;
        if (openTasks.Count > 0)
        {
            const string updateTasksSql = @"
UPDATE workflow_tasks
SET status = 'cancelled'
WHERE workflow_id = @workflowId
  AND status NOT IN ('done', 'cancelled');";

            await using var updateTasksCmd = new NpgsqlCommand(updateTasksSql, connection, transaction);
            updateTasksCmd.Parameters.AddWithValue("workflowId", workflowId);
            cancelledTaskCount = await updateTasksCmd.ExecuteNonQueryAsync();

            foreach (var task in openTasks)
            {
                await PostgresRepositorySharedHelpers.InsertAuditEntry(
                    connection,
                    transaction,
                    workflowId,
                    task.Id,
                    actorUserId,
                    "task_status_changed",
                    task.Status,
                    "cancelled",
                    PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(task.Title));
            }
        }

        const string disableNotificationsSql = @"
UPDATE workflow_notifications
SET status = 'disabled'
WHERE workflow_id = @workflowId
  AND status = 'pending';";

        int disabledNotificationCount;
        await using (var disableNotifCmd = new NpgsqlCommand(disableNotificationsSql, connection, transaction))
        {
            disableNotifCmd.Parameters.AddWithValue("workflowId", workflowId);
            disabledNotificationCount = await disableNotifCmd.ExecuteNonQueryAsync();
        }

        // Workflow-Storno-Audit-Eintrag mit JSON-Detail (ReasonCode + ReasonDetail + Counts).
        var auditDetailJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            reasonCode,
            reasonDetail,
            cancelledTaskCount,
            disabledNotificationCount
        });

        const string auditSql = @"
INSERT INTO workflow_audit_log (workflow_id, actor_user_id, event_type, old_value, new_value, detail)
VALUES (@workflowId, @actorUserId, 'workflow_cancelled', @oldValue, 'cancelled', @detail);";

        await using (var auditCmd = new NpgsqlCommand(auditSql, connection, transaction))
        {
            auditCmd.Parameters.AddWithValue("workflowId", workflowId);
            auditCmd.Parameters.AddWithValue("actorUserId", actorUserId);
            auditCmd.Parameters.AddWithValue("oldValue", previousStatus);
            auditCmd.Parameters.AddWithValue("detail", auditDetailJson);
            await auditCmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        return new WorkflowCancellationResultDto
        {
            Uid = workflowUid,
            PreviousStatus = previousStatus,
            CancelledTaskCount = cancelledTaskCount,
            DisabledNotificationCount = disabledNotificationCount
        };
    }

    // Mappt den Akteur-User auf die zugehoerige Person (people.app_user_id), damit
    // cancelled_by_person_id konsistent zur Person-als-Fachanker-Trennung gepflegt wird.
    private static async Task<long?> ResolvePersonIdForActorUser(NpgsqlConnection connection, NpgsqlTransaction transaction, long actorUserId)
    {
        const string sql = @"
SELECT id
FROM people
WHERE app_user_id = @actorUserId
LIMIT 1;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        var result = await command.ExecuteScalarAsync();
        if (result is null || result is DBNull)
        {
            return null;
        }
        return Convert.ToInt64(result);
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
    w.workflow_definition_version_id,
    w.position_role_id,
    wd.id AS workflow_definition_id,
    w.department_id,
    w.status,
    wd.name,
    wd.requires_supervisor_step,
    wd.approval_spec_key
FROM workflows w
JOIN workflow_definitions wd ON wd.id = w.workflow_definition_id
WHERE w.uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;";

        long workflowId;
        long? workflowDefinitionVersionId;
        int workflowRoleId;
        int workflowDefinitionId;
        int workflowDepartmentId;
        string workflowStatus;
        string workflowDefinitionName;
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
            workflowDefinitionVersionId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            workflowRoleId = reader.GetInt32(2);
            workflowDefinitionId = reader.GetInt32(3);
            workflowDepartmentId = reader.GetInt32(4);
            workflowStatus = reader.GetString(5);
            workflowDefinitionName = reader.GetString(6);
            requiresSupervisorStep = reader.GetBoolean(7);
            approvalTaskTemplateKey = WorkflowStatusRules.EnsureApprovalTaskConfiguration(
                workflowDefinitionName,
                requiresSupervisorStep,
                reader.IsDBNull(8) ? null : reader.GetString(8));
        }

        if (!string.Equals(workflowStatus, "waiting_for_supervisor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Der Schritt der Abteilungsleitung kann nur abgeschlossen werden, solange der Fall auf die Abteilungsleitung wartet.");
        }

        if (!requiresSupervisorStep)
        {
            throw new InvalidOperationException("Fuer diesen Prozesstyp ist kein Supervisor-Schritt konfiguriert.");
        }

        if (workflowDefinitionVersionId.HasValue)
        {
            await PostgresWorkflowRuntimeRepository.CompleteRuntimeSupervisorGatekeeperStep(
                connection,
                transaction,
                workflowUid,
                selections,
                actorUserId);

            await transaction.CommitAsync();
            return await GetWorkflowByUid(workflowUid);
        }

        if (string.IsNullOrWhiteSpace(approvalTaskTemplateKey))
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

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction, workflowDefinitionId);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, workflowRoleId, workflowDefinitionId);

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

        var generatedTaskCount = await _taskGeneration.GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            answersByKey,
            TaskGenerationStage.AfterSupervisor);

        if (generatedTaskCount > 0)
        {
            await _auditWrite.InsertAuditEntry(
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

        await _statusCalculation.RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await _statusCalculation.RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);
        var nextWorkflowStatus = await LoadWorkflowStatusForUpdate(connection, transaction, workflowId);
        await _auditWrite.InsertAuditEntry(
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

}
