using System.Text.Json;

namespace AdAutomationWorker.Core.Polling;

// Schmaler Read-Only-Snapshot eines erfolgreich beanspruchten Worker-Jobs.
// Genug Kontext, damit der Handler arbeiten kann; nicht mehr — alles weitere bleibt in der DB.
public sealed record WorkerJobClaim
{
    public required long JobId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required long WorkflowNodeInstanceId { get; init; }
    public required string ActionKey { get; init; }
    public required JsonElement Payload { get; init; }
    public required int AttemptNumber { get; init; }
}
