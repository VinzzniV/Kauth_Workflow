using Npgsql;

namespace API;

// Verknüpfungsoperationen: Links zwischen Workflows erstellen, lesen, löschen
// und Antworten aus einem Quell-Workflow ableiten.
internal sealed partial class PostgresWorkflowRepository
{
    private sealed class WorkflowLinkLookupRecord
    {
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
        public required int ProcessTypeId { get; init; }
        public required string ProcessTypeKey { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
    }

    public async Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        // Alle Links laden, bei denen der angegebene Workflow entweder Quelle oder Ziel ist.
        // Die "andere Seite" des Links wird als LinkedWorkflow zurückgegeben.
        const string sql = @"
SELECT
    wl.id,
    source_w.uid   AS source_uid,
    target_w.uid   AS target_uid,
    wl.link_type,
    wl.notes,
    wl.created_by_user_id,
    u.display_name AS created_by_user_name,
    wl.created_at,
    -- Linked workflow (the other side)
    linked_w.uid         AS linked_uid,
    linked_w.first_name,
    linked_w.last_name,
    linked_w.status      AS linked_status,
    linked_w.created_at  AS linked_created_at,
    pt.key               AS linked_pt_key,
    pt.name              AS linked_pt_name,
    pt.requires_target_person
FROM workflow_links wl
JOIN workflows source_w ON source_w.id = wl.source_workflow_id
JOIN workflows target_w ON target_w.id = wl.target_workflow_id
-- Linked workflow = die jeweils andere Seite des Links
JOIN workflows linked_w ON linked_w.id = CASE
    WHEN source_w.uid = @uid THEN target_w.id
    ELSE source_w.id
END
JOIN process_types pt ON pt.id = linked_w.process_type_id
LEFT JOIN app_users u ON u.id = wl.created_by_user_id
WHERE source_w.uid = @uid OR target_w.uid = @uid
ORDER BY wl.created_at DESC;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        var links = new List<WorkflowLinkDto>();
        while (await reader.ReadAsync())
        {
            links.Add(new WorkflowLinkDto
            {
                Id = reader.GetInt64(0),
                SourceWorkflowUid = reader.GetGuid(1),
                TargetWorkflowUid = reader.GetGuid(2),
                LinkType = reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedByUserId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                CreatedByUserName = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                LinkedWorkflowFirstName = reader.GetString(9),
                LinkedWorkflowLastName = reader.GetString(10),
                LinkedWorkflowStatus = reader.GetString(11),
                LinkedWorkflowCreatedAt = reader.GetDateTime(12),
                LinkedWorkflowProcessType = new WorkflowProcessTypeDto
                {
                    Key = reader.GetString(13),
                    Name = reader.GetString(14),
                    RequiresTargetPerson = reader.GetBoolean(15)
                }
            });
        }

        return links;
    }

    public async Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
WITH base_workflow AS (
    SELECT
        w.uid,
        w.target_person_id,
        w.employee_number
    FROM workflows w
    WHERE w.uid = @uid
      AND w.workflow_definition_version_id IS NULL
    LIMIT 1
)
SELECT
    related.uid,
    related.status,
    related.created_at,
    related.department_id,
    pt.key,
    pt.name,
    pt.requires_target_person
FROM base_workflow base
JOIN workflows related
    ON related.uid <> base.uid
   AND related.workflow_definition_version_id IS NULL
   AND (
        (base.target_person_id IS NOT NULL AND related.target_person_id = base.target_person_id)
        OR (
            related.employee_number = base.employee_number
            AND (base.target_person_id IS NULL OR related.target_person_id IS NULL)
        )
   )
JOIN process_types pt ON pt.id = related.process_type_id
ORDER BY related.created_at DESC;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        var workflows = new List<RelatedWorkflowSummaryDto>();
        while (await reader.ReadAsync())
        {
            workflows.Add(new RelatedWorkflowSummaryDto
            {
                Uid = reader.GetGuid(0),
                WorkflowStatus = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                DepartmentId = reader.GetInt32(3),
                ProcessType = new WorkflowProcessTypeDto
                {
                    Key = reader.GetString(4),
                    Name = reader.GetString(5),
                    RequiresTargetPerson = reader.GetBoolean(6)
                }
            });
        }

