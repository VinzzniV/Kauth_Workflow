using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

// Slice 3 (Admin-Gated-Automation): Approval-Endpoint-Hauptlogik. Tx-Boundaries
// sind sicherheitsrelevant — siehe Reihenfolge in der Implementation.
internal sealed class AutomationApprovalService
{
    private readonly LifecycleRuntimeSettings runtimeSettings;
    private readonly IAuthorizationPolicyService authorizationPolicy;
    private readonly WorkflowAutomationPlanService planService;
    private readonly AutomationReauthTokenService reauthTokenService;
    private readonly IWorkflowAuditWriteOperations auditWrite;

    public AutomationApprovalService(
        LifecycleRuntimeSettings runtimeSettings,
        IAuthorizationPolicyService authorizationPolicy,
        WorkflowAutomationPlanService planService,
        AutomationReauthTokenService reauthTokenService,
        IWorkflowAuditWriteOperations auditWrite)
    {
        this.runtimeSettings = runtimeSettings;
        this.authorizationPolicy = authorizationPolicy;
        this.planService = planService;
        this.reauthTokenService = reauthTokenService;
        this.auditWrite = auditWrite;
    }

    public async Task<AutomationApprovalOutcome> ApproveAsync(
        AutomationApprovalRequest request,
        CurrentUser user,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(request.WorkflowInstanceUid)
            || string.IsNullOrWhiteSpace(request.NodeKey)
            || string.IsNullOrWhiteSpace(request.PlanHash)
            || string.IsNullOrWhiteSpace(request.ReauthToken))
        {
            return new AutomationApprovalOutcome.BadRequest("Missing required fields.");
        }

        // 1. Read-only Lookup: Workflow + Node + node_instance.
        var nodeContext = await AutomationNodeAuthLookup.LoadAsync(
            runtimeSettings.ConnectionString, request.WorkflowInstanceUid, request.NodeKey, ct);
        if (nodeContext is null || nodeContext.NodeInstanceId is null)
        {
            return new AutomationApprovalOutcome.NotFound("Workflow or node not found.");
        }

