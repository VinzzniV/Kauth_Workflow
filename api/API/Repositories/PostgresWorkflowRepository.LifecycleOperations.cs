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
        long? workflowDefinitionVersionId;
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
            workflowDefinitionVersionId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            workflowRoleId = reader.GetInt32(2);
            workflowProcessTypeId = reader.GetInt32(3);
            workflowDepartmentId = reader.GetInt32(4);
            workflowStatus = reader.GetString(5);
            workflowProcessTypeName = reader.GetString(6);
            requiresSupervisorStep = reader.GetBoolean(7);
            approvalTaskTemplateKey = WorkflowStatusRules.EnsureApprovalTaskConfiguration(
                workflowProcessTypeName,
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
            await CompleteRuntimeSupervisorGatekeeperStep(
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
