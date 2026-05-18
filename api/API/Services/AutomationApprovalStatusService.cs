using Npgsql;

namespace API;

// Slice 7 (Admin-Gated-Automation, Live-Log): Liefert pro Approval-Bundle eine
// per-Action-Status-Sicht fuer den Approval-Dialog im running-State. Daten kommen
// aus automation_jobs + attempts + logs; Steps, fuer die noch kein Job existiert
// (Slice 5 chained sequenziell), werden als 'pending' synthetisiert.
internal sealed class AutomationApprovalStatusService
{
    private readonly LifecycleRuntimeSettings runtimeSettings;
    private readonly IAuthorizationPolicyService authorizationPolicy;
    private readonly IWorkflowAutomationReadRepository readRepository;
    private readonly WorkflowAutomationRetrySettings retrySettings;

    public AutomationApprovalStatusService(
        LifecycleRuntimeSettings runtimeSettings,
        IAuthorizationPolicyService authorizationPolicy,
        IWorkflowAutomationReadRepository readRepository,
        WorkflowAutomationRetrySettings retrySettings)
    {
        this.runtimeSettings = runtimeSettings;
        this.authorizationPolicy = authorizationPolicy;
        this.readRepository = readRepository;
        this.retrySettings = retrySettings;
    }

    public async Task<AutomationApprovalStatusOutcome> GetStatusAsync(
        long approvalId, CurrentUser user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        // 1. Approval -> Node-Auth-Info aufloesen.
        var context = await LoadApprovalContextAsync(approvalId, ct);
        if (context is null)
        {
            return new AutomationApprovalStatusOutcome.NotFound();
        }

        // 2. Authorization. Spiegelt das Approval-Service-Pattern (Slice 3):
        // Nur Nutzer mit der konfigurierten automation_admin_role duerfen die
        // Live-Sicht eines Approvals lesen.
        if (string.IsNullOrWhiteSpace(context.AutomationAdminRole))
        {
            return new AutomationApprovalStatusOutcome.InsufficientRole();
        }
        if (!authorizationPolicy.HasAnyRole(user, context.AutomationAdminRole))
        {
            return new AutomationApprovalStatusOutcome.InsufficientRole();
        }

        // 3. Bundle-Actions laden (alle, geordnet nach execution_order — auch fuer
        //    noch nicht materialisierte Jobs als pending-Synthese).
        var bundleActions = await LoadBundleActionsAsync(context.NodeId, ct);

        // 4. Jobs + Attempts + Logs fuer das Bundle.
        var jobs = await readRepository.GetAutomationJobsForNodeInstance(context.NodeInstanceId, ct);

        // 5. Match Jobs <-> Actions + Step-Aggregation.
        var jobsByActionKey = jobs.GroupBy(j => j.ActionKey).ToDictionary(g => g.Key, g => g.First());
        var steps = new List<AutomationApprovalStatusStepDto>(bundleActions.Count);
        foreach (var action in bundleActions)
        {
            var maxAttempts = ResolveEffectiveMaxAttempts(action.MaxAttemptsOverride);
            if (jobsByActionKey.TryGetValue(action.ActionKey, out var job))
            {
                steps.Add(BuildStepFromJob(job, action, maxAttempts));
            }
            else
            {
                steps.Add(new AutomationApprovalStatusStepDto
                {
                    ActionKey = action.ActionKey,
                    ExecutionOrder = action.ExecutionOrder,
                    JobStatus = "pending",
                    CurrentAttempt = 0,
                    MaxAttempts = maxAttempts,
                    LatestErrorMessage = null,
                    LatestFailureKind = null,
                    Logs = Array.Empty<AutomationApprovalStatusLogDto>()
                });
            }
        }

        var overall = DeriveOverallStatus(steps);
        return new AutomationApprovalStatusOutcome.Success(new AutomationApprovalStatusResponse
        {
            ApprovalId = approvalId,
            OverallStatus = overall,
            Steps = steps
        });
    }

    // Spiegelt WorkflowAutomationRetryPolicy.EvaluateRetryOutcome (Zeile 49):
    // effectiveMaxAttempts = override > 0 ? override : settings.MaxAttempts.
    // Drift gegen den Runtime-Pfad waere ein UX-Bug.
    private int ResolveEffectiveMaxAttempts(int? maxAttemptsOverride)
    {
        return maxAttemptsOverride is > 0 ? maxAttemptsOverride.Value : retrySettings.MaxAttempts;
    }

