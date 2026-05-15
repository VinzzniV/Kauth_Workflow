using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Handlers;

namespace AdAutomationWorker.Core.Polling;

// Vertrag fuer Plan-Request-Polling. Analog IWorkerJobStore, aber fuer read-only Plan-Mode-Requests.
// Einzige produktive Implementierung: PostgresWorkerPlanStore im Core.
// Tests koennen einen Stub einsetzen ohne Postgres zu brauchen.
public interface IWorkerPlanStore
{
    // Two-Phase-Claim mit FOR UPDATE SKIP LOCKED auf automation_plan_requests.
    // Gibt null wenn nichts pending ist.
    Task<WorkerPlanClaim?> ClaimNextPlanRequestAsync(string workerId, CancellationToken cancellationToken);

    // Setzt automation_plan_requests.status='completed' + plan_json + completed_at.
    Task MarkPlanRequestCompletedAsync(long requestId, JsonNode planJson, CancellationToken cancellationToken);

    // Setzt automation_plan_requests.status='failed' + error_message + completed_at.
    Task MarkPlanRequestFailedAsync(long requestId, string errorMessage, CancellationToken cancellationToken);

    // Setzt verwaiste running-Requests (claimed_at < NOW() - staleTimeout) zurueck auf pending.
    Task<int> ReleaseStaleClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken);
}

public sealed record WorkerPlanClaim
{
    public required long RequestId { get; init; }
    public required string WorkflowInstanceUid { get; init; }
    public required string NodeKey { get; init; }
    public required string ActionKey { get; init; }
    public required System.Text.Json.JsonElement PayloadJson { get; init; }
}
