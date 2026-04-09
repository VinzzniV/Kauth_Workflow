using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private const string WorkflowDefinitionDraftStatus = "draft";
    private const string WorkflowDefinitionPublishedStatus = "published";

    public async Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
SELECT
    d.definition_key,
    COALESCE(NULLIF(BTRIM(d.name), ''), NULLIF(BTRIM(v.name), '')) AS effective_name,
    COALESCE(NULLIF(BTRIM(d.description), ''), NULLIF(BTRIM(v.description), '')) AS effective_description,
    pt.requires_target_person,
    pt.key AS primary_legacy_process_type_key,
    v.version_number
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
INNER JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
WHERE v.status = @publishedStatus
  AND pt.is_active = TRUE
ORDER BY effective_name, d.definition_key, v.version_number DESC;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("publishedStatus", WorkflowDefinitionPublishedStatus);
        await using var reader = await command.ExecuteReaderAsync();

        var definitions = new List<WorkflowStartableDefinitionDto>();
        while (await reader.ReadAsync())
        {
            definitions.Add(new WorkflowStartableDefinitionDto
            {
                DefinitionKey = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                RequiresTargetPerson = reader.GetBoolean(3),
                PrimaryLegacyProcessTypeKey = reader.GetString(4),
                LatestPublishedVersionNumber = reader.GetInt32(5)
            });
        }

        return definitions;
    }

    public async Task<List<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
SELECT
    d.id,
    d.definition_key,
    d.name,
    d.description,
    v.id,
    v.workflow_definition_id,
    v.version_number,
    v.status,
    v.name,
    v.description,
    pt.key,
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definitions d
LEFT JOIN workflow_definition_versions v
    ON v.workflow_definition_id = d.id
LEFT JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
ORDER BY d.definition_key, v.version_number DESC, v.id DESC;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var definitions = new List<WorkflowDefinitionSummaryDto>();
        var definitionById = new Dictionary<int, WorkflowDefinitionSummaryDto>();

        while (await reader.ReadAsync())
        {
            var definitionId = reader.GetInt32(0);
            if (!definitionById.TryGetValue(definitionId, out var definition))
            {
                definition = new WorkflowDefinitionSummaryDto
                {
                    Id = definitionId,
                    Key = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Versions = new List<WorkflowDefinitionVersionSummaryDto>()
                };

                definitionById.Add(definitionId, definition);
                definitions.Add(definition);
            }

            if (!reader.IsDBNull(4))
            {
                definition.Versions.Add(MapWorkflowDefinitionVersionSummary(reader, 4));
            }
        }

        await reader.CloseAsync();
        foreach (var version in definitions.SelectMany(definition => definition.Versions))
        {
            var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, null, version.Id);
            if (detail is null)
            {
                continue;
            }

            version.CanPublish = detail.CanPublish;
            version.ValidationIssues = detail.ValidationIssues;
        }

        return definitions;
    }

    public async Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definitionKey = _workflowDefinitionValidationService.NormalizeDefinitionKey(request.Key);
        var definitionName = NormalizeWorkflowDefinitionRequiredText(request.Name, "Name");
        var description = NormalizeWorkflowDefinitionOptionalText(request.Description);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
INSERT INTO workflow_definitions (
    definition_key,
    name,
    description,
    updated_at
)
VALUES (
    @definitionKey,
    @name,
    @description,
    NOW()
)
RETURNING id;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("definitionKey", definitionKey);
        command.Parameters.AddWithValue("name", definitionName);
        command.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);

        try
        {
            var createdId = await command.ExecuteScalarAsync();
            if (createdId is not int definitionId)
            {
                throw new InvalidOperationException("Workflow definition could not be created.");
            }

            return await GetAdminWorkflowDefinitionById(connection, null, definitionId)
                ?? throw new InvalidOperationException("Workflow definition could not be loaded after creation.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException(
                $"Workflow definition key '{definitionKey}' already exists.",
                ex);
        }
    }

    public async Task<WorkflowDefinitionSummaryDto?> UpdateAdminWorkflowDefinition(
        int definitionId,
        UpdateWorkflowDefinitionRequest request)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(request);

        var definitionName = NormalizeWorkflowDefinitionRequiredText(request.Name, "Name");
        var description = NormalizeWorkflowDefinitionOptionalText(request.Description);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        if (!await WorkflowDefinitionExists(connection, null, definitionId))
        {
            return null;
        }

        const string sql = """
UPDATE workflow_definitions
SET
    name = @name,
    description = @description,
    updated_at = NOW()
WHERE id = @definitionId;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("definitionId", definitionId);
        command.Parameters.AddWithValue("name", definitionName);
        command.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();

        return await GetAdminWorkflowDefinitionById(connection, null, definitionId);
    }

    public async Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(
        int definitionId,
        CreateWorkflowDefinitionVersionRequest request)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        if (!await WorkflowDefinitionExists(connection, null, definitionId))
        {
            return null;
        }

        const string sql = """
