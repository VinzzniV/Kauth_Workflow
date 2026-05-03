using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    internal const string WorkflowDefinitionDraftStatus = "draft";
    internal const string WorkflowDefinitionPublishedStatus = "published";

    public async Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
SELECT
    d.definition_key,
    COALESCE(NULLIF(BTRIM(d.name), ''), NULLIF(BTRIM(v.name), '')) AS effective_name,
    COALESCE(NULLIF(BTRIM(d.description), ''), NULLIF(BTRIM(v.description), '')) AS effective_description,
    d.requires_target_person,
    v.version_number
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
WHERE v.status = @publishedStatus
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
                LatestPublishedVersionNumber = reader.GetInt32(4)
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
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definitions d
LEFT JOIN workflow_definition_versions v
    ON v.workflow_definition_id = d.id
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
            var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, null, version.Id, _automation, _workflowDefinitionValidationService);
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

        var definitionName = NormalizeWorkflowDefinitionRequiredText(request.Name, "Name");
        var description = NormalizeWorkflowDefinitionOptionalText(request.Description);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var definitionKey = await BuildUniqueWorkflowDefinitionKey(
            connection,
            transaction,
            request.Key,
            definitionName);

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

        await using var command = new NpgsqlCommand(sql, connection, transaction);
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

            _ = await InsertWorkflowDefinitionVersion(
                connection,
                transaction,
                definitionId,
                WorkflowDefinitionDraftStatus,
                null,
                null);

            await transaction.CommitAsync();

            return await GetAdminWorkflowDefinitionById(connection, null, definitionId)
                ?? throw new InvalidOperationException("Workflow definition could not be loaded after creation.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
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

    public async Task<bool> DeleteAdminWorkflowDefinition(int definitionId)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await WorkflowDefinitionExists(connection, transaction, definitionId))
        {
            await transaction.CommitAsync();
            return false;
        }

        if (await WorkflowDefinitionHasRuntimeReferences(connection, transaction, definitionId))
        {
            throw new InvalidOperationException(
                "Workflow definition cannot be deleted while workflow instances still reference one of its versions.");
        }

        const string sql = """
DELETE FROM workflow_definitions
WHERE id = @definitionId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        var affected = await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return affected > 0;
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

        var versionId = await InsertWorkflowDefinitionVersion(
            connection,
            null,
            definitionId,
            WorkflowDefinitionDraftStatus,
            NormalizeWorkflowDefinitionOptionalText(request.Name),
            NormalizeWorkflowDefinitionOptionalText(request.Description));

        return await GetAdminWorkflowDefinitionVersionSummaryById(connection, null, versionId);
    }

    public async Task<WorkflowDefinitionVersionDetailDto?> GetOrCreateAdminWorkflowDefinitionWorkingDraft(int definitionId)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await WorkflowDefinitionExists(connection, transaction, definitionId))
        {
            await transaction.CommitAsync();
            return null;
        }

        var existingDraftVersionId = await GetLatestWorkflowDefinitionVersionId(
            connection,
            transaction,
            definitionId,
            WorkflowDefinitionDraftStatus);

        if (existingDraftVersionId.HasValue)
        {
            await transaction.CommitAsync();
            return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, existingDraftVersionId.Value, _automation, _workflowDefinitionValidationService);
        }

        var sourceVersionId = await GetLatestWorkflowDefinitionVersionId(connection, transaction, definitionId);
        if (!sourceVersionId.HasValue)
        {
            var emptyDraftVersionId = await InsertWorkflowDefinitionVersion(
                connection,
                transaction,
                definitionId,
                WorkflowDefinitionDraftStatus,
                null,
                null);

            await transaction.CommitAsync();
            return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, emptyDraftVersionId, _automation, _workflowDefinitionValidationService);
        }

        var sourceVersion = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, sourceVersionId.Value, _automation, _workflowDefinitionValidationService)
            ?? throw new InvalidOperationException("The source workflow version could not be loaded.");
        var clonedVersionId = await InsertWorkflowDefinitionVersion(
            connection,
            transaction,
            definitionId,
            WorkflowDefinitionDraftStatus,
            sourceVersion.Name,
            sourceVersion.Description);

        await PersistWorkflowDefinitionVersionGraph(
            connection,
            transaction,
            clonedVersionId,
            sourceVersion.Nodes.Select(ToDraftNode).ToList(),
            sourceVersion.Edges.Select(ToDraftEdge).ToList());

        await transaction.CommitAsync();
        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, clonedVersionId, _automation, _workflowDefinitionValidationService);
    }

    public async Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId)
    {
        if (versionId <= 0)
        {
            throw new InvalidOperationException("versionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId, _automation, _workflowDefinitionValidationService);
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
    updated_at = NOW()
WHERE id = @versionId;
""";

        await using (var updateCommand = new NpgsqlCommand(updateVersionSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("versionId", versionId);
            updateCommand.Parameters.AddWithValue("name", (object?)validatedDraft.Name ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("description", (object?)validatedDraft.Description ?? DBNull.Value);
            await updateCommand.ExecuteNonQueryAsync();
        }

        await PersistWorkflowDefinitionVersionGraph(
            connection,
            transaction,
            versionId,
            validatedDraft.Nodes,
            validatedDraft.Edges);

        await transaction.CommitAsync();

        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId, _automation, _workflowDefinitionValidationService);
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
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definitions d
LEFT JOIN workflow_definition_versions v
    ON v.workflow_definition_id = d.id
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
                var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, version.Id, _automation, _workflowDefinitionValidationService);
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
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definition_versions v
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
        var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, summary.Id, _automation, _workflowDefinitionValidationService);
        if (detail is not null)
        {
            summary.CanPublish = detail.CanPublish;
            summary.ValidationIssues = detail.ValidationIssues;
        }

        return summary;
    }

    internal static async Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersionDetailById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long versionId,
        IWorkflowAutomationOperations automation,
        IWorkflowDefinitionValidationService validationService)
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
    v.created_at,
    v.updated_at,
    v.published_at
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
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
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10),
                PublishedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
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
                    Config = reader.IsDBNull(6) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(6)),
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
                    InputMapping = reader.IsDBNull(2) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(2)),
                    ExecutionOrder = reader.GetInt32(3),
                    OnErrorBehavior = reader.GetString(4)
                });
            }
        }
        catch (PostgresException ex) when (PostgresWorkflowAutomationOperations.IsMissingWorkflowBuilderAutomationSchema(ex))
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

        var validationSnapshot = await BuildWorkflowDefinitionValidationSnapshot(connection, transaction, detail, automation, validationService);
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
            CreatedAt = reader.GetDateTime(startOrdinal + 6),
            UpdatedAt = reader.GetDateTime(startOrdinal + 7),
            PublishedAt = reader.IsDBNull(startOrdinal + 8) ? null : reader.GetDateTime(startOrdinal + 8),
            CanPublish = false,
            ValidationIssues = new List<ValidationIssueDto>()
        };
    }

    private static async Task<bool> LegacyProcessTypeExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string processTypeKey,
        bool requireActive)
    {
        _ = requireActive;
        const string sql = """
SELECT 1
FROM workflow_definitions
WHERE definition_key = @key
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", processTypeKey.Trim().ToLowerInvariant());
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> LoadDefinitionRequiresSupervisorStep(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string workflowDefinitionKey)
    {
        const string sql = """
SELECT requires_supervisor_step
FROM workflow_definitions
WHERE definition_key = @key
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", workflowDefinitionKey.Trim().ToLowerInvariant());
        var scalar = await command.ExecuteScalarAsync();
        return scalar is bool requiresSupervisorStep && requiresSupervisorStep;
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

    private static async Task<bool> WorkflowDefinitionHasRuntimeReferences(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId)
    {
        const string sql = """
SELECT 1
FROM workflows w
INNER JOIN workflow_definition_versions v
    ON v.id = w.workflow_definition_version_id
WHERE v.workflow_definition_id = @definitionId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        return await command.ExecuteScalarAsync() is not null;
    }

    internal static async Task<WorkflowDefinitionVersionRecord?> GetWorkflowDefinitionVersionRecord(
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

    private static async Task<long?> GetLatestWorkflowDefinitionVersionId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId,
        string? status = null)
    {
        const string sql = """
SELECT id
FROM workflow_definition_versions
WHERE workflow_definition_id = @definitionId
  AND LOWER(status) = LOWER(@status)
ORDER BY version_number DESC, id DESC
LIMIT 1;
""";

        const string sqlWithoutStatus = """
SELECT id
FROM workflow_definition_versions
WHERE workflow_definition_id = @definitionId
ORDER BY version_number DESC, id DESC
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(status is null ? sqlWithoutStatus : sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        if (status is not null)
        {
            command.Parameters.AddWithValue("status", status);
        }

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long versionId ? versionId : null;
    }

    // ToDraftNode/ToDraftEdge/PersistWorkflowDefinitionVersionGraph/HasWorkflowNodePositionColumns
    // wurden in WorkflowDefinitionGraphMappingOperations.cs ausgelagert (HQ3-Z3, 2026-05-02).
    // Sie werden weiterhin von dieser Datei (GetOrCreateAdminWorkflowDefinitionWorkingDraft,
    // ReplaceAdminWorkflowDefinitionVersion, GetAdminWorkflowDefinitionVersionDetailById)
    // ueber die partial-class-Mitgliedschaft aufgerufen.


    private async Task<long> InsertWorkflowDefinitionVersion(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId,
        string status,
        string? name,
        string? description)
    {
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

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long versionId)
        {
            throw new InvalidOperationException("Workflow definition version could not be created.");
        }

        return versionId;
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

    private async Task<string> BuildUniqueWorkflowDefinitionKey(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? requestedKey,
        string definitionName)
    {
        var baseKey = !string.IsNullOrWhiteSpace(requestedKey)
            ? _workflowDefinitionValidationService.NormalizeDefinitionKey(requestedKey)
            : SlugifyWorkflowDefinitionKey(definitionName);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = $"workflow_{Guid.NewGuid():N}"[..17];
        }

        var candidate = baseKey;
        var suffix = 2;
        while (await WorkflowDefinitionKeyExists(connection, transaction, candidate))
        {
            candidate = $"{baseKey}_{suffix}";
            suffix += 1;
        }

        return candidate;
    }

    private static string SlugifyWorkflowDefinitionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "workflow";
        }

        var normalized = value
            .Trim()
            .Replace("Ä", "Ae", StringComparison.Ordinal)
            .Replace("Ö", "Oe", StringComparison.Ordinal)
            .Replace("Ü", "Ue", StringComparison.Ordinal)
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

        var builder = new List<char>(normalized.Length);
        var lastWasSeparator = false;
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Add(char.ToLowerInvariant(character));
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            builder.Add('_');
            lastWasSeparator = true;
        }

        var slug = new string(builder.ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "workflow" : slug;
    }

    private static async Task<bool> WorkflowDefinitionKeyExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string definitionKey)
    {
        const string sql = """
SELECT 1
FROM workflow_definitions
WHERE definition_key = @definitionKey
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionKey", definitionKey);
        return await command.ExecuteScalarAsync() is not null;
    }

    internal sealed class WorkflowDefinitionVersionRecord
    {
        public required long Id { get; init; }
        public required int WorkflowDefinitionId { get; init; }
        public required string Status { get; init; }
    }

    // Naming-Hinweis: Methode heißt aus Legacy-Gründen weiter Resolve...LegacyProcessTypeId,
    // liefert seit Slice 6.3d-ii aber die `workflow_definitions.id`. Aufrufer in
    // Stammdaten-Pfaden (Answer-Definitions, Task-Templates etc.) erwarten seit
    // 6.3d-ii diese ID, weil die FKs umgehängt sind.
    internal static async Task<int?> ResolveWorkflowDefinitionLegacyProcessTypeId(
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
FROM workflow_definitions
WHERE definition_key = @processTypeKey
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
