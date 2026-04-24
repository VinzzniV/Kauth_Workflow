using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresRotationRepository
{
    private sealed class RotationDesiredTaskRecord
    {
        public required long RotationPlanId { get; init; }
        public required long RotationStationId { get; init; }
        public required long PersonId { get; init; }
        public required int DepartmentId { get; init; }
        public required string DepartmentName { get; init; }
        public required int OrderIndex { get; init; }
        public required int TemplateId { get; init; }
        public required string TemplateTitle { get; init; }
        public required string TriggerType { get; init; }
        public required DateOnly AnchorDate { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required string TaskType { get; init; }
        public int? ResponsibilityId { get; init; }
        public string? ResponsibilityName { get; init; }
        public DateOnly? DueDate { get; init; }
    }

    private sealed class RotationGeneratedTaskRecord
    {
        public required long Id { get; init; }
        public required long RotationPlanId { get; init; }
        public long? RotationStationId { get; init; }
        public required long PersonId { get; init; }
        public required int DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public int? TemplateId { get; init; }
        public string? TemplateTitle { get; init; }
        public required string TriggerType { get; init; }
        public required DateOnly AnchorDate { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required string TaskType { get; init; }
        public int? ResponsibilityId { get; init; }
        public string? ResponsibilityName { get; init; }
        public DateOnly? DueDate { get; init; }
        public required string Status { get; init; }
        public string? CompletionNote { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    private sealed class RotationPlanEnvelopeRecord
    {
        public required long RotationPlanId { get; init; }
        public required long PersonId { get; init; }
        public required Guid SourceWorkflowUid { get; init; }
        public required string PlanStatus { get; init; }
        public required string PlanTitle { get; init; }
        public required string DisplayName { get; init; }
        public required int DepartmentId { get; init; }
        public required string DepartmentName { get; init; }
    }

    public async Task<List<RotationGeneratedTaskDto>> GetRotationGeneratedTasks(long planId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await LoadRotationGeneratedTasks(connection, null, planId, null);
    }

    public async Task<List<TaskWithWorkflowDto>> GetAllRotationTaskEnvelopes()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await LoadRotationTaskEnvelopes(connection, null, null, null);
    }

    public async Task<List<TaskWithWorkflowDto>> GetRotationTaskEnvelopesForUser(long userId, int[] responsibilityIds)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await LoadRotationTaskEnvelopesForUser(connection, userId, responsibilityIds);
    }

    public async Task<RotationGeneratedTaskDto?> GetRotationGeneratedTask(long taskId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return (await LoadRotationGeneratedTasks(connection, null, null, taskId)).FirstOrDefault();
    }

    public async Task<List<long>> GetRotationPlanIdsForDepartment(int departmentId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT DISTINCT rs.rotation_plan_id
FROM rotation_stations rs
JOIN rotation_plans rp ON rp.id = rs.rotation_plan_id
WHERE rs.department_id = @departmentId
  AND rp.status IN ('draft', 'active')
ORDER BY rs.rotation_plan_id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await using var reader = await command.ExecuteReaderAsync();

        var planIds = new List<long>();
        while (await reader.ReadAsync())
        {
            planIds.Add(reader.GetInt64(reader.GetOrdinal("rotation_plan_id")));
        }

        return planIds;
    }

    public async Task<RotationTaskRegenerationResultDto> SynchronizeRotationGeneratedTasks(
        long planId,
        long actorUserId,
        string reason)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var planRecord = await LoadRotationPlanEnvelopeRecord(connection, transaction, planId);
            if (planRecord is null
                || planRecord.PlanStatus is RotationPlanStatuses.Completed or RotationPlanStatuses.Archived)
            {
                await transaction.CommitAsync();
                return new RotationTaskRegenerationResultDto
                {
                    Created = 0,
                    Updated = 0,
                    Cancelled = 0,
                    Unchanged = 0
                };
            }

            var desiredTasks = await LoadDesiredRotationTasks(connection, transaction, planId);
            var existingTasks = await LoadRotationGeneratedTaskRecords(connection, transaction, planId, null);
            var existingByKey = existingTasks
                .Where(task => task.RotationStationId.HasValue && task.TemplateId.HasValue)
                .ToDictionary(
                    task => BuildRotationTaskMatchKey(task.RotationStationId!.Value, task.TemplateId!.Value),
                    task => task);

            var created = 0;
            var updated = 0;
            var cancelled = 0;
            var unchanged = 0;
            var matchedTaskIds = new HashSet<long>();

            foreach (var desiredTask in desiredTasks)
            {
                var key = BuildRotationTaskMatchKey(desiredTask.RotationStationId, desiredTask.TemplateId);
                if (!existingByKey.TryGetValue(key, out var existingTask))
                {
                    var createdTaskId = await InsertRotationGeneratedTask(connection, transaction, desiredTask);
                    await UpsertRotationPrimaryAssignment(
                        connection,
                        transaction,
                        createdTaskId,
                        desiredTask.ResponsibilityId);
                    await InsertRotationAuditEntry(
                        connection,
                        transaction,
                        desiredTask.RotationPlanId,
                        desiredTask.RotationStationId,
                        createdTaskId,
                        actorUserId,
                        "rotation_task_generated",
                        null,
                        CreateRotationGeneratedTaskAuditSnapshot(desiredTask),
                        reason);
                    created++;
                    matchedTaskIds.Add(createdTaskId);
                    continue;
                }

                matchedTaskIds.Add(existingTask.Id);
                if (RotationTaskStatusRules.TerminalTaskStatuses.Contains(existingTask.Status))
                {
                    unchanged++;
                    continue;
                }

                if (RotationTaskNeedsUpdate(existingTask, desiredTask))
                {
                    await UpdateRotationGeneratedTask(connection, transaction, existingTask.Id, desiredTask);
                    await UpsertRotationPrimaryAssignment(
                        connection,
                        transaction,
                        existingTask.Id,
                        desiredTask.ResponsibilityId);
                    await InsertRotationAuditEntry(
                        connection,
                        transaction,
                        desiredTask.RotationPlanId,
                        desiredTask.RotationStationId,
                        existingTask.Id,
                        actorUserId,
                        "rotation_task_updated",
                        CreateRotationGeneratedTaskAuditSnapshot(existingTask),
                        CreateRotationGeneratedTaskAuditSnapshot(desiredTask),
                        reason);
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            foreach (var existingTask in existingTasks)
            {
                if (matchedTaskIds.Contains(existingTask.Id))
                {
                    continue;
                }

                if (!string.Equals(existingTask.Status, RotationTaskStatuses.Open, StringComparison.OrdinalIgnoreCase))
                {
                    unchanged++;
                    continue;
                }

                await CancelRotationGeneratedTask(connection, transaction, existingTask.Id);
                await InsertRotationAuditEntry(
                    connection,
                    transaction,
                    existingTask.RotationPlanId,
                    existingTask.RotationStationId,
                    existingTask.Id,
                    actorUserId,
                    "rotation_task_cancelled",
                    CreateRotationGeneratedTaskAuditSnapshot(existingTask),
                    new { status = RotationTaskStatuses.Cancelled },
                    reason);
                cancelled++;
            }

            var result = new RotationTaskRegenerationResultDto
            {
                Created = created,
                Updated = updated,
                Cancelled = cancelled,
                Unchanged = unchanged
            };

            await InsertRotationAuditEntry(
                connection,
                transaction,
                planId,
                null,
                null,
                actorUserId,
                "rotation_task_sync_completed",
                null,
                result,
                reason);

            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TaskWithWorkflowDto?> GetRotationTaskEnvelope(long taskId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return (await LoadRotationTaskEnvelopes(connection, null, null, taskId)).FirstOrDefault();
    }

    public async Task<TaskWithWorkflowDto?> GetRotationTaskEnvelopeByRef(string taskRef)
    {
        if (!RotationTaskRef.TryParse(taskRef, out var taskId))
        {
            return null;
        }

        return await GetRotationTaskEnvelope(taskId);
    }

    public async Task<TaskWithWorkflowDto?> UpdateRotationTaskStatusByRef(string taskRef, string status, long actorUserId)
    {
        if (!RotationTaskRef.TryParse(taskRef, out var taskId))
        {
            return null;
        }

        var normalizedStatus = RotationTaskStatusRules.NormalizeTaskStatus(status);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var taskRecord = (await LoadRotationGeneratedTaskRecords(connection, transaction, null, taskId)).FirstOrDefault();
        if (taskRecord is null)
        {
            return null;
        }

        RotationTaskStatusRules.EnsureTaskTransitionAllowed(taskRecord.Status, normalizedStatus);
        if (string.Equals(taskRecord.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync();
            return await GetRotationTaskEnvelope(taskId);
        }

        await PersistRotationTaskStatus(connection, transaction, taskId, normalizedStatus);
        await SyncRotationPrimaryAssignmentCompletion(connection, transaction, taskId, normalizedStatus);
        await InsertRotationAuditEntry(
            connection,
            transaction,
            taskRecord.RotationPlanId,
            taskRecord.RotationStationId,
            taskId,
            actorUserId,
            "rotation_task_status_changed",
            new { status = taskRecord.Status },
            new { status = normalizedStatus },
            taskRecord.Title);

        await transaction.CommitAsync();
        return await GetRotationTaskEnvelope(taskId);
    }

    public async Task<TaskWithWorkflowDto?> UpdateRotationTaskAssignmentByRef(
        string taskRef,
        TaskAssignRequest request,
        long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!RotationTaskRef.TryParse(taskRef, out var taskId))
        {
            return null;
        }

        if (request.AssigneeUserId is null && request.AssigneeResponsibilityId is null)
        {
            throw new InvalidOperationException("Either assigneeUserId or assigneeResponsibilityId must be provided.");
        }

        if (request.AssigneeUserId is <= 0)
        {
            throw new InvalidOperationException("assigneeUserId must be greater than zero.");
        }

        if (request.AssigneeResponsibilityId is <= 0)
        {
            throw new InvalidOperationException("assigneeResponsibilityId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var taskRecord = (await LoadRotationGeneratedTaskRecords(connection, transaction, null, taskId)).FirstOrDefault();
        if (taskRecord is null)
        {
            return null;
        }

        if (RotationTaskStatusRules.TerminalTaskStatuses.Contains(taskRecord.Status))
        {
            throw new InvalidOperationException("Assignment changes are not allowed for terminal task states.");
        }

        if (request.AssigneeResponsibilityId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureAssignableResponsibilityExists(connection, transaction, request.AssigneeResponsibilityId.Value);
        }

        if (request.AssigneeUserId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureAssignableUserExists(connection, transaction, request.AssigneeUserId.Value);
        }

        if (request.AssigneeUserId.HasValue && request.AssigneeResponsibilityId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureUserHasResponsibility(
                connection,
                transaction,
                request.AssigneeUserId.Value,
                request.AssigneeResponsibilityId.Value);
        }

        var oldAssigneeLabel = await LoadPrimaryRotationTaskAssignmentAuditLabel(connection, transaction, taskId);
        await ReplaceRotationPrimaryAssignment(
            connection,
            transaction,
            taskId,
            request.AssigneeUserId,
            request.AssigneeResponsibilityId);

        const string updateTaskSql = @"
UPDATE rotation_generated_tasks
SET responsibility_id = @responsibilityId,
    updated_at = NOW()
WHERE id = @taskId;";

        await using (var command = new NpgsqlCommand(updateTaskSql, connection, transaction))
        {
            command.Parameters.AddWithValue("taskId", taskId);
            command.Parameters.Add("responsibilityId", NpgsqlDbType.Integer).Value =
                (object?)request.AssigneeResponsibilityId ?? DBNull.Value;
            await command.ExecuteNonQueryAsync();
        }

        var newAssigneeLabel = request.AssigneeUserId.HasValue
            ? await PostgresRepositorySharedHelpers.LoadAssigneeUserAuditLabel(connection, transaction, request.AssigneeUserId.Value)
            : await PostgresRepositorySharedHelpers.LoadAssigneeResponsibilityAuditLabel(connection, transaction, request.AssigneeResponsibilityId!.Value);

        await InsertRotationAuditEntry(
            connection,
            transaction,
            taskRecord.RotationPlanId,
            taskRecord.RotationStationId,
            taskId,
            actorUserId,
            "rotation_task_assigned",
            oldAssigneeLabel is null ? null : new { assignee = oldAssigneeLabel },
            new { assignee = newAssigneeLabel },
            taskRecord.Title);

        await transaction.CommitAsync();
        return await GetRotationTaskEnvelope(taskId);
    }

    public async Task<TaskWithWorkflowDto?> AddRotationTaskCommentByRef(string taskRef, string commentText, long actorUserId)
    {
        if (!RotationTaskRef.TryParse(taskRef, out var taskId))
        {
            return null;
        }

        var normalizedComment = NormalizeRotationTaskComment(commentText);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var taskRecord = (await LoadRotationGeneratedTaskRecords(connection, transaction, null, taskId)).FirstOrDefault();
        if (taskRecord is null)
        {
            return null;
        }

        var planRecord = await LoadRotationPlanEnvelopeRecord(connection, transaction, taskRecord.RotationPlanId);
        if (planRecord is null)
        {
            return null;
        }

        if (planRecord.PlanStatus is RotationPlanStatuses.Completed or RotationPlanStatuses.Archived)
        {
            throw new InvalidOperationException("Kommentare sind fuer abgeschlossene Durchlaufplaene nicht mehr erlaubt.");
        }

        if (RotationTaskStatusRules.TerminalTaskStatuses.Contains(taskRecord.Status))
        {
            throw new InvalidOperationException("Kommentare sind fuer beendete Rotation-Tasks nicht mehr erlaubt.");
        }

        const string sql = @"
INSERT INTO rotation_task_comments (
    rotation_generated_task_id,
    author_user_id,
    comment_text
)
VALUES (
    @taskId,
    @actorUserId,
    @commentText
);";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("taskId", taskId);
            command.Parameters.AddWithValue("actorUserId", actorUserId);
            command.Parameters.AddWithValue("commentText", normalizedComment);
            await command.ExecuteNonQueryAsync();
        }

        await InsertRotationAuditEntry(
            connection,
            transaction,
            taskRecord.RotationPlanId,
            taskRecord.RotationStationId,
            taskId,
            actorUserId,
            "rotation_task_comment_added",
            null,
            null,
            normalizedComment);

        await transaction.CommitAsync();
        return await GetRotationTaskEnvelope(taskId);
    }

    public Task<TaskWithWorkflowDto?> DecideRotationTaskApprovalByRef(
        string taskRef,
        TaskApprovalDecisionRequest request,
        long actorUserId)
    {
        throw new InvalidOperationException("Rotation tasks do not support approval decisions.");
    }

    private static async Task<List<RotationDesiredTaskRecord>> LoadDesiredRotationTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long planId)
    {
        const string sql = @"
SELECT
    rp.id AS rotation_plan_id,
    rp.person_id,
    rs.id AS rotation_station_id,
    rs.department_id,
    d.name AS department_name,
    rs.order_index,
    t.id AS template_id,
    t.title AS template_title,
    t.trigger_type,
    CASE
        WHEN t.trigger_type = 'enter' THEN rs.start_date
        ELSE rs.end_date
    END AS anchor_date,
    t.description,
    t.task_type,
    t.default_responsibility_id,
    r.name AS responsibility_name,
    CASE
        WHEN t.trigger_type = 'enter' THEN rs.start_date + t.due_offset_days
        ELSE rs.end_date + t.due_offset_days
    END AS due_date
FROM rotation_plans rp
JOIN rotation_stations rs ON rs.rotation_plan_id = rp.id
JOIN departments d ON d.id = rs.department_id
JOIN department_action_templates t
    ON t.department_id = rs.department_id
   AND t.is_active = TRUE
LEFT JOIN app_responsibilities r ON r.id = t.default_responsibility_id
WHERE rp.id = @planId
  AND rp.status IN ('draft', 'active')
ORDER BY rs.order_index, rs.id, t.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("planId", planId);
        await using var reader = await command.ExecuteReaderAsync();

    var records = new List<RotationDesiredTaskRecord>();
    while (await reader.ReadAsync())
    {
        var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
        var personId = reader.GetOrdinal("person_id");
        var rotationStationId = reader.GetOrdinal("rotation_station_id");
        var departmentId = reader.GetOrdinal("department_id");
        var departmentName = reader.GetOrdinal("department_name");
        var orderIndex = reader.GetOrdinal("order_index");
        var templateId = reader.GetOrdinal("template_id");
        var templateTitle = reader.GetOrdinal("template_title");
        var triggerType = reader.GetOrdinal("trigger_type");
        var anchorDate = reader.GetOrdinal("anchor_date");
        var description = reader.GetOrdinal("description");
        var taskType = reader.GetOrdinal("task_type");
        var defaultResponsibilityId = reader.GetOrdinal("default_responsibility_id");
        var responsibilityName = reader.GetOrdinal("responsibility_name");
        var dueDate = reader.GetOrdinal("due_date");

        records.Add(new RotationDesiredTaskRecord
        {
            RotationPlanId = reader.GetInt64(rotationPlanId),
            PersonId = reader.GetInt64(personId),
            RotationStationId = reader.GetInt64(rotationStationId),
            DepartmentId = reader.GetInt32(departmentId),
            DepartmentName = reader.GetString(departmentName),
            OrderIndex = reader.GetInt32(orderIndex),
            TemplateId = reader.GetInt32(templateId),
            TemplateTitle = reader.GetString(templateTitle),
            TriggerType = reader.GetString(triggerType),
            AnchorDate = reader.GetFieldValue<DateOnly>(anchorDate),
            Title = reader.GetString(templateTitle),
            Description = reader.IsDBNull(description) ? null : reader.GetString(description),
            TaskType = reader.GetString(taskType),
            ResponsibilityId = reader.IsDBNull(defaultResponsibilityId) ? null : reader.GetInt32(defaultResponsibilityId),
            ResponsibilityName = reader.IsDBNull(responsibilityName) ? null : reader.GetString(responsibilityName),
            DueDate = reader.IsDBNull(dueDate) ? null : reader.GetFieldValue<DateOnly>(dueDate)
        });
    }

        return records;
    }

    private static async Task<long> InsertRotationGeneratedTask(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RotationDesiredTaskRecord desiredTask)
    {
        const string sql = @"
INSERT INTO rotation_generated_tasks (
    rotation_plan_id,
    rotation_station_id,
    person_id,
    department_id,
    template_id,
    trigger_type,
    anchor_date,
    title,
    description,
    task_type,
    responsibility_id,
    due_date,
    status
)
VALUES (
    @rotationPlanId,
    @rotationStationId,
    @personId,
    @departmentId,
    @templateId,
    @triggerType,
    @anchorDate,
    @title,
    @description,
    @taskType,
    @responsibilityId,
    @dueDate,
    'open'
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rotationPlanId", desiredTask.RotationPlanId);
        command.Parameters.AddWithValue("rotationStationId", desiredTask.RotationStationId);
        command.Parameters.AddWithValue("personId", desiredTask.PersonId);
        command.Parameters.AddWithValue("departmentId", desiredTask.DepartmentId);
        command.Parameters.AddWithValue("templateId", desiredTask.TemplateId);
        command.Parameters.AddWithValue("triggerType", desiredTask.TriggerType);
        command.Parameters.AddWithValue("anchorDate", desiredTask.AnchorDate);
        command.Parameters.AddWithValue("title", desiredTask.Title);
        command.Parameters.Add("description", NpgsqlDbType.Text).Value = (object?)desiredTask.Description ?? DBNull.Value;
        command.Parameters.AddWithValue("taskType", desiredTask.TaskType);
        command.Parameters.Add("responsibilityId", NpgsqlDbType.Integer).Value =
            (object?)desiredTask.ResponsibilityId ?? DBNull.Value;
        command.Parameters.Add("dueDate", NpgsqlDbType.Date).Value = (object?)desiredTask.DueDate ?? DBNull.Value;

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long taskId
            ? taskId
            : throw new InvalidOperationException("Rotation generated task could not be created.");
    }

    private static bool RotationTaskNeedsUpdate(RotationGeneratedTaskRecord existingTask, RotationDesiredTaskRecord desiredTask)
    {
        return existingTask.RotationStationId != desiredTask.RotationStationId
               || existingTask.DepartmentId != desiredTask.DepartmentId
               || existingTask.TemplateId != desiredTask.TemplateId
               || !string.Equals(existingTask.TriggerType, desiredTask.TriggerType, StringComparison.OrdinalIgnoreCase)
               || existingTask.AnchorDate != desiredTask.AnchorDate
               || !string.Equals(existingTask.Title, desiredTask.Title, StringComparison.Ordinal)
               || !string.Equals(existingTask.Description ?? string.Empty, desiredTask.Description ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existingTask.TaskType, desiredTask.TaskType, StringComparison.OrdinalIgnoreCase)
               || existingTask.ResponsibilityId != desiredTask.ResponsibilityId
               || existingTask.DueDate != desiredTask.DueDate;
    }

    private static async Task UpdateRotationGeneratedTask(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        RotationDesiredTaskRecord desiredTask)
    {
        const string sql = @"
UPDATE rotation_generated_tasks
SET rotation_station_id = @rotationStationId,
    department_id = @departmentId,
    template_id = @templateId,
    trigger_type = @triggerType,
    anchor_date = @anchorDate,
    title = @title,
    description = @description,
    task_type = @taskType,
    responsibility_id = @responsibilityId,
    due_date = @dueDate,
    updated_at = NOW()
WHERE id = @taskId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.Add("rotationStationId", NpgsqlDbType.Bigint).Value = desiredTask.RotationStationId;
        command.Parameters.AddWithValue("departmentId", desiredTask.DepartmentId);
        command.Parameters.AddWithValue("templateId", desiredTask.TemplateId);
        command.Parameters.AddWithValue("triggerType", desiredTask.TriggerType);
        command.Parameters.AddWithValue("anchorDate", desiredTask.AnchorDate);
        command.Parameters.AddWithValue("title", desiredTask.Title);
        command.Parameters.Add("description", NpgsqlDbType.Text).Value = (object?)desiredTask.Description ?? DBNull.Value;
        command.Parameters.AddWithValue("taskType", desiredTask.TaskType);
        command.Parameters.Add("responsibilityId", NpgsqlDbType.Integer).Value =
            (object?)desiredTask.ResponsibilityId ?? DBNull.Value;
        command.Parameters.Add("dueDate", NpgsqlDbType.Date).Value = (object?)desiredTask.DueDate ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CancelRotationGeneratedTask(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
UPDATE rotation_generated_tasks
SET status = 'cancelled',
    completed_at = COALESCE(completed_at, NOW()),
    updated_at = NOW()
WHERE id = @taskId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        await command.ExecuteNonQueryAsync();
        await SyncRotationPrimaryAssignmentCompletion(connection, transaction, taskId, RotationTaskStatuses.Cancelled);
    }

    private static async Task PersistRotationTaskStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string status)
    {
        const string sql = @"
UPDATE rotation_generated_tasks
SET status = @status,
    started_at = CASE
        WHEN @status = 'in_progress' AND started_at IS NULL THEN NOW()
        ELSE started_at
    END,
    completed_at = CASE
        WHEN @status IN ('completed', 'failed', 'cancelled') THEN COALESCE(completed_at, NOW())
        ELSE completed_at
    END,
    updated_at = NOW()
WHERE id = @taskId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpsertRotationPrimaryAssignment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        int? responsibilityId)
    {
        const string currentPrimarySql = @"
SELECT assignee_responsibility_id
FROM rotation_task_assignments
WHERE rotation_generated_task_id = @taskId
  AND is_primary = TRUE
ORDER BY id DESC
LIMIT 1;";

        int? currentResponsibilityId = null;
        await using (var command = new NpgsqlCommand(currentPrimarySql, connection, transaction))
        {
            command.Parameters.AddWithValue("taskId", taskId);
            var scalar = await command.ExecuteScalarAsync();
            currentResponsibilityId = scalar is null || scalar is DBNull ? null : (int?)scalar;
        }

        if (currentResponsibilityId == responsibilityId)
        {
            return;
        }

        const string clearPrimarySql = @"
UPDATE rotation_task_assignments
SET is_primary = FALSE,
    completed_at = COALESCE(completed_at, NOW())
WHERE rotation_generated_task_id = @taskId
  AND is_primary = TRUE;";

        await using (var clearCommand = new NpgsqlCommand(clearPrimarySql, connection, transaction))
        {
            clearCommand.Parameters.AddWithValue("taskId", taskId);
            await clearCommand.ExecuteNonQueryAsync();
        }

        if (!responsibilityId.HasValue)
        {
            return;
        }

        await PostgresRepositorySharedHelpers.EnsureAssignableResponsibilityExists(connection, transaction, responsibilityId.Value);

        const string insertSql = @"
INSERT INTO rotation_task_assignments (
    rotation_generated_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary,
    assigned_at,
    completed_at
)
VALUES (
    @taskId,
    NULL,
    @responsibilityId,
    'responsibility',
    TRUE,
    NOW(),
    NULL
);";

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("taskId", taskId);
        insertCommand.Parameters.AddWithValue("responsibilityId", responsibilityId.Value);
        await insertCommand.ExecuteNonQueryAsync();
    }

    private static async Task ReplaceRotationPrimaryAssignment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        long? assigneeUserId,
        int? assigneeResponsibilityId)
    {
        const string clearPrimarySql = @"
UPDATE rotation_task_assignments
SET is_primary = FALSE,
    completed_at = COALESCE(completed_at, NOW())
WHERE rotation_generated_task_id = @taskId
  AND is_primary = TRUE;";

        await using (var clearCommand = new NpgsqlCommand(clearPrimarySql, connection, transaction))
        {
            clearCommand.Parameters.AddWithValue("taskId", taskId);
            await clearCommand.ExecuteNonQueryAsync();
        }

        var assignmentType = assigneeUserId.HasValue ? "user" : "responsibility";
        const string insertSql = @"
INSERT INTO rotation_task_assignments (
    rotation_generated_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary,
    assigned_at,
    completed_at
)
VALUES (
    @taskId,
    @assigneeUserId,
    @assigneeResponsibilityId,
    @assignmentType,
    TRUE,
    NOW(),
    NULL
);";

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("taskId", taskId);
        insertCommand.Parameters.Add("assigneeUserId", NpgsqlDbType.Bigint).Value = (object?)assigneeUserId ?? DBNull.Value;
        insertCommand.Parameters.Add("assigneeResponsibilityId", NpgsqlDbType.Integer).Value =
            assignmentType == "responsibility"
                ? assigneeResponsibilityId!.Value
                : DBNull.Value;
        insertCommand.Parameters.AddWithValue("assignmentType", assignmentType);
        await insertCommand.ExecuteNonQueryAsync();
    }

    private static async Task SyncRotationPrimaryAssignmentCompletion(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string status)
    {
        if (!RotationTaskStatusRules.TerminalTaskStatuses.Contains(status))
        {
            return;
        }

        const string sql = @"
UPDATE rotation_task_assignments
SET completed_at = COALESCE(completed_at, NOW())
WHERE rotation_generated_task_id = @taskId
  AND is_primary = TRUE
  AND completed_at IS NULL;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> LoadPrimaryRotationTaskAssignmentAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT COALESCE(u.display_name, r.name) AS assignee_label
FROM rotation_task_assignments ta
LEFT JOIN app_users u ON u.id = ta.assignee_user_id
LEFT JOIN app_responsibilities r ON r.id = ta.assignee_responsibility_id
WHERE ta.rotation_generated_task_id = @taskId
  AND ta.is_primary = TRUE
ORDER BY ta.id DESC
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<List<RotationGeneratedTaskRecord>> LoadRotationGeneratedTaskRecords(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? planId,
        long? taskId)
    {
        const string sql = @"
SELECT
    rgt.id AS generated_task_id,
    rgt.rotation_plan_id,
    rgt.rotation_station_id,
    rgt.person_id,
    rgt.department_id,
    department.name AS department_name,
    rgt.template_id,
    template.title AS template_title,
    rgt.trigger_type,
    rgt.anchor_date,
    rgt.title,
    rgt.description,
    rgt.task_type,
    rgt.responsibility_id,
    responsibility.name AS responsibility_name,
    rgt.due_date,
    rgt.status,
    rgt.completion_note,
    rgt.started_at,
    rgt.completed_at,
    rgt.created_at,
    rgt.updated_at
FROM rotation_generated_tasks rgt
JOIN departments department ON department.id = rgt.department_id
LEFT JOIN department_action_templates template ON template.id = rgt.template_id
LEFT JOIN app_responsibilities responsibility ON responsibility.id = rgt.responsibility_id
WHERE (@planId IS NULL OR rgt.rotation_plan_id = @planId)
  AND (@taskId IS NULL OR rgt.id = @taskId)
ORDER BY rgt.created_at DESC, rgt.id DESC;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("planId", NpgsqlDbType.Bigint).Value = (object?)planId ?? DBNull.Value;
        command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync();

    var records = new List<RotationGeneratedTaskRecord>();
    while (await reader.ReadAsync())
    {
        var generatedTaskId = reader.GetOrdinal("generated_task_id");
        var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
        var rotationStationId = reader.GetOrdinal("rotation_station_id");
        var personId = reader.GetOrdinal("person_id");
        var departmentId = reader.GetOrdinal("department_id");
        var departmentName = reader.GetOrdinal("department_name");
        var templateId = reader.GetOrdinal("template_id");
        var templateTitle = reader.GetOrdinal("template_title");
        var triggerType = reader.GetOrdinal("trigger_type");
        var anchorDate = reader.GetOrdinal("anchor_date");
        var title = reader.GetOrdinal("title");
        var description = reader.GetOrdinal("description");
        var taskType = reader.GetOrdinal("task_type");
        var responsibilityId = reader.GetOrdinal("responsibility_id");
        var responsibilityName = reader.GetOrdinal("responsibility_name");
        var dueDate = reader.GetOrdinal("due_date");
        var status = reader.GetOrdinal("status");
        var completionNote = reader.GetOrdinal("completion_note");
        var startedAt = reader.GetOrdinal("started_at");
        var completedAt = reader.GetOrdinal("completed_at");
        var createdAt = reader.GetOrdinal("created_at");
        var updatedAt = reader.GetOrdinal("updated_at");

        records.Add(new RotationGeneratedTaskRecord
        {
            Id = reader.GetInt64(generatedTaskId),
            RotationPlanId = reader.GetInt64(rotationPlanId),
            RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
            PersonId = reader.GetInt64(personId),
            DepartmentId = reader.GetInt32(departmentId),
            DepartmentName = reader.IsDBNull(departmentName) ? null : reader.GetString(departmentName),
            TemplateId = reader.IsDBNull(templateId) ? null : reader.GetInt32(templateId),
            TemplateTitle = reader.IsDBNull(templateTitle) ? null : reader.GetString(templateTitle),
            TriggerType = reader.GetString(triggerType),
            AnchorDate = reader.GetFieldValue<DateOnly>(anchorDate),
            Title = reader.GetString(title),
            Description = reader.IsDBNull(description) ? null : reader.GetString(description),
            TaskType = reader.GetString(taskType),
            ResponsibilityId = reader.IsDBNull(responsibilityId) ? null : reader.GetInt32(responsibilityId),
            ResponsibilityName = reader.IsDBNull(responsibilityName) ? null : reader.GetString(responsibilityName),
            DueDate = reader.IsDBNull(dueDate) ? null : reader.GetFieldValue<DateOnly>(dueDate),
            Status = reader.GetString(status),
            CompletionNote = reader.IsDBNull(completionNote) ? null : reader.GetString(completionNote),
            StartedAt = reader.IsDBNull(startedAt) ? null : reader.GetDateTime(startedAt),
            CompletedAt = reader.IsDBNull(completedAt) ? null : reader.GetDateTime(completedAt),
            CreatedAt = reader.GetDateTime(createdAt),
            UpdatedAt = reader.GetDateTime(updatedAt)
        });
    }

        return records;
    }

    private static async Task<List<RotationGeneratedTaskDto>> LoadRotationGeneratedTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? planId,
        long? taskId)
    {
        var taskRecords = await LoadRotationGeneratedTaskRecords(connection, transaction, planId, taskId);
        if (taskRecords.Count == 0)
        {
            return [];
        }

        var taskIds = taskRecords.Select(task => task.Id).ToArray();
        var assignmentsByTaskId = await LoadRotationTaskAssignments(connection, transaction, taskIds);
        var commentsByTaskId = await LoadRotationTaskComments(connection, transaction, taskIds);

        return taskRecords.Select(task => new RotationGeneratedTaskDto
        {
            Id = task.Id,
            TaskRef = RotationTaskRef.Build(task.Id),
            RotationPlanId = task.RotationPlanId,
            RotationStationId = task.RotationStationId,
            PersonId = task.PersonId,
            DepartmentId = task.DepartmentId,
            DepartmentName = task.DepartmentName,
            TemplateId = task.TemplateId,
            TemplateTitle = task.TemplateTitle,
            TriggerType = task.TriggerType,
            AnchorDate = task.AnchorDate,
            Title = task.Title,
            Description = task.Description,
            TaskType = task.TaskType,
            ResponsibilityId = task.ResponsibilityId,
            ResponsibilityName = task.ResponsibilityName,
            DueDate = task.DueDate,
            Status = task.Status,
            CompletionNote = task.CompletionNote,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Assignments = assignmentsByTaskId.TryGetValue(task.Id, out var assignments) ? assignments : [],
            Comments = commentsByTaskId.TryGetValue(task.Id, out var comments) ? comments : []
        }).ToList();
    }

    private static Task<List<TaskWithWorkflowDto>> LoadRotationTaskEnvelopesForUser(
        NpgsqlConnection connection,
        long userId,
        int[] responsibilityIds)
        => LoadRotationTaskEnvelopes(connection, null, null, null, userId, responsibilityIds);

    private static async Task<List<TaskWithWorkflowDto>> LoadRotationTaskEnvelopes(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? planId,
        long? taskId,
        long? filterUserId = null,
        int[]? filterResponsibilityIds = null)
    {
        const string sql = @"
SELECT
    rgt.id AS generated_task_id,
    rgt.rotation_plan_id,
    rp.status AS plan_status,
    rp.title AS plan_title,
    w.uid AS source_workflow_uid,
    rp.person_id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    d.name AS department_name,
    rgt.rotation_station_id,
    rgt.template_id,
    template.title AS template_title,
    rgt.trigger_type,
    rgt.anchor_date,
    rgt.title,
    rgt.description,
    rgt.task_type,
    rgt.responsibility_id,
    responsibility.name AS responsibility_name,
    rgt.due_date,
    rgt.status,
    rgt.completion_note,
    rgt.created_at,
    rgt.updated_at,
    rgt.started_at,
    rgt.completed_at
FROM rotation_generated_tasks rgt
JOIN rotation_plans rp ON rp.id = rgt.rotation_plan_id
JOIN workflows w ON w.id = rp.source_workflow_id
JOIN departments d ON d.id = rgt.department_id
LEFT JOIN people p ON p.id = rp.person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN department_action_templates template ON template.id = rgt.template_id
LEFT JOIN app_responsibilities responsibility ON responsibility.id = rgt.responsibility_id
WHERE (@planId IS NULL OR rgt.rotation_plan_id = @planId)
  AND (@taskId IS NULL OR rgt.id = @taskId)
  AND (
      @filterUserId IS NULL
      OR EXISTS (
          SELECT 1 FROM rotation_task_assignments rta
          WHERE rta.rotation_generated_task_id = rgt.id
            AND rta.is_primary = TRUE
            AND (
                rta.assignee_responsibility_id = ANY(@filterResponsibilityIds)
                OR rta.assignee_user_id = @filterUserId
            )
      )
  )
ORDER BY rgt.created_at DESC, rgt.id DESC;";

        var taskRecords = new List<RotationGeneratedTaskRecord>();
        var planRecordsByTaskId = new Dictionary<long, RotationPlanEnvelopeRecord>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("planId", NpgsqlDbType.Bigint).Value = (object?)planId ?? DBNull.Value;
            command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
            command.Parameters.Add("filterUserId", NpgsqlDbType.Bigint).Value = (object?)filterUserId ?? DBNull.Value;
            command.Parameters.Add("filterResponsibilityIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
                (object?)filterResponsibilityIds ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var generatedTaskId = reader.GetOrdinal("generated_task_id");
                var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
                var planStatus = reader.GetOrdinal("plan_status");
                var planTitle = reader.GetOrdinal("plan_title");
                var sourceWorkflowUid = reader.GetOrdinal("source_workflow_uid");
                var personId = reader.GetOrdinal("person_id");
                var displayName = reader.GetOrdinal("display_name");
                var departmentId = reader.GetOrdinal("department_id");
                var departmentName = reader.GetOrdinal("department_name");
                var rotationStationId = reader.GetOrdinal("rotation_station_id");
                var templateId = reader.GetOrdinal("template_id");
                var templateTitle = reader.GetOrdinal("template_title");
                var triggerType = reader.GetOrdinal("trigger_type");
                var anchorDate = reader.GetOrdinal("anchor_date");
                var title = reader.GetOrdinal("title");
                var description = reader.GetOrdinal("description");
                var taskType = reader.GetOrdinal("task_type");
                var responsibilityId = reader.GetOrdinal("responsibility_id");
                var responsibilityName = reader.GetOrdinal("responsibility_name");
                var dueDate = reader.GetOrdinal("due_date");
                var status = reader.GetOrdinal("status");
                var completionNote = reader.GetOrdinal("completion_note");
                var createdAt = reader.GetOrdinal("created_at");
                var updatedAt = reader.GetOrdinal("updated_at");
                var startedAt = reader.GetOrdinal("started_at");
                var completedAt = reader.GetOrdinal("completed_at");

                var id = reader.GetInt64(generatedTaskId);
                taskRecords.Add(new RotationGeneratedTaskRecord
                {
                    Id = id,
                    RotationPlanId = reader.GetInt64(rotationPlanId),
                    RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
                    PersonId = reader.GetInt64(personId),
                    DepartmentId = reader.GetInt32(departmentId),
                    DepartmentName = reader.GetString(departmentName),
                    TemplateId = reader.IsDBNull(templateId) ? null : reader.GetInt32(templateId),
                    TemplateTitle = reader.IsDBNull(templateTitle) ? null : reader.GetString(templateTitle),
                    TriggerType = reader.GetString(triggerType),
                    AnchorDate = reader.GetFieldValue<DateOnly>(anchorDate),
                    Title = reader.GetString(title),
                    Description = reader.IsDBNull(description) ? null : reader.GetString(description),
                    TaskType = reader.GetString(taskType),
                    ResponsibilityId = reader.IsDBNull(responsibilityId) ? null : reader.GetInt32(responsibilityId),
                    ResponsibilityName = reader.IsDBNull(responsibilityName) ? null : reader.GetString(responsibilityName),
                    DueDate = reader.IsDBNull(dueDate) ? null : reader.GetFieldValue<DateOnly>(dueDate),
                    Status = reader.GetString(status),
                    CompletionNote = reader.IsDBNull(completionNote) ? null : reader.GetString(completionNote),
                    CreatedAt = reader.GetDateTime(createdAt),
                    UpdatedAt = reader.GetDateTime(updatedAt),
                    StartedAt = reader.IsDBNull(startedAt) ? null : reader.GetDateTime(startedAt),
                    CompletedAt = reader.IsDBNull(completedAt) ? null : reader.GetDateTime(completedAt)
                });

                planRecordsByTaskId[id] = new RotationPlanEnvelopeRecord
                {
                    RotationPlanId = reader.GetInt64(rotationPlanId),
                    PlanStatus = reader.GetString(planStatus),
                    PlanTitle = reader.GetString(planTitle),
                    SourceWorkflowUid = reader.GetGuid(sourceWorkflowUid),
                    PersonId = reader.GetInt64(personId),
                    DisplayName = reader.GetString(displayName),
                    DepartmentId = reader.GetInt32(departmentId),
                    DepartmentName = reader.GetString(departmentName)
                };
            }
        }

        if (taskRecords.Count == 0)
        {
            return [];
        }

        var taskIds = taskRecords.Select(task => task.Id).ToArray();
        var assignmentsByTaskId = await LoadRotationTaskAssignments(connection, transaction, taskIds);
        var commentsByTaskId = await LoadRotationTaskComments(connection, transaction, taskIds);

        return taskRecords.Select(task =>
        {
            var planRecord = planRecordsByTaskId[task.Id];
            DateTime? dueAt = task.DueDate.HasValue
                ? DateTime.SpecifyKind(task.DueDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                : null;

            return new TaskWithWorkflowDto
            {
                TaskRef = RotationTaskRef.Build(task.Id),
                TaskFamily = TaskFamilyNames.Rotation,
                Task = new WorkflowTaskDto
                {
                    Id = task.Id,
                    TaskTemplateId = task.TemplateId,
                    TaskKey = task.TemplateId.HasValue ? $"rotation-template-{task.TemplateId.Value}" : $"rotation-task-{task.Id}",
                    IsRuntimeNodeTask = false,
                    IsApprovalTask = false,
                    Title = task.Title,
                    Description = task.Description ?? string.Empty,
                    Category = task.TaskType,
                    IconKey = ResolveRotationTaskIconKey(task.TaskType),
                    Status = task.Status,
                    IsRequired = true,
                    SortOrder = task.RotationStationId.HasValue && task.RotationStationId.Value <= int.MaxValue
                        ? (int)task.RotationStationId.Value
                        : 0,
                    CreatedAt = task.CreatedAt,
                    DueAt = dueAt,
                    SlaStatus = TaskDueDateRules.ResolveSlaStatus(task.Status, dueAt, RotationTaskStatusRules.TerminalTaskStatuses),
                    ReadyAt = task.CreatedAt,
                    StartedAt = task.StartedAt,
                    CompletedAt = task.CompletedAt,
                    ProcessArea = task.DepartmentName,
                    IsDepartmentPhaseTask = true,
                    Assignments = assignmentsByTaskId.TryGetValue(task.Id, out var assignments) ? assignments : [],
                    Dependencies = [],
                    Comments = commentsByTaskId.TryGetValue(task.Id, out var comments) ? comments : []
                },
                Workflow = null,
                Rotation = new TaskRotationContextDto
                {
                    RotationPlanId = planRecord.RotationPlanId,
                    PlanStatus = planRecord.PlanStatus,
                    PlanTitle = planRecord.PlanTitle,
                    SourceWorkflowUid = planRecord.SourceWorkflowUid,
                    PersonId = planRecord.PersonId,
                    DisplayName = planRecord.DisplayName,
                    DepartmentId = planRecord.DepartmentId,
                    DepartmentName = planRecord.DepartmentName,
                    RotationStationId = task.RotationStationId,
                    TriggerType = task.TriggerType,
                    AnchorDate = task.AnchorDate
                }
            };
        }).ToList();
    }

    private static async Task<Dictionary<long, List<WorkflowTaskAssignmentDto>>> LoadRotationTaskAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<long> taskIds)
    {
        var result = taskIds.ToDictionary(taskId => taskId, _ => new List<WorkflowTaskAssignmentDto>());
        if (taskIds.Count == 0)
        {
            return result;
        }

        const string sql = @"
SELECT
    ta.rotation_generated_task_id,
    ta.id AS assignment_id,
    ta.assignment_type,
    ta.is_primary,
    ta.assigned_at,
    ta.completed_at,
    ta.assignee_user_id,
    u.display_name AS assignee_user_name,
    u.email AS assignee_user_email,
    ta.assignee_responsibility_id,
    r.responsibility_key AS assignee_responsibility_key,
    r.name AS assignee_responsibility_name,
    r.responsibility_type AS assignee_responsibility_type
FROM rotation_task_assignments ta
LEFT JOIN app_users u ON u.id = ta.assignee_user_id
LEFT JOIN app_responsibilities r ON r.id = ta.assignee_responsibility_id
WHERE ta.rotation_generated_task_id = ANY(@taskIds)
ORDER BY ta.rotation_generated_task_id, ta.is_primary DESC, ta.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds.ToArray();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var rotationGeneratedTaskId = reader.GetOrdinal("rotation_generated_task_id");
        var assignmentId = reader.GetOrdinal("assignment_id");
        var assignmentType = reader.GetOrdinal("assignment_type");
        var isPrimary = reader.GetOrdinal("is_primary");
        var assignedAt = reader.GetOrdinal("assigned_at");
        var completedAt = reader.GetOrdinal("completed_at");
        var assigneeUserId = reader.GetOrdinal("assignee_user_id");
        var assigneeUserName = reader.GetOrdinal("assignee_user_name");
        var assigneeUserEmail = reader.GetOrdinal("assignee_user_email");
        var assigneeResponsibilityId = reader.GetOrdinal("assignee_responsibility_id");
        var assigneeResponsibilityKey = reader.GetOrdinal("assignee_responsibility_key");
        var assigneeResponsibilityName = reader.GetOrdinal("assignee_responsibility_name");
        var assigneeResponsibilityType = reader.GetOrdinal("assignee_responsibility_type");

        var taskId = reader.GetInt64(rotationGeneratedTaskId);
        result[taskId].Add(new WorkflowTaskAssignmentDto
        {
            Id = reader.GetInt64(assignmentId),
            AssignmentType = reader.GetString(assignmentType),
            IsPrimary = reader.GetBoolean(isPrimary),
            AssignedAt = reader.GetDateTime(assignedAt),
            CompletedAt = reader.IsDBNull(completedAt) ? null : reader.GetDateTime(completedAt),
            AssigneeUserId = reader.IsDBNull(assigneeUserId) ? null : reader.GetInt64(assigneeUserId),
            AssigneeUserName = reader.IsDBNull(assigneeUserName) ? null : reader.GetString(assigneeUserName),
            AssigneeUserEmail = reader.IsDBNull(assigneeUserEmail) ? null : reader.GetString(assigneeUserEmail),
            AssigneeResponsibilityId = reader.IsDBNull(assigneeResponsibilityId) ? null : reader.GetInt32(assigneeResponsibilityId),
            AssigneeResponsibilityKey = reader.IsDBNull(assigneeResponsibilityKey) ? null : reader.GetString(assigneeResponsibilityKey),
            AssigneeResponsibilityName = reader.IsDBNull(assigneeResponsibilityName) ? null : reader.GetString(assigneeResponsibilityName),
            AssigneeResponsibilityType = reader.IsDBNull(assigneeResponsibilityType) ? null : reader.GetString(assigneeResponsibilityType)
        });
    }

        return result;
    }

    private static async Task<Dictionary<long, List<WorkflowTaskCommentDto>>> LoadRotationTaskComments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<long> taskIds)
    {
        var result = taskIds.ToDictionary(taskId => taskId, _ => new List<WorkflowTaskCommentDto>());
        if (taskIds.Count == 0)
        {
            return result;
        }

        const string sql = @"
SELECT
    c.rotation_generated_task_id,
    c.id AS comment_id,
    c.author_user_id,
    u.display_name AS author_user_name,
    c.comment_text,
    c.created_at
FROM rotation_task_comments c
LEFT JOIN app_users u ON u.id = c.author_user_id
WHERE c.rotation_generated_task_id = ANY(@taskIds)
ORDER BY c.rotation_generated_task_id, c.created_at, c.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds.ToArray();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rotationGeneratedTaskId = reader.GetOrdinal("rotation_generated_task_id");
            var commentId = reader.GetOrdinal("comment_id");
            var authorUserId = reader.GetOrdinal("author_user_id");
            var authorUserName = reader.GetOrdinal("author_user_name");
            var commentText = reader.GetOrdinal("comment_text");
            var createdAt = reader.GetOrdinal("created_at");

            var taskId = reader.GetInt64(rotationGeneratedTaskId);
            result[taskId].Add(new WorkflowTaskCommentDto
            {
                Id = reader.GetInt64(commentId),
                TaskId = taskId,
                AuthorUserId = reader.IsDBNull(authorUserId) ? null : reader.GetInt64(authorUserId),
                AuthorUserName = reader.IsDBNull(authorUserName) ? null : reader.GetString(authorUserName),
                CommentText = reader.GetString(commentText),
                CreatedAt = reader.GetDateTime(createdAt)
            });
        }

        return result;
    }

    private static async Task<RotationPlanEnvelopeRecord?> LoadRotationPlanEnvelopeRecord(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long planId)
    {
        const string sql = @"
SELECT
    rp.id AS rotation_plan_id,
    rp.person_id,
    w.uid AS source_workflow_uid,
    rp.status AS plan_status,
    rp.title AS plan_title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    COALESCE(w.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name
FROM rotation_plans rp
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN people p ON p.id = rp.person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments d ON d.id = COALESCE(w.department_id, p.department_id, u.department_id)
WHERE rp.id = @planId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("planId", planId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
        var personId = reader.GetOrdinal("person_id");
        var sourceWorkflowUid = reader.GetOrdinal("source_workflow_uid");
        var planStatus = reader.GetOrdinal("plan_status");
        var planTitle = reader.GetOrdinal("plan_title");
        var displayName = reader.GetOrdinal("display_name");
        var departmentId = reader.GetOrdinal("department_id");
        var departmentName = reader.GetOrdinal("department_name");

        return new RotationPlanEnvelopeRecord
        {
            RotationPlanId = reader.GetInt64(rotationPlanId),
            PersonId = reader.GetInt64(personId),
            SourceWorkflowUid = reader.GetGuid(sourceWorkflowUid),
            PlanStatus = reader.GetString(planStatus),
            PlanTitle = reader.GetString(planTitle),
            DisplayName = reader.GetString(displayName),
            DepartmentId = reader.GetInt32(departmentId),
            DepartmentName = reader.IsDBNull(departmentName) ? string.Empty : reader.GetString(departmentName)
        };
    }

    private static async Task InsertRotationAuditEntry(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rotationPlanId,
        long? rotationStationId,
        long? generatedTaskId,
        long? actorUserId,
        string eventType,
        object? oldValue,
        object? newValue,
        string? detail)
    {
        const string sql = @"
INSERT INTO rotation_audit_log (
    rotation_plan_id,
    rotation_station_id,
    generated_task_id,
    actor_user_id,
    event_type,
    old_value,
    new_value,
    detail
)
VALUES (
    @rotationPlanId,
    @rotationStationId,
    @generatedTaskId,
    @actorUserId,
    @eventType,
    @oldValue,
    @newValue,
    @detail
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rotationPlanId", rotationPlanId);
        command.Parameters.Add("rotationStationId", NpgsqlDbType.Bigint).Value = (object?)rotationStationId ?? DBNull.Value;
        command.Parameters.Add("generatedTaskId", NpgsqlDbType.Bigint).Value = (object?)generatedTaskId ?? DBNull.Value;
        command.Parameters.Add("actorUserId", NpgsqlDbType.Bigint).Value = (object?)actorUserId ?? DBNull.Value;
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.Add("oldValue", NpgsqlDbType.Jsonb).Value =
            oldValue is null ? DBNull.Value : JsonSerializer.Serialize(oldValue);
        command.Parameters.Add("newValue", NpgsqlDbType.Jsonb).Value =
            newValue is null ? DBNull.Value : JsonSerializer.Serialize(newValue);
        command.Parameters.Add("detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }

    private static object CreateRotationGeneratedTaskAuditSnapshot(RotationDesiredTaskRecord task)
    {
        return new
        {
            task.RotationPlanId,
            task.RotationStationId,
            task.DepartmentId,
            task.TemplateId,
            task.TriggerType,
            task.AnchorDate,
            task.Title,
            task.TaskType,
            task.ResponsibilityId,
            task.DueDate
        };
    }

    private static object CreateRotationGeneratedTaskAuditSnapshot(RotationGeneratedTaskRecord task)
    {
        return new
        {
            task.RotationPlanId,
            task.RotationStationId,
            task.DepartmentId,
            task.TemplateId,
            task.TriggerType,
            task.AnchorDate,
            task.Title,
            task.TaskType,
            task.ResponsibilityId,
            task.DueDate,
            task.Status
        };
    }

    private static string NormalizeRotationTaskComment(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            throw new InvalidOperationException("Kommentar darf nicht leer sein.");
        }

        var normalized = commentText.Trim();
        if (normalized.Length > 2000)
        {
            throw new InvalidOperationException("Kommentar darf maximal 2000 Zeichen haben.");
        }

        return normalized;
    }

    private static string BuildRotationTaskMatchKey(long stationId, int templateId)
    {
        return $"{stationId}:{templateId}";
    }

    private static string ResolveRotationTaskIconKey(string taskType)
    {
        return taskType.Trim().ToLowerInvariant() switch
        {
            RotationTaskTypes.Technical => "computer",
            RotationTaskTypes.Information => "info",
            RotationTaskTypes.Approval => "approval",
            _ => "checklist"
        };
    }
}
