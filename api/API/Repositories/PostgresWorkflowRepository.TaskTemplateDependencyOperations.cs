using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static readonly HashSet<string> AllowedAdminTaskTemplateDependencyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done"
    };

    public async Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int workflowDefinitionId)
    {
        if (workflowDefinitionId <= 0)
        {
            throw new InvalidOperationException("workflowDefinitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await EnsureProcessTypeExists(connection, null, workflowDefinitionId);
        var measureNodeId = await ResolveMeasureNodeIdForDefinition(connection, null, workflowDefinitionId);
        if (measureNodeId is null)
        {
            return new AdminDependencyGraphDto
            {
                Nodes = new List<AdminDependencyGraphNodeDto>(),
                Edges = new List<AdminDependencyGraphEdgeDto>()
            };
        }

        const string nodesSql = @"
SELECT
    id,
    title,
    category
FROM workflow_node_task_specs
WHERE workflow_node_id = @measureNodeId
ORDER BY sort_order, title, id;";

        var nodes = new List<AdminDependencyGraphNodeDto>();
        await using (var nodesCommand = new NpgsqlCommand(nodesSql, connection))
        {
            nodesCommand.Parameters.AddWithValue("measureNodeId", measureNodeId.Value);
            await using var reader = await nodesCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                nodes.Add(new AdminDependencyGraphNodeDto
                {
                    Id = checked((int)reader.GetInt64(0)),
                    Title = reader.GetString(1),
                    Category = reader.GetString(2)
                });
            }
        }

        const string edgesSql = @"
SELECT
    d.id,
    d.workflow_node_task_spec_id,
    d.depends_on_workflow_node_task_spec_id
FROM workflow_node_task_spec_dependencies d
WHERE d.workflow_node_id = @measureNodeId
ORDER BY d.id;";

        var edges = new List<AdminDependencyGraphEdgeDto>();
        await using (var edgesCommand = new NpgsqlCommand(edgesSql, connection))
        {
            edgesCommand.Parameters.AddWithValue("measureNodeId", measureNodeId.Value);
            await using var reader = await edgesCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                edges.Add(new AdminDependencyGraphEdgeDto
                {
                    Id = reader.GetInt64(0),
                    SourceTemplateId = checked((int)reader.GetInt64(1)),
                    TargetTemplateId = checked((int)reader.GetInt64(2)),
                    RequiredStatus = "done"
                });
            }
        }

        return new AdminDependencyGraphDto
        {
            Nodes = nodes,
            Edges = edges
        };
    }

    public async Task<List<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        const string sql = @"
SELECT
    d.id,
    d.workflow_node_task_spec_id,
    d.depends_on_workflow_node_task_spec_id,
    dep.title
FROM workflow_node_task_spec_dependencies d
JOIN workflow_node_task_specs dep ON dep.id = d.depends_on_workflow_node_task_spec_id
WHERE d.workflow_node_task_spec_id = @templateId
ORDER BY dep.sort_order, dep.title, d.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        await using var reader = await command.ExecuteReaderAsync();

        var dependencies = new List<AdminTaskTemplateDependencyDto>();
        while (await reader.ReadAsync())
        {
            dependencies.Add(MapAdminTaskTemplateDependency(reader));
        }

        return dependencies;
    }

    public async Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(
        int templateId,
        AdminTaskTemplateDependencyCreateRequest request)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        ValidateAdminTaskTemplateDependencyRequest(request);

        var dependsOnTemplate = await GetAdminTaskTemplateById(connection, null, request.DependsOnTaskTemplateId);
        if (dependsOnTemplate is null)
        {
            throw new InvalidOperationException("Die referenzierte Abhängigkeit existiert nicht.");
        }

        if (dependsOnTemplate.WorkflowDefinitionId != template.WorkflowDefinitionId)
        {
            throw new InvalidOperationException("Abhängigkeiten dürfen nur innerhalb desselben Prozesstyps angelegt werden.");
        }

        if (request.DependsOnTaskTemplateId == templateId)
        {
            throw new InvalidOperationException("Ein Task-Template darf nicht von sich selbst abhängen.");
        }

        if (await AdminTaskTemplateDependencyExists(connection, null, templateId, request.DependsOnTaskTemplateId))
        {
            throw new InvalidOperationException("Diese Abhängigkeit ist bereits vorhanden.");
        }

        var measureNodeId = await ResolveMeasureNodeIdForDefinition(connection, null, template.WorkflowDefinitionId)
            ?? throw new InvalidOperationException("Die Workflow-Definition hat keinen Massnahmen-Node in der published Version.");

        var existingDependencies = await LoadAdminTaskTemplateDependencyGraph(connection, null, measureNodeId);
        if (WouldCreateDependencyCycle(templateId, request.DependsOnTaskTemplateId, existingDependencies))
        {
            throw new InvalidOperationException("Die Abhängigkeit würde einen Kreis erzeugen.");
        }

        const string sql = @"
