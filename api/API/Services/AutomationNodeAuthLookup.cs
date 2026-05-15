using Npgsql;

namespace API;

// Slice 3: Liefert die fuer Auth-Entscheidungen relevante Node-Metadata
// (NodeType + automation_admin_role) plus Workflow-IDs. Wird vom Plan-Endpoint
// (Conditional Auth, je nach Node-Typ) und vom Approval-Service genutzt.
internal static class AutomationNodeAuthLookup
{
    public static async Task<AutomationNodeAuthInfo?> LoadAsync(
        string? connectionString, string workflowInstanceUid, string nodeKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return await LoadAsync(connection, null, workflowInstanceUid, nodeKey, ct);
    }

    public static async Task<AutomationNodeAuthInfo?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string workflowInstanceUid,
        string nodeKey,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                w.id AS workflow_id,
                n.id AS node_id,
                n.node_key,
                n.node_type,
                n.automation_admin_role,
                ni.id AS node_instance_id,
                ni.status AS node_instance_status
            FROM public.workflows w
            INNER JOIN public.workflow_nodes n
                ON n.workflow_definition_version_id = w.workflow_definition_version_id
            LEFT JOIN public.workflow_node_instances ni
                ON ni.workflow_id = w.id AND ni.workflow_node_id = n.id
            WHERE w.uid = @uid::uuid
              AND n.node_key = @nodeKey
            ORDER BY ni.id DESC NULLS LAST
            LIMIT 1
            """;

        await using var cmd = transaction is null
            ? new NpgsqlCommand(sql, connection)
            : new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("uid", workflowInstanceUid);
        cmd.Parameters.AddWithValue("nodeKey", nodeKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new AutomationNodeAuthInfo(
            WorkflowId: reader.GetInt64(0),
            NodeId: reader.GetInt64(1),
            NodeKey: reader.GetString(2),
            NodeType: reader.GetString(3),
            AutomationAdminRole: reader.IsDBNull(4) ? null : reader.GetString(4),
            NodeInstanceId: reader.IsDBNull(5) ? null : reader.GetInt64(5),
            NodeInstanceStatus: reader.IsDBNull(6) ? null : reader.GetString(6));
    }
}

internal sealed record AutomationNodeAuthInfo(
    long WorkflowId,
    long NodeId,
    string NodeKey,
    string NodeType,
    string? AutomationAdminRole,
    long? NodeInstanceId,
    string? NodeInstanceStatus);