    private static AutomationApprovalStatusStepDto BuildStepFromJob(
        AutomationJobDetailDto job, BundleActionRecord action, int maxAttempts)
    {
        // currentAttempt = letzter Attempt im Verlauf (oder 0, wenn der Job claimed
        // aber noch kein Attempt geschrieben). attempts sind aufsteigend sortiert.
        var latestAttempt = job.Attempts.Count > 0 ? job.Attempts[^1] : null;
        var currentAttempt = latestAttempt?.AttemptNumber ?? 0;

        // latestErrorMessage/latestFailureKind nur, wenn Job auf failed steht
        // (vorherige transient-Retries sind UX-Rauschen, kein Endzustand).
        string? latestErrorMessage = null;
        string? latestFailureKind = null;
        if (string.Equals(job.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            latestErrorMessage = latestAttempt?.ErrorMessage;
            latestFailureKind = latestAttempt?.FailureKind;
        }

        var logs = job.Logs
            .Select(l => new AutomationApprovalStatusLogDto
            {
                Level = l.Level,
                Message = l.Message,
                CreatedAt = l.CreatedAt
            })
            .ToList();

        return new AutomationApprovalStatusStepDto
        {
            ActionKey = action.ActionKey,
            ExecutionOrder = action.ExecutionOrder,
            JobStatus = job.Status,
            CurrentAttempt = currentAttempt,
            MaxAttempts = maxAttempts,
            LatestErrorMessage = latestErrorMessage,
            LatestFailureKind = latestFailureKind,
            Logs = logs
        };
    }

    private static string DeriveOverallStatus(IReadOnlyList<AutomationApprovalStatusStepDto> steps)
    {
        if (steps.Count == 0) return "running";
        if (steps.Any(s => string.Equals(s.JobStatus, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }
        if (steps.All(s => string.Equals(s.JobStatus, "succeeded", StringComparison.OrdinalIgnoreCase)))
        {
            return "succeeded";
        }
        return "running";
    }

    private async Task<ApprovalContext?> LoadApprovalContextAsync(long approvalId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                a.workflow_id,
                a.workflow_node_instance_id,
                n.id AS node_id,
                n.node_key,
                n.automation_admin_role
            FROM public.automation_approvals a
            INNER JOIN public.workflow_node_instances ni ON ni.id = a.workflow_node_instance_id
            INNER JOIN public.workflow_nodes n ON n.id = ni.workflow_node_id
            WHERE a.id = @approvalId
            LIMIT 1
            """;
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("approvalId", approvalId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ApprovalContext(
            WorkflowId: reader.GetInt64(0),
            NodeInstanceId: reader.GetInt64(1),
            NodeId: reader.GetInt64(2),
            NodeKey: reader.GetString(3),
            AutomationAdminRole: reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private async Task<IReadOnlyList<BundleActionRecord>> LoadBundleActionsAsync(long nodeId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                ad.action_key,
                wna.execution_order,
                ad.max_attempts_override
            FROM public.workflow_node_actions wna
            INNER JOIN public.action_definitions ad ON ad.id = wna.action_definition_id
            WHERE wna.workflow_node_id = @nodeId
            ORDER BY wna.execution_order ASC, wna.id ASC
            """;
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("nodeId", nodeId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<BundleActionRecord>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new BundleActionRecord(
                ActionKey: reader.GetString(0),
                ExecutionOrder: reader.GetInt32(1),
                MaxAttemptsOverride: reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        }
        return result;
    }

    private sealed record ApprovalContext(
        long WorkflowId,
        long NodeInstanceId,
        long NodeId,
        string NodeKey,
        string? AutomationAdminRole);

    private sealed record BundleActionRecord(string ActionKey, int ExecutionOrder, int? MaxAttemptsOverride);
}

internal sealed class AutomationApprovalStatusResponse
{
    public required long ApprovalId { get; init; }
    public required string OverallStatus { get; init; }  // running | succeeded | failed
    public required IReadOnlyList<AutomationApprovalStatusStepDto> Steps { get; init; }
}

internal sealed class AutomationApprovalStatusStepDto
{
    public required string ActionKey { get; init; }
    public required int ExecutionOrder { get; init; }
    public required string JobStatus { get; init; }  // pending | running | succeeded | failed | cancelled
    public required int CurrentAttempt { get; init; }
    public required int MaxAttempts { get; init; }
    public string? LatestErrorMessage { get; init; }
    public string? LatestFailureKind { get; init; }
    public required IReadOnlyList<AutomationApprovalStatusLogDto> Logs { get; init; }
}

internal sealed class AutomationApprovalStatusLogDto
{
    public required string Level { get; init; }
    public required string Message { get; init; }
    public required DateTime CreatedAt { get; init; }
}

internal abstract record AutomationApprovalStatusOutcome
{
    public sealed record Success(AutomationApprovalStatusResponse Response) : AutomationApprovalStatusOutcome;
    public sealed record NotFound() : AutomationApprovalStatusOutcome;
    public sealed record InsufficientRole() : AutomationApprovalStatusOutcome;
}