INSERT INTO workflow_node_task_spec_dependencies (
    workflow_node_task_spec_id,
    depends_on_workflow_node_task_spec_id,
    workflow_node_id
)
VALUES (
    @templateId,
    @dependsOnTaskTemplateId,
    @measureNodeId
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        command.Parameters.AddWithValue("dependsOnTaskTemplateId", (long)request.DependsOnTaskTemplateId);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long dependencyId)
        {
            throw new InvalidOperationException("Task spec dependency could not be created.");
        }

        return await GetAdminTaskTemplateDependencyById(connection, null, templateId, dependencyId)
            ?? throw new InvalidOperationException("Task spec dependency could not be loaded after creation.");
    }

    public async Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        if (dependencyId <= 0)
        {
            throw new InvalidOperationException("dependencyId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM workflow_node_task_spec_dependencies
WHERE id = @dependencyId
  AND workflow_node_task_spec_id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("dependencyId", dependencyId);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static AdminTaskTemplateDependencyDto MapAdminTaskTemplateDependency(NpgsqlDataReader reader)
    {
        // LA5: required_status entfaellt im Schema (immer 'done').
        return new AdminTaskTemplateDependencyDto
        {
            Id = reader.GetInt64(0),
            TaskTemplateId = checked((int)reader.GetInt64(1)),
            DependsOnTaskTemplateId = checked((int)reader.GetInt64(2)),
            DependsOnTemplateTitle = reader.GetString(3),
            RequiredStatus = "done"
        };
    }

    private static void ValidateAdminTaskTemplateDependencyRequest(AdminTaskTemplateDependencyCreateRequest request)
    {
        if (request.DependsOnTaskTemplateId <= 0)
        {
            throw new InvalidOperationException("dependsOnTaskTemplateId must be greater than zero.");
        }

        var normalizedRequiredStatus = NormalizeAdminTaskTemplateDependencyStatus(request.RequiredStatus);
        if (!AllowedAdminTaskTemplateDependencyStatuses.Contains(normalizedRequiredStatus))
        {
            throw new InvalidOperationException("Der required_status ist ungültig.");
        }
    }

    private static string NormalizeAdminTaskTemplateDependencyStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static async Task<Dictionary<int, List<int>>> LoadAdminTaskTemplateDependencyGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long measureNodeId)
    {
        const string sql = @"
SELECT d.workflow_node_task_spec_id, d.depends_on_workflow_node_task_spec_id
FROM workflow_node_task_spec_dependencies d
WHERE d.workflow_node_id = @measureNodeId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        await using var reader = await command.ExecuteReaderAsync();

        var graph = new Dictionary<int, List<int>>();
        while (await reader.ReadAsync())
        {
            var taskTemplateId = checked((int)reader.GetInt64(0));
            var dependsOnTaskTemplateId = checked((int)reader.GetInt64(1));

            if (!graph.TryGetValue(taskTemplateId, out var dependencies))
            {
                dependencies = new List<int>();
                graph[taskTemplateId] = dependencies;
            }

            dependencies.Add(dependsOnTaskTemplateId);
        }

        return graph;
    }

    private static async Task<bool> AdminTaskTemplateDependencyExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        int dependsOnTaskTemplateId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_node_task_spec_dependencies
    WHERE workflow_node_task_spec_id = @templateId
      AND depends_on_workflow_node_task_spec_id = @dependsOnTaskTemplateId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        command.Parameters.AddWithValue("dependsOnTaskTemplateId", (long)dependsOnTaskTemplateId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static bool WouldCreateDependencyCycle(
        int taskTemplateId,
        int dependsOnTaskTemplateId,
        IReadOnlyDictionary<int, List<int>> graph)
    {
        var stack = new Stack<int>();
        var visited = new HashSet<int>();
        stack.Push(dependsOnTaskTemplateId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == taskTemplateId)
            {
                return true;
            }

            if (!graph.TryGetValue(current, out var nextItems))
            {
                continue;
            }

            foreach (var next in nextItems)
            {
                stack.Push(next);
            }
        }

        return false;
    }

    private static async Task<AdminTaskTemplateDependencyDto?> GetAdminTaskTemplateDependencyById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        long dependencyId)
    {
        const string sql = @"
SELECT
    d.id,
    d.workflow_node_task_spec_id,
    d.depends_on_workflow_node_task_spec_id,
    dep.title
FROM workflow_node_task_spec_dependencies d
JOIN workflow_node_task_specs dep ON dep.id = d.depends_on_workflow_node_task_spec_id
WHERE d.id = @dependencyId
  AND d.workflow_node_task_spec_id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("dependencyId", dependencyId);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminTaskTemplateDependency(reader);
    }
}