        return workflows;
    }

    public async Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var sourceWorkflow = await LoadWorkflowLinkLookup(connection, transaction, request.SourceWorkflowUid);
        var targetWorkflow = await LoadWorkflowLinkLookup(connection, transaction, targetWorkflowUid);

        if (sourceWorkflow is null || targetWorkflow is null)
            return null;

        if (sourceWorkflow.WorkflowId == targetWorkflow.WorkflowId)
            return null;

        var linkId = await InsertWorkflowLink(
            connection,
            transaction,
            sourceWorkflow.WorkflowId,
            targetWorkflow.WorkflowId,
            request.LinkType,
            actorUserId,
            request.Notes);
        if (!linkId.HasValue)
        {
            return null;
        }

        await InsertAuditEntry(
            connection,
            transaction,
            targetWorkflow.WorkflowId,
            null,
            actorUserId,
            "workflow_linked",
            null,
            request.LinkType,
            $"Linked to workflow {request.SourceWorkflowUid} ({request.LinkType})");

        if (string.Equals(request.LinkType, "derived_from", StringComparison.OrdinalIgnoreCase))
        {
            await ApplyDerivedAnswersToWorkflow(
                connection,
                transaction,
                sourceWorkflow,
                targetWorkflow,
                actorUserId,
                regenerateTasks: true);
        }

        await transaction.CommitAsync();
        return await GetWorkflowLinkById(connection, linkId.Value, targetWorkflowUid);
    }

    public async Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM workflow_links wl
USING workflows source_w, workflows target_w
WHERE wl.id = @linkId
  AND source_w.id = wl.source_workflow_id
  AND target_w.id = wl.target_workflow_id
  AND (source_w.uid = @workflowUid OR target_w.uid = @workflowUid)
RETURNING CASE
    WHEN source_w.uid = @workflowUid THEN source_w.id
    ELSE target_w.id
END;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@linkId", linkId);
        cmd.Parameters.AddWithValue("@workflowUid", workflowUid);

        var result = await cmd.ExecuteScalarAsync();
        if (result is not long auditWorkflowId)
            return false;

        // Audit-Eintrag
        const string auditSql = @"
INSERT INTO workflow_audit_log (workflow_id, actor_user_id, event_type, detail)
VALUES (@workflowId, @actorUserId, 'workflow_unlinked', 'Workflow link removed');";

        await using var auditCmd = new NpgsqlCommand(auditSql, connection);
        auditCmd.Parameters.AddWithValue("@workflowId", auditWorkflowId);
        auditCmd.Parameters.AddWithValue("@actorUserId", actorUserId);
        await auditCmd.ExecuteNonQueryAsync();

        return true;
    }

    public async Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    w.uid,
    w.first_name,
    w.last_name,
    w.employee_number,
    d.name AS department_name,
    w.status,
    w.created_at,
    pt.key  AS pt_key,
    pt.name AS pt_name,
    pt.requires_target_person
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN process_types pt ON pt.id = w.process_type_id
WHERE w.employee_number = @employeeNumber
  AND w.workflow_definition_version_id IS NULL
  AND (@excludeUid IS NULL OR w.uid <> @excludeUid)