        if (!string.Equals(nodeContext.NodeType, "task", StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationApprovalOutcome.NodeTypeNotApprovable();
        }

        if (string.IsNullOrWhiteSpace(nodeContext.AutomationAdminRole))
        {
            return new AutomationApprovalOutcome.NodeNotAdminGated();
        }

        if (!authorizationPolicy.HasAnyRole(user, nodeContext.AutomationAdminRole))
        {
            return new AutomationApprovalOutcome.InsufficientRole();
        }

        var nodeInstanceId = nodeContext.NodeInstanceId.Value;

        // 2. Plan-Recompute (öffnet eigene Read-Tx).
        var plan = await planService.ComputeNodePlanAsync(request.WorkflowInstanceUid, request.NodeKey, ct);
        if (plan is null)
        {
            return new AutomationApprovalOutcome.PlanUnavailable(Array.Empty<string>());
        }
        var failedActionKeys = plan.Steps.Where(s => !s.IsSuccess).Select(s => s.ActionKey).ToArray();
        if (failedActionKeys.Length > 0)
        {
            return new AutomationApprovalOutcome.PlanUnavailable(failedActionKeys);
        }

        // 3. Drift-Check — Token wird hier NICHT konsumiert; bei Drift bleibt das Token valid.
        var currentHash = PlanHash.ComputeHash(plan);
        if (!string.Equals(currentHash, request.PlanHash, StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationApprovalOutcome.PlanDrift(currentHash, plan);
        }

        // 4-12. Approval-Tx: Lock + Vorab-Check + Token-Consume + Approval + Job + Audit.
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 5. Row-Lock auf workflow_node_instances (Slice-3-Doppel-Approval-Schutz).
        var nodeExecution = await PostgresWorkflowRuntimeRepository.LoadNodeExecutionForUpdate(
            connection, tx, nodeContext.WorkflowId, nodeInstanceId);
        if (nodeExecution is null || !string.Equals(nodeExecution.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationApprovalOutcome.AlreadyApproved(
                await TryLoadExistingApprovalSummaryAsync(connection, tx, nodeInstanceId, ct));
        }

        // 6. Defensive Vorab-Check auf bestehende Approval.
        var existingApproval = await TryLoadExistingApprovalSummaryAsync(connection, tx, nodeInstanceId, ct);
        if (existingApproval is not null)
        {
            return new AutomationApprovalOutcome.AlreadyApproved(existingApproval);
        }

        // 7. Reauth-Token atomar konsumieren.
        var reauthTokenId = await reauthTokenService.ValidateAndConsumeAsync(
            request.ReauthToken,
            user.UserId,
            AutomationReauthTokenService.PurposeAutomationApproval,
            connection, tx, ct);
        if (reauthTokenId is null)
        {
            return new AutomationApprovalOutcome.ReauthRequired();
        }

        // 8. Approval-Row schreiben.
        long approvalId;
        try
        {
            approvalId = await InsertApprovalAsync(
                connection, tx,
                nodeContext.WorkflowId,
                nodeInstanceId,
                user.UserId,
                reauthTokenId.Value,
                currentHash,
                plan,
                ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")  // UNIQUE-Violation
        {
            // Race-Condition-Sicherung: Trotz Lock + Vorab-Check ist eine parallele
            // Approval irgendwie durchgekommen. Defense-in-depth.
            return new AutomationApprovalOutcome.AlreadyApproved(
                await TryLoadExistingApprovalSummaryAsync(connection, tx, nodeInstanceId, ct));
        }

        // 9. Erstes Action-Job einstellen (analog Engine-Pfad).
        var firstAction = await LoadFirstActionForNodeAsync(connection, tx, nodeContext.NodeId, ct);
        if (firstAction is null)
        {
            throw new InvalidOperationException(
                $"Task-Node '{nodeContext.NodeKey}' hat trotz automation_admin_role keine Actions konfiguriert.");
        }

        var answersByKey = await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, tx, nodeContext.WorkflowId);
        var createdAdUserOutputsByNodeKey = await PostgresWorkflowAutomationOperations.LoadCreatedAdUserOutputsForWorkflowInScope(
            connection, tx, nodeContext.WorkflowId, ct);
        var createdMailboxOutputsByNodeKey = await PostgresWorkflowAutomationOperations.LoadCreatedMailboxOutputsForWorkflowInScope(
            connection, tx, nodeContext.WorkflowId, ct);
        var payload = await PostgresWorkflowAutomationOperations.BuildAutomationJobPayloadAsync(
            connection, tx,
            nodeContext.WorkflowId,
            firstAction.InputMapping,
            answersByKey,
            ct,
            createdAdUserOutputsByNodeKey,
            createdMailboxOutputsByNodeKey);

        var firstJobId = await PostgresWorkflowAutomationOperations.CreateAutomationJobAsync(
            connection, tx,
            nodeContext.WorkflowId,
            nodeInstanceId,
            firstAction.Id,
            firstAction.ActionDefinitionId,
            payload,
            ct);

        // 10. Runtime-Event.
        await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
            connection, tx,
            nodeContext.WorkflowId,
            nodeInstanceId,
            "automation_approved",
            PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
            {
                nodeKey = nodeContext.NodeKey,
                approvalId,
                firstJobId
            }));

        // 11. Workflow-Audit.
        await auditWrite.InsertAuditEntry(
            connection, tx,
            nodeContext.WorkflowId,
            null,
            user.UserId,
            "runtime_automation_approved",
            null,
            null,
            $"nodeKey={nodeContext.NodeKey};approvalId={approvalId};firstJobId={firstJobId}");

        await tx.CommitAsync(ct);
        return new AutomationApprovalOutcome.Success(approvalId, firstJobId);
    }

    private static async Task<FirstActionRecord?> LoadFirstActionForNodeAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, long nodeId, CancellationToken ct)
    {
        const string sql = """
            SELECT wna.id, wna.action_definition_id, wna.input_mapping_json::text
            FROM public.workflow_node_actions wna
            WHERE wna.workflow_node_id = @nodeId
            ORDER BY wna.execution_order ASC, wna.id ASC
            LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("nodeId", nodeId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        JsonElement? inputMapping = reader.IsDBNull(2)
            ? null
            : JsonSerializer.Deserialize<JsonElement>(reader.GetString(2));
        return new FirstActionRecord(reader.GetInt64(0), reader.GetInt64(1), inputMapping);
    }

    private static async Task<ExistingApprovalSummary?> TryLoadExistingApprovalSummaryAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, long nodeInstanceId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, actor_user_id, created_at
            FROM public.automation_approvals
            WHERE workflow_node_instance_id = @nodeInstanceId
            LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ExistingApprovalSummary(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetDateTime(2));
    }

    private static async Task<long> InsertApprovalAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx,
        long workflowId, long nodeInstanceId, long actorUserId, long reauthTokenId,
        string planHash, NodePlanResult plan, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO public.automation_approvals
                (workflow_id, workflow_node_instance_id, actor_user_id, reauth_token_id,
                 plan_hash, plan_snapshot_json, hash_algorithm)
            VALUES
                (@workflowId, @nodeInstanceId, @actorUserId, @reauthTokenId,
                 @planHash, @planSnapshotJson::jsonb, @hashAlgorithm)
            RETURNING id
            """;

        var planJson = JsonSerializer.Serialize(plan);

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("workflowId", workflowId);
        cmd.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
        cmd.Parameters.AddWithValue("actorUserId", actorUserId);
        cmd.Parameters.AddWithValue("reauthTokenId", reauthTokenId);
        cmd.Parameters.AddWithValue("planHash", planHash);
        cmd.Parameters.Add(new NpgsqlParameter("planSnapshotJson", NpgsqlDbType.Jsonb) { Value = planJson });
        cmd.Parameters.AddWithValue("hashAlgorithm", PlanHash.Algorithm);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private sealed record FirstActionRecord(long Id, long ActionDefinitionId, JsonElement? InputMapping);
}

internal sealed record AutomationApprovalRequest(
    string WorkflowInstanceUid,
    string NodeKey,
    string PlanHash,
    string ReauthToken);

internal sealed record ExistingApprovalSummary(long ApprovalId, long ApproverUserId, DateTime ApprovedAt);

// Outcome-Union — Endpoint mapped auf HTTP-Code.
internal abstract record AutomationApprovalOutcome
{
    public sealed record Success(long ApprovalId, long FirstJobId) : AutomationApprovalOutcome;
    public sealed record BadRequest(string Message) : AutomationApprovalOutcome;
    public sealed record NotFound(string Message) : AutomationApprovalOutcome;
    public sealed record NodeTypeNotApprovable() : AutomationApprovalOutcome;
    public sealed record NodeNotAdminGated() : AutomationApprovalOutcome;
    public sealed record InsufficientRole() : AutomationApprovalOutcome;
    public sealed record ReauthRequired() : AutomationApprovalOutcome;
    public sealed record PlanUnavailable(IReadOnlyList<string> FailedActionKeys) : AutomationApprovalOutcome;
    public sealed record PlanDrift(string CurrentPlanHash, NodePlanResult Plan) : AutomationApprovalOutcome;
    public sealed record AlreadyApproved(ExistingApprovalSummary? ExistingApproval) : AutomationApprovalOutcome;
}
