using System.Text.Json;

namespace API;

internal abstract class SimulatedWorkflowAutomationActionHandler : IWorkflowAutomationActionHandler
{
    private readonly string _simulationKind;

    protected SimulatedWorkflowAutomationActionHandler(string actionKey, string simulationKind)
    {
        ActionKey = actionKey;
        _simulationKind = simulationKind;
    }

    public string ActionKey { get; }

    public Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfConfiguredFailure(context);

        return Task.FromResult(new WorkflowAutomationHandlerResult
        {
            Output = JsonSerializer.SerializeToElement(new
            {
                simulated = true,
                simulationKind = _simulationKind,
                actionKey = ActionKey,
                handledAtUtc = DateTime.UtcNow,
                attemptNumber = context.AttemptNumber,
                payload = context.Payload
            }),
            Logs =
            [
                new WorkflowAutomationLogEntry
                {
                    Level = "info",
                    Message = $"Simulated action '{ActionKey}' executed successfully.",
                    Details = JsonSerializer.SerializeToElement(new
                    {
                        simulationKind = _simulationKind,
                        attemptNumber = context.AttemptNumber
                    })
                }
            ]
        });
    }

    private static void ThrowIfConfiguredFailure(WorkflowAutomationHandlerContext context)
    {
        if (!context.Payload.HasValue || context.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var payload = context.Payload.Value;
        if (payload.TryGetProperty("simulateFailure", out var simulateFailure)
            && simulateFailure.ValueKind == JsonValueKind.True)
        {
            throw new InvalidOperationException($"Simulated failure requested for action '{context.ActionKey}'.");
        }

        if (payload.TryGetProperty("failAttemptsBeforeSuccess", out var failAttempts)
            && failAttempts.ValueKind == JsonValueKind.Number
            && failAttempts.TryGetInt32(out var failAttemptCount)
            && failAttemptCount >= context.AttemptNumber)
        {
            throw new InvalidOperationException(
                $"Simulated transient failure requested for action '{context.ActionKey}' on attempt {context.AttemptNumber}.");
        }
    }
}

internal sealed class CreateAdUserAutomationHandler() : SimulatedWorkflowAutomationActionHandler("CreateAdUser", "directory");
internal sealed class CreateMailboxAutomationHandler() : SimulatedWorkflowAutomationActionHandler("CreateMailbox", "mailbox");
internal sealed class AssignGroupsAutomationHandler() : SimulatedWorkflowAutomationActionHandler("AssignGroups", "directory_groups");
internal sealed class CreateErpEmployeeAutomationHandler() : SimulatedWorkflowAutomationActionHandler("CreateErpEmployee", "erp");
internal sealed class SendWelcomeMailAutomationHandler() : SimulatedWorkflowAutomationActionHandler("SendWelcomeMail", "notification");