ORDER BY w.created_at DESC;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@employeeNumber", employeeNumber);
        command.Parameters.AddWithValue("@excludeUid", (object?)excludeWorkflowUid ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();

        var workflows = new List<LinkableWorkflowDto>();
        while (await reader.ReadAsync())
        {
            workflows.Add(new LinkableWorkflowDto
            {
                Uid = reader.GetGuid(0),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                EmployeeNumber = reader.GetInt32(3),
                DepartmentName = reader.GetString(4),
                Status = reader.GetString(5),
                WorkflowStatus = reader.GetString(5), // same DB column
                CreatedAt = reader.GetDateTime(6),
                ProcessType = new WorkflowProcessTypeDto
                {
                    Key = reader.GetString(7),
                    Name = reader.GetString(8),
                    RequiresTargetPerson = reader.GetBoolean(9)
                }
            });
        }

        return workflows;
    }

    private static async Task<WorkflowLinkLookupRecord?> LoadWorkflowLinkLookup(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workflowUid)
    {
        const string sql = @"
SELECT
    w.id,
    w.uid,
    w.process_type_id,
    pt.key,
    pt.requires_supervisor_step,
    w.department_id,
    w.position_role_id
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
WHERE w.uid = @uid
  AND w.workflow_definition_version_id IS NULL
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("uid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowLinkLookupRecord
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1),
            ProcessTypeId = reader.GetInt32(2),
            ProcessTypeKey = reader.GetString(3),
            RequiresSupervisorStep = reader.GetBoolean(4),
            DepartmentId = reader.GetInt32(5),
            RoleId = reader.GetInt32(6)
        };
    }

    private static async Task<long?> InsertWorkflowLink(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long sourceWorkflowId,
        long targetWorkflowId,
        string linkType,
        long actorUserId,
        string? notes)
    {
        const string insertSql = @"
INSERT INTO workflow_links (source_workflow_id, target_workflow_id, link_type, created_by_user_id, notes)
VALUES (@sourceId, @targetId, @linkType, @userId, @notes)
ON CONFLICT (source_workflow_id, target_workflow_id, link_type) DO NOTHING
RETURNING id;";

        await using var command = new NpgsqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("sourceId", sourceWorkflowId);
        command.Parameters.AddWithValue("targetId", targetWorkflowId);
        command.Parameters.AddWithValue("linkType", linkType);
        command.Parameters.AddWithValue("userId", actorUserId);
        command.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long linkId ? linkId : null;
    }

    private async Task<Dictionary<string, StoredWorkflowAnswerRecord>> ApplyDerivedAnswersToWorkflow(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkflowLinkLookupRecord sourceWorkflow,
        WorkflowLinkLookupRecord targetWorkflow,
        long actorUserId,
        bool regenerateTasks)
    {
        var hasExistingAnswers = await WorkflowHasExistingAnswers(connection, transaction, targetWorkflow.WorkflowId);
        if (hasExistingAnswers)
        {
            return await LoadStoredAnswersByKey(connection, transaction, targetWorkflow.WorkflowId);
        }

        var derivedAnswers = await GetDerivedAnswersInternal(
            connection,
            transaction,
            sourceWorkflow.WorkflowUid,
            targetWorkflow.ProcessTypeKey);
        if (derivedAnswers.Count == 0)
        {
            return new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction, targetWorkflow.ProcessTypeId);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, targetWorkflow.RoleId, targetWorkflow.ProcessTypeId);
        var selections = BuildDerivedRequirementSelections(derivedAnswers, answerDefinitions);
        if (selections.Count == 0)
        {
            return new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var storedAnswers = await PersistWorkflowAnswers(
            connection,
            transaction,
            targetWorkflow.WorkflowId,
            selections,
            answerDefinitions,
            roleDefaults);

        var answersByKey = storedAnswers
            .GroupBy(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (regenerateTasks && !targetWorkflow.RequiresSupervisorStep)
        {
            await DeleteWorkflowTasksForRegeneration(connection, transaction, targetWorkflow.WorkflowId);

            var generatedTaskCount = await GenerateWorkflowTasks(
                connection,
                transaction,
                targetWorkflow.WorkflowId,
                targetWorkflow.DepartmentId,
                answersByKey,
                TaskGenerationStage.Initial);

            if (generatedTaskCount > 0)
            {
                await InsertAuditEntry(
                    connection,
                    transaction,
                    targetWorkflow.WorkflowId,
                    null,
                    actorUserId,
                    "tasks_generated",
                    null,
                    null,
                    $"{generatedTaskCount} Aufgabe(n) aus abgeleiteten Antworten erstellt");
            }

            await RecalculateWorkflowTaskAvailability(connection, transaction, targetWorkflow.WorkflowId);
            await RecalculateAndPersistWorkflowStatus(connection, transaction, targetWorkflow.WorkflowId, actorUserId);
        }

        await InsertAuditEntry(
            connection,
            transaction,
            targetWorkflow.WorkflowId,
            null,
            actorUserId,
            "workflow_answers_derived",
            null,
            null,
            $"{selections.Count} Antwort(en) aus {sourceWorkflow.WorkflowUid} übernommen");

        return answersByKey;
    }

    private static List<RequirementSelectionInputDto> BuildDerivedRequirementSelections(
        IReadOnlyList<DerivedAnswerDto> derivedAnswers,
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions)
    {
        var definitionByKey = definitions.Values
            .GroupBy(definition => definition.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var selections = new List<RequirementSelectionInputDto>();
        foreach (var derivedAnswer in derivedAnswers)
        {
            if (!definitionByKey.TryGetValue(derivedAnswer.TargetAnswerKey, out var definition))
            {
                continue;
            }

            selections.Add(new RequirementSelectionInputDto
            {
                RequirementId = definition.DefinitionId,
                ValueBoolean = derivedAnswer.ValueBoolean,
                ValueText = derivedAnswer.ValueText,
                ValueNumber = derivedAnswer.ValueNumber,
                SelectedOptionId = ResolveDerivedSelectedOptionId(definition, derivedAnswer.SelectedOptionValue),
                SelectedOptionIds = null
            });
        }

        return selections;
    }

    private static int? ResolveDerivedSelectedOptionId(
        AnswerDefinitionRecord definition,
        string? selectedOptionValue)
    {
        if (string.IsNullOrWhiteSpace(selectedOptionValue))
        {
            return null;
        }

        return definition.OptionsById.Values
            .FirstOrDefault(option => string.Equals(
                option.OptionValue,
                selectedOptionValue,
                StringComparison.OrdinalIgnoreCase))
            ?.OptionId;
    }

    private static async Task<bool> WorkflowHasExistingAnswers(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_answers
    WHERE workflow_id = @workflowId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task DeleteWorkflowTasksForRegeneration(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
DELETE FROM workflow_tasks
WHERE workflow_id = @workflowId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<string, StoredWorkflowAnswerRecord>> LoadStoredAnswersByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT
    a.id,
    a.answer_definition_id,
    a.answer_key,
    a.input_type,
    a.value_boolean,
    a.value_text,
    a.value_number,
    a.selected_option_id,
    selected_option.option_value
FROM workflow_answers a
LEFT JOIN workflow_answer_options selected_option ON selected_option.id = a.selected_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, a.id;";

        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var answerKey = reader.GetString(2);
                answers[answerKey] = new StoredWorkflowAnswerRecord
                {
                    WorkflowAnswerId = reader.GetInt64(0),
                    AnswerDefinitionId = reader.GetInt32(1),
                    AnswerKey = answerKey,
                    InputType = reader.GetString(3),
                    ValueBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                    ValueText = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ValueNumber = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    SelectedOptionId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    SelectedOptionValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                    SelectedOptionIds = new List<int>(),
                    SelectedOptionValues = new List<string>()
                };
            }
        }

        const string multiSelectSql = @"
SELECT
    a.answer_key,
    o.id,
    o.option_value
FROM workflow_answers a
JOIN workflow_answer_selected_options aso ON aso.workflow_answer_id = a.id
JOIN workflow_answer_options o ON o.id = aso.answer_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, a.id, o.sort_order, o.id;";

        await using var multiSelectCommand = new NpgsqlCommand(multiSelectSql, connection, transaction);
        multiSelectCommand.Parameters.AddWithValue("workflowId", workflowId);
        await using var multiSelectReader = await multiSelectCommand.ExecuteReaderAsync();

        while (await multiSelectReader.ReadAsync())
        {
            var answerKey = multiSelectReader.GetString(0);
            if (!answers.TryGetValue(answerKey, out var answer))
            {
                continue;
            }

            answer.SelectedOptionIds.Add(multiSelectReader.GetInt32(1));
            answer.SelectedOptionValues.Add(multiSelectReader.GetString(2));
        }

        return answers;
    }

    private static async Task<List<DerivedAnswerDto>> GetDerivedAnswersInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid sourceWorkflowUid,
        string targetProcessTypeKey)
    {
        const string sql = @"
SELECT
    dr.target_answer_key,
    dr.source_answer_key,
    dr.derivation_kind,
    wa.value_boolean,
    wa.value_text,
    wa.value_number,
    selected_option.option_value
FROM workflow_answer_derivation_rules dr
JOIN process_types tpt ON tpt.id = dr.target_process_type_id AND tpt.key = @targetProcessTypeKey
JOIN workflows w ON w.uid = @sourceUid
JOIN process_types spt ON spt.id = w.process_type_id AND spt.id = dr.source_process_type_id
JOIN workflow_answers wa ON wa.workflow_id = w.id AND wa.answer_key = dr.source_answer_key
LEFT JOIN workflow_answer_options selected_option ON selected_option.id = wa.selected_option_id
WHERE dr.is_active = TRUE
ORDER BY dr.sort_order;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@sourceUid", sourceWorkflowUid);
        command.Parameters.AddWithValue("@targetProcessTypeKey", targetProcessTypeKey);
        await using var reader = await command.ExecuteReaderAsync();

        var derived = new List<DerivedAnswerDto>();
        while (await reader.ReadAsync())
        {
            var kind = reader.GetString(2);
            derived.Add(new DerivedAnswerDto
            {
                TargetAnswerKey = reader.GetString(0),
                SourceAnswerKey = reader.GetString(1),
                ValueBoolean = kind == "copy_boolean" && !reader.IsDBNull(3) ? reader.GetBoolean(3) : null,
                ValueText = kind == "copy_text" && !reader.IsDBNull(4) ? reader.GetString(4) : null,
                ValueNumber = kind == "copy_number" && !reader.IsDBNull(5) ? reader.GetDecimal(5) : null,
                SelectedOptionValue = kind == "copy_selected_option" && !reader.IsDBNull(6) ? reader.GetString(6) : null,
            });
        }

        return derived;
    }

    public async Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetProcessTypeKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await GetDerivedAnswersInternal(connection, null!, sourceWorkflowUid, targetProcessTypeKey);
    }

    private async Task<WorkflowLinkDto?> GetWorkflowLinkById(NpgsqlConnection connection, long linkId, Guid perspectiveUid)
    {
        const string sql = @"
SELECT
    wl.id,
    source_w.uid   AS source_uid,
    target_w.uid   AS target_uid,
    wl.link_type,
    wl.notes,
    wl.created_by_user_id,
    u.display_name AS created_by_user_name,
    wl.created_at,
    linked_w.uid         AS linked_uid,
    linked_w.first_name,
    linked_w.last_name,
    linked_w.status,
    linked_w.created_at  AS linked_created_at,
    pt.key               AS linked_pt_key,
    pt.name              AS linked_pt_name,
    pt.requires_target_person
FROM workflow_links wl
JOIN workflows source_w ON source_w.id = wl.source_workflow_id
JOIN workflows target_w ON target_w.id = wl.target_workflow_id
JOIN workflows linked_w ON linked_w.id = CASE
    WHEN source_w.uid = @perspectiveUid THEN target_w.id
    ELSE source_w.id
END
JOIN process_types pt ON pt.id = linked_w.process_type_id
LEFT JOIN app_users u ON u.id = wl.created_by_user_id
WHERE wl.id = @linkId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@linkId", linkId);
        command.Parameters.AddWithValue("@perspectiveUid", perspectiveUid);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new WorkflowLinkDto
        {
            Id = reader.GetInt64(0),
            SourceWorkflowUid = reader.GetGuid(1),
            TargetWorkflowUid = reader.GetGuid(2),
            LinkType = reader.GetString(3),
            Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedByUserId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            CreatedByUserName = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt = reader.GetDateTime(7),
            LinkedWorkflowFirstName = reader.GetString(9),
            LinkedWorkflowLastName = reader.GetString(10),
            LinkedWorkflowStatus = reader.GetString(11),
            LinkedWorkflowCreatedAt = reader.GetDateTime(12),
            LinkedWorkflowProcessType = new WorkflowProcessTypeDto
            {
                Key = reader.GetString(13),
                Name = reader.GetString(14),
                RequiresTargetPerson = reader.GetBoolean(15)
            }
        };
    }
}