INSERT INTO workflow_definition_versions (
    workflow_definition_id,
    version_number,
    status,
    name,
    description,
    updated_at
)
SELECT
    @definitionId,
    COALESCE(MAX(version_number), 0) + 1,
    @status,
    @name,
    @description,
    NOW()
FROM workflow_definition_versions
WHERE workflow_definition_id = @definitionId
RETURNING id;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("definitionId", definitionId);
        command.Parameters.AddWithValue("status", WorkflowDefinitionDraftStatus);
        command.Parameters.AddWithValue("name", (object?)NormalizeWorkflowDefinitionOptionalText(request.Name) ?? DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)NormalizeWorkflowDefinitionOptionalText(request.Description) ?? DBNull.Value);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long versionId)
        {
            throw new InvalidOperationException("Workflow definition version could not be created.");
        }

        return await GetAdminWorkflowDefinitionVersionSummaryById(connection, null, versionId);
    }

    public async Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId)
    {
        if (versionId <= 0)
        {
            throw new InvalidOperationException("versionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId);
    }

    public async Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(
        long versionId,
        ReplaceWorkflowDefinitionVersionRequest request)
    {
        if (versionId <= 0)
        {
            throw new InvalidOperationException("versionId must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(request);

        var validatedDraft = _workflowDefinitionValidationService.ValidateAndNormalize(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var primaryLegacyProcessTypeId = await ResolveWorkflowDefinitionLegacyProcessTypeId(
            connection,
            transaction,
            request.PrimaryLegacyProcessTypeKey,
            requireActive: false);

        var versionRecord = await GetWorkflowDefinitionVersionRecord(connection, transaction, versionId);
        if (versionRecord is null)
        {
            return null;
        }

        if (!string.Equals(versionRecord.Status, WorkflowDefinitionDraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workflow definition version '{versionId}' is not editable because it is in status '{versionRecord.Status}'.");
        }

        const string updateVersionSql = """
UPDATE workflow_definition_versions
SET
    name = @name,
    description = @description,
    primary_legacy_process_type_id = @primaryLegacyProcessTypeId,
    updated_at = NOW()
WHERE id = @versionId;
""";

        await using (var updateCommand = new NpgsqlCommand(updateVersionSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("versionId", versionId);
            updateCommand.Parameters.AddWithValue("name", (object?)validatedDraft.Name ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("description", (object?)validatedDraft.Description ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("primaryLegacyProcessTypeId", (object?)primaryLegacyProcessTypeId ?? DBNull.Value);
            await updateCommand.ExecuteNonQueryAsync();
        }

        const string clearSql = """
DELETE FROM workflow_edges
WHERE workflow_definition_version_id = @versionId;

DELETE FROM workflow_node_configs
WHERE workflow_node_id IN (
    SELECT id
    FROM workflow_nodes
    WHERE workflow_definition_version_id = @versionId
);

DELETE FROM workflow_nodes
WHERE workflow_definition_version_id = @versionId;
""";

        await using (var clearCommand = new NpgsqlCommand(clearSql, connection, transaction))
        {
            clearCommand.Parameters.AddWithValue("versionId", versionId);
            await clearCommand.ExecuteNonQueryAsync();
        }

        var nodeIdByKey = new Dictionary<string, long>(StringComparer.Ordinal);
        const string insertNodeSql = """
INSERT INTO workflow_nodes (
    workflow_definition_version_id,
    node_key,
    node_type,
    title,
    sort_order,
    position_x,
    position_y
)
VALUES (
    @versionId,
    @nodeKey,
    @nodeType,
    @title,
    @sortOrder,
    @positionX,
    @positionY
)
RETURNING id;
""";

        const string insertConfigSql = """
INSERT INTO workflow_node_configs (
    workflow_node_id,
    config_json
)
VALUES (
    @workflowNodeId,
    @configJson
);
""";

        const string insertNodeActionSql = """
INSERT INTO workflow_node_actions (
    workflow_node_id,
    action_definition_id,
    input_mapping_json,
    execution_order,
    on_error_behavior
)
VALUES (
    @workflowNodeId,
    @actionDefinitionId,
    @inputMappingJson,
    @executionOrder,
    @onErrorBehavior
);
""";

        foreach (var node in validatedDraft.Nodes)
        {
            await using var insertNodeCommand = new NpgsqlCommand(insertNodeSql, connection, transaction);
            insertNodeCommand.Parameters.AddWithValue("versionId", versionId);
            insertNodeCommand.Parameters.AddWithValue("nodeKey", node.NodeKey);
            insertNodeCommand.Parameters.AddWithValue("nodeType", node.NodeType);
            insertNodeCommand.Parameters.AddWithValue("title", (object?)node.Title ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("sortOrder", node.SortOrder);
            insertNodeCommand.Parameters.AddWithValue("positionX", (object?)node.PositionX ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("positionY", (object?)node.PositionY ?? DBNull.Value);

            var createdNodeId = await insertNodeCommand.ExecuteScalarAsync();
            if (createdNodeId is not long workflowNodeId)
            {
                throw new InvalidOperationException($"Workflow node '{node.NodeKey}' could not be created.");
            }

            nodeIdByKey.Add(node.NodeKey, workflowNodeId);

            if (node.Config.HasValue)
            {
                await using var insertConfigCommand = new NpgsqlCommand(insertConfigSql, connection, transaction);
                insertConfigCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                insertConfigCommand.Parameters.Add(
                    new NpgsqlParameter("configJson", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(node.Config.Value)
                    });
                await insertConfigCommand.ExecuteNonQueryAsync();
            }

            foreach (var action in node.Actions)
            {
                var actionDefinitionId = await ResolveActionDefinitionIdByKey(
                    connection,
                    transaction,
                    action.ActionKey!,
                    requireActive: false);

                await using var insertActionCommand = new NpgsqlCommand(insertNodeActionSql, connection, transaction);
                insertActionCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                insertActionCommand.Parameters.AddWithValue("actionDefinitionId", actionDefinitionId);
                insertActionCommand.Parameters.Add(
                    new NpgsqlParameter("inputMappingJson", NpgsqlDbType.Jsonb)
                    {
                        Value = action.InputMapping.HasValue ? JsonSerializer.Serialize(action.InputMapping.Value) : DBNull.Value
                    });
                insertActionCommand.Parameters.AddWithValue("executionOrder", action.ExecutionOrder);
                insertActionCommand.Parameters.AddWithValue("onErrorBehavior", action.OnErrorBehavior);
                await insertActionCommand.ExecuteNonQueryAsync();
            }
        }

        const string insertEdgeSql = """
INSERT INTO workflow_edges (
    workflow_definition_version_id,
    source_workflow_node_id,
    target_workflow_node_id,
    priority,
    condition_expression
)
VALUES (
    @versionId,
    @sourceNodeId,
    @targetNodeId,
    @priority,
    @conditionExpression
);
""";

        foreach (var edge in validatedDraft.Edges)
        {
            await using var insertEdgeCommand = new NpgsqlCommand(insertEdgeSql, connection, transaction);
            insertEdgeCommand.Parameters.AddWithValue("versionId", versionId);
            insertEdgeCommand.Parameters.AddWithValue("sourceNodeId", nodeIdByKey[edge.SourceNodeKey]);
            insertEdgeCommand.Parameters.AddWithValue("targetNodeId", nodeIdByKey[edge.TargetNodeKey]);
            insertEdgeCommand.Parameters.AddWithValue("priority", edge.Priority);
            insertEdgeCommand.Parameters.AddWithValue("conditionExpression", (object?)edge.ConditionExpression ?? DBNull.Value);
            await insertEdgeCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId);
    }

    private async Task<WorkflowDefinitionSummaryDto?> GetAdminWorkflowDefinitionById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId)
    {
        const string sql = """
SELECT
    d.id,
    d.definition_key,
    d.name,
    d.description,
    v.id,
    v.workflow_definition_id,
    v.version_number,
    v.status,
    v.name,
    v.description,
    pt.key,
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definitions d
LEFT JOIN workflow_definition_versions v
    ON v.workflow_definition_id = d.id
LEFT JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
WHERE d.id = @definitionId
ORDER BY v.version_number DESC, v.id DESC;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        await using var reader = await command.ExecuteReaderAsync();

        WorkflowDefinitionSummaryDto? definition = null;
        while (await reader.ReadAsync())
        {
            definition ??= new WorkflowDefinitionSummaryDto
            {
                Id = reader.GetInt32(0),
                Key = reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Versions = new List<WorkflowDefinitionVersionSummaryDto>()
            };

            if (!reader.IsDBNull(4))
            {
                definition.Versions.Add(MapWorkflowDefinitionVersionSummary(reader, 4));
            }
        }

        await reader.CloseAsync();
        if (definition is not null)
        {
            foreach (var version in definition.Versions)
            {
                var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, version.Id);
                if (detail is null)
                {
                    continue;
                }

                version.CanPublish = detail.CanPublish;
                version.ValidationIssues = detail.ValidationIssues;
            }
        }

        return definition;
    }

    private async Task<WorkflowDefinitionVersionSummaryDto?> GetAdminWorkflowDefinitionVersionSummaryById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long versionId)
    {
        const string sql = """
SELECT
    v.id,
    v.workflow_definition_id,
    v.version_number,
    v.status,
    v.name,
    v.description,
    pt.key,
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definition_versions v
LEFT JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
WHERE v.id = @versionId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("versionId", versionId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        var summary = MapWorkflowDefinitionVersionSummary(reader, 0);
        await reader.CloseAsync();
        var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, summary.Id);
        if (detail is not null)
        {
            summary.CanPublish = detail.CanPublish;
            summary.ValidationIssues = detail.ValidationIssues;
        }

        return summary;
    }

    private async Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersionDetailById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long versionId)
    {
        var hasBuilderPositionColumns = await HasWorkflowNodePositionColumns(connection, transaction);
        const string headerSql = """
SELECT
    v.id,
    v.workflow_definition_id,
    d.definition_key,
    d.name,
    d.description,
    v.version_number,
    v.status,
    v.name,
    v.description,
    pt.key,
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
LEFT JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
WHERE v.id = @versionId;
""";

        WorkflowDefinitionVersionDetailDto? detail;
        await using (var headerCommand = new NpgsqlCommand(headerSql, connection, transaction))
        {
            headerCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await headerCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            detail = new WorkflowDefinitionVersionDetailDto
            {
                Id = reader.GetInt64(0),
                WorkflowDefinitionId = reader.GetInt32(1),
                DefinitionKey = reader.GetString(2),
                DefinitionName = reader.GetString(3),
                DefinitionDescription = reader.IsDBNull(4) ? null : reader.GetString(4),
                VersionNumber = reader.GetInt32(5),
                Status = reader.GetString(6),
                Name = reader.IsDBNull(7) ? null : reader.GetString(7),
                Description = reader.IsDBNull(8) ? null : reader.GetString(8),
                PrimaryLegacyProcessTypeKey = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                UpdatedAt = reader.GetDateTime(11),
                PublishedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                CanPublish = false,
                ValidationIssues = new List<ValidationIssueDto>(),
                Nodes = new List<WorkflowDefinitionNodeDto>(),
                Edges = new List<WorkflowDefinitionEdgeDto>()
            };
        }

        var nodeSql = hasBuilderPositionColumns
            ? """
SELECT
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    n.position_x,
    n.position_y,
    nc.config_json::text
FROM workflow_nodes n
LEFT JOIN workflow_node_configs nc
    ON nc.workflow_node_id = n.id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY n.sort_order, n.node_key, n.id;
"""
            : """
SELECT
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    NULL::integer AS position_x,
    NULL::integer AS position_y,
    nc.config_json::text
FROM workflow_nodes n
LEFT JOIN workflow_node_configs nc
    ON nc.workflow_node_id = n.id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY n.sort_order, n.node_key, n.id;
""";

        await using (var nodeCommand = new NpgsqlCommand(nodeSql, connection, transaction))
        {
            nodeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await nodeCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Nodes.Add(new WorkflowDefinitionNodeDto
                {
                    NodeKey = reader.GetString(0),
                    NodeType = reader.GetString(1),
                    Title = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    PositionX = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    PositionY = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Config = reader.IsDBNull(6) ? null : ParseJsonElement(reader.GetString(6)),
                    Actions = new List<WorkflowNodeActionDto>()
                });
            }
        }

        const string actionSql = """
SELECT
    n.node_key,
    ad.action_key,
    wna.input_mapping_json::text,
    wna.execution_order,
    wna.on_error_behavior
FROM workflow_node_actions wna
INNER JOIN workflow_nodes n
    ON n.id = wna.workflow_node_id
INNER JOIN action_definitions ad
    ON ad.id = wna.action_definition_id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY n.sort_order, n.node_key, wna.execution_order, wna.id;
""";

        try
        {
            await using var actionCommand = new NpgsqlCommand(actionSql, connection, transaction);
            actionCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await actionCommand.ExecuteReaderAsync();

            var nodeByKey = detail.Nodes.ToDictionary(node => node.NodeKey ?? string.Empty, StringComparer.Ordinal);
            while (await reader.ReadAsync())
            {
                var nodeKey = reader.GetString(0);
                if (!nodeByKey.TryGetValue(nodeKey, out var node))
                {
                    continue;
                }

                node.Actions.Add(new WorkflowNodeActionDto
                {
                    ActionKey = reader.GetString(1),
                    InputMapping = reader.IsDBNull(2) ? null : ParseJsonElement(reader.GetString(2)),
                    ExecutionOrder = reader.GetInt32(3),
                    OnErrorBehavior = reader.GetString(4)
                });
            }
        }
        catch (PostgresException ex) when (IsMissingWorkflowBuilderAutomationSchema(ex))
        {
            // Old local schemas may not have the automation tables yet.
        }

        const string edgeSql = """
SELECT
    source_node.node_key,
    target_node.node_key,
    e.priority,
    e.condition_expression
FROM workflow_edges e
INNER JOIN workflow_nodes source_node
    ON source_node.id = e.source_workflow_node_id
INNER JOIN workflow_nodes target_node
    ON target_node.id = e.target_workflow_node_id
WHERE e.workflow_definition_version_id = @versionId
ORDER BY source_node.node_key, e.priority, target_node.node_key, e.id;
""";

        await using (var edgeCommand = new NpgsqlCommand(edgeSql, connection, transaction))
        {
            edgeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await edgeCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Edges.Add(new WorkflowDefinitionEdgeDto
                {
                    SourceNodeKey = reader.GetString(0),
                    TargetNodeKey = reader.GetString(1),
                    Priority = reader.GetInt32(2),
                    ConditionExpression = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }
        }

        var validationSnapshot = await BuildWorkflowDefinitionValidationSnapshot(connection, transaction, detail);
        detail.CanPublish = validationSnapshot.CanPublish;
        detail.ValidationIssues = MapValidationIssues(validationSnapshot.Issues);
        return detail;
    }

    private static WorkflowDefinitionVersionSummaryDto MapWorkflowDefinitionVersionSummary(
        NpgsqlDataReader reader,
        int startOrdinal)
    {
        return new WorkflowDefinitionVersionSummaryDto
        {
            Id = reader.GetInt64(startOrdinal),
            WorkflowDefinitionId = reader.GetInt32(startOrdinal + 1),
            VersionNumber = reader.GetInt32(startOrdinal + 2),
            Status = reader.GetString(startOrdinal + 3),
            Name = reader.IsDBNull(startOrdinal + 4) ? null : reader.GetString(startOrdinal + 4),
            Description = reader.IsDBNull(startOrdinal + 5) ? null : reader.GetString(startOrdinal + 5),
            PrimaryLegacyProcessTypeKey = reader.IsDBNull(startOrdinal + 6) ? null : reader.GetString(startOrdinal + 6),
            CreatedAt = reader.GetDateTime(startOrdinal + 7),
            UpdatedAt = reader.GetDateTime(startOrdinal + 8),
            PublishedAt = reader.IsDBNull(startOrdinal + 9) ? null : reader.GetDateTime(startOrdinal + 9),
            CanPublish = false,
            ValidationIssues = new List<ValidationIssueDto>()
        };
    }

    private async Task<WorkflowDefinitionValidationSnapshot> BuildWorkflowDefinitionValidationSnapshot(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        WorkflowDefinitionVersionDetailDto detail)
    {
        var referenceIssues = new List<WorkflowDefinitionValidationIssue>();
        var requiresSupervisorStep = false;

        if (string.IsNullOrWhiteSpace(detail.PrimaryLegacyProcessTypeKey))
        {
            referenceIssues.Add(CreateDefinitionReferenceIssue(
                "missing_primary_legacy_process_type",
                $"Workflow definition '{detail.DefinitionKey}' version {detail.VersionNumber} requires a primaryLegacyProcessTypeKey before it can be published.",
                "workflow_definition_version",
                detail.DefinitionKey));
        }
        else if (!await LegacyProcessTypeExists(connection, transaction, detail.PrimaryLegacyProcessTypeKey, requireActive: true))
        {
            referenceIssues.Add(CreateDefinitionReferenceIssue(
                "unknown_primary_legacy_process_type",
                $"Workflow definition '{detail.DefinitionKey}' version {detail.VersionNumber} references unknown or inactive primaryLegacyProcessTypeKey '{detail.PrimaryLegacyProcessTypeKey}'.",
                "workflow_definition_version",
                detail.PrimaryLegacyProcessTypeKey));
        }
        else
        {
            requiresSupervisorStep = await LoadLegacyProcessTypeRequiresSupervisorStep(
                connection,
                transaction,
                detail.PrimaryLegacyProcessTypeKey);
        }

        foreach (var node in detail.Nodes)
        {
            if (string.Equals(node.NodeType, "form", StringComparison.OrdinalIgnoreCase))
            {
                var processTypeKey = TryGetNodeConfigValue(node, "legacyProcessTypeKey");
                if (!string.IsNullOrWhiteSpace(processTypeKey)
                    && !await LegacyProcessTypeExists(connection, transaction, processTypeKey, requireActive: true))
                {
                    referenceIssues.Add(CreateDefinitionReferenceIssue(
                        "unknown_form_legacy_process_type",
                        $"Node '{node.NodeKey}' references unknown or inactive legacyProcessTypeKey '{processTypeKey}'.",
                        "workflow_node",
                        node.NodeKey));
                }
            }

            if (string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
            {
                var templateKey = TryGetNodeConfigValue(node, "legacyTemplateKey");
                if (!string.IsNullOrWhiteSpace(templateKey)
                    && !await LegacyTaskTemplateExists(connection, transaction, templateKey, requireActive: true))
                {
                    referenceIssues.Add(CreateDefinitionReferenceIssue(
                        "unknown_legacy_template",
                        $"Node '{node.NodeKey}' references unknown or inactive legacyTemplateKey '{templateKey}'.",
                        "workflow_node",
                        node.NodeKey));
                }
            }

            if (string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var action in node.Actions)
                {
                    if (string.IsNullOrWhiteSpace(action.ActionKey))
                    {
                        continue;
                    }

                    if (!await ActionDefinitionExists(connection, transaction, action.ActionKey, requireActive: true))
                    {
                        referenceIssues.Add(CreateDefinitionReferenceIssue(
                            "unknown_action_definition",
                            $"Node '{node.NodeKey}' references unknown or inactive action '{action.ActionKey}'.",
                            "workflow_node",
                            node.NodeKey));
                    }
                }
            }
        }

        return _workflowDefinitionValidationService.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes = detail.Nodes,
            Edges = detail.Edges,
            PrimaryLegacyProcessTypeKey = detail.PrimaryLegacyProcessTypeKey,
            RequiresSupervisorStep = requiresSupervisorStep,
            ReferenceIssues = referenceIssues
        });
    }

    private static List<ValidationIssueDto> MapValidationIssues(IEnumerable<WorkflowDefinitionValidationIssue> issues)
    {
        return issues
            .Select(issue => new ValidationIssueDto
            {
                Code = issue.Code,
                Severity = issue.Severity,
                Scope = issue.Scope,
                Message = issue.Message,
                ReferenceKey = issue.ReferenceKey
            })
            .ToList();
    }

    private static WorkflowDefinitionValidationIssue CreateDefinitionReferenceIssue(
        string code,
        string message,
        string scope,
        string? referenceKey = null)
    {
        return new WorkflowDefinitionValidationIssue
        {
            Code = code,
            Severity = "error",
            Scope = scope,
            Message = message,
            ReferenceKey = referenceKey
        };
    }

    private static string? TryGetNodeConfigValue(WorkflowDefinitionNodeDto node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return null;
        }

        return property.GetString()!.Trim().ToLowerInvariant();
    }

    private static async Task<bool> LegacyProcessTypeExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string processTypeKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM process_types
WHERE key = @key
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", processTypeKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("requireActive", requireActive);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> LoadLegacyProcessTypeRequiresSupervisorStep(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string processTypeKey)
    {
        const string sql = """
SELECT requires_supervisor_step
FROM process_types
WHERE key = @key
  AND is_active = TRUE
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", processTypeKey.Trim().ToLowerInvariant());
        var scalar = await command.ExecuteScalarAsync();
        return scalar is bool requiresSupervisorStep && requiresSupervisorStep;
    }

    private static async Task<bool> LegacyTaskTemplateExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string templateKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM task_templates
WHERE template_key = @templateKey
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateKey", templateKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("requireActive", requireActive);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> WorkflowDefinitionExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId)
    {
        const string sql = """
SELECT 1
FROM workflow_definitions
WHERE id = @definitionId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<WorkflowDefinitionVersionRecord?> GetWorkflowDefinitionVersionRecord(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long versionId)
    {
        const string sql = """
SELECT id, workflow_definition_id, status
FROM workflow_definition_versions
WHERE id = @versionId
FOR UPDATE;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("versionId", versionId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowDefinitionVersionRecord
        {
            Id = reader.GetInt64(0),
            WorkflowDefinitionId = reader.GetInt32(1),
            Status = reader.GetString(2)
        };
    }

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<bool> HasWorkflowNodePositionColumns(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = """
SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = current_schema()
  AND table_name = 'workflow_nodes'
  AND column_name IN ('position_x', 'position_y');
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is long count && count >= 2;
    }

    private static string NormalizeWorkflowDefinitionRequiredText(string? value, string fieldName)
    {
        var normalized = NormalizeWorkflowDefinitionOptionalText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string? NormalizeWorkflowDefinitionOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class WorkflowDefinitionVersionRecord
    {
        public required long Id { get; init; }
        public required int WorkflowDefinitionId { get; init; }
        public required string Status { get; init; }
    }

    private static async Task<int?> ResolveWorkflowDefinitionLegacyProcessTypeId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? primaryLegacyProcessTypeKey,
        bool requireActive)
    {
        if (string.IsNullOrWhiteSpace(primaryLegacyProcessTypeKey))
        {
            return null;
        }

        const string sql = """
SELECT id
FROM process_types
WHERE key = @processTypeKey
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", primaryLegacyProcessTypeKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("requireActive", requireActive);
        var processTypeId = await command.ExecuteScalarAsync();
        if (processTypeId is int resolvedId)
        {
            return resolvedId;
        }

        throw new InvalidOperationException(
            requireActive
                ? $"Active legacy process type '{primaryLegacyProcessTypeKey}' was not found."
                : $"Legacy process type '{primaryLegacyProcessTypeKey}' was not found.");
    }
}
