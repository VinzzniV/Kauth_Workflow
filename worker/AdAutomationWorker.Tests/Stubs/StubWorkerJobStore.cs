using System.Text.Json;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Core.Polling;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class StubWorkerJobStore : IWorkerJobStore
{
    public Queue<WorkerJobClaim?> ClaimQueue { get; } = new();
    public List<string> ClaimedByCalls { get; } = new();
    public List<TimeSpan> StaleReleaseCalls { get; } = new();
    public int StaleReleaseReturnValue { get; set; }
    public Queue<int> HeartbeatReturnValues { get; } = new();
    public List<(long JobId, string WorkerId)> HeartbeatCalls { get; } = new();
    public List<MarkSuccessCall> SuccessCalls { get; } = new();
    public List<MarkFailureCall> FailureCalls { get; } = new();
    public TaskCompletionSource? ClaimGate { get; set; }
    public Exception? ClaimException { get; set; }

    public async Task<WorkerJobClaim?> ClaimNextPendingJobAsync(string workerId, CancellationToken cancellationToken)
    {
        ClaimedByCalls.Add(workerId);
        if (ClaimGate is not null) await ClaimGate.Task;
        if (ClaimException is not null) throw ClaimException;
        return ClaimQueue.Count > 0 ? ClaimQueue.Dequeue() : null;
    }

    public Task<int> ReleaseStaleClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken)
    {
        StaleReleaseCalls.Add(staleTimeout);
        return Task.FromResult(StaleReleaseReturnValue);
    }

    public Task<int> UpdateHeartbeatAsync(long jobId, string workerId, CancellationToken cancellationToken)
    {
        HeartbeatCalls.Add((jobId, workerId));
        return Task.FromResult(HeartbeatReturnValues.Count > 0 ? HeartbeatReturnValues.Dequeue() : 1);
    }

    public Task MarkJobSucceededAsync(long jobId, int attemptNumber, JsonElement? output, IReadOnlyList<WorkerLogEntry> logs, CancellationToken cancellationToken, PendingVaultWrite? vaultWrite = null)
    {
        SuccessCalls.Add(new MarkSuccessCall(jobId, attemptNumber, output, logs.ToArray(), vaultWrite));
        return Task.CompletedTask;
    }

    public Task MarkJobFailedAsync(long jobId, int attemptNumber, string errorMessage, IReadOnlyList<WorkerLogEntry> logs, CancellationToken cancellationToken, string? failureKind = null, JsonElement? output = null)
    {
        FailureCalls.Add(new MarkFailureCall(jobId, attemptNumber, errorMessage, logs.ToArray(), failureKind, output));
        return Task.CompletedTask;
    }

    internal sealed record MarkSuccessCall(long JobId, int AttemptNumber, JsonElement? Output, IReadOnlyList<WorkerLogEntry> Logs, PendingVaultWrite? VaultWrite);
    internal sealed record MarkFailureCall(long JobId, int AttemptNumber, string ErrorMessage, IReadOnlyList<WorkerLogEntry> Logs, string? FailureKind, JsonElement? Output);
}
