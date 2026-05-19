using System.Text.Json;
using System.Text.Json.Nodes;

namespace API;

// Offboarding-Handler: entfernt eine Exchange-Online-Lizenz via Microsoft Graph.
//
// Payload-Vertrag (Pflicht):
//   - userPrincipalName (string, UPN-Format)
//   - skuId (string, UUID-parsebar) -- die Exchange-Online-SKU die entfernt werden soll
//
// Output (bei Success):
//   - userPrincipalName: UPN des Users
//   - licenseSkuId: die entfernte SKU
//   - licenseNotAssigned: true wenn SKU bereits nicht zugewiesen war (idempotent)
//   - removedAtUtc: Zeitpunkt der Lizenzentfernung (null wenn licenseNotAssigned)
//
// FailureKind-Vertrag:
//   - Payload-Validierung -> Permanent
//   - UserNotFound -> Permanent (Offboarding-Pfad: User sollte existieren)
//   - PermanentFailure -> Permanent
//   - TransientFailure -> Transient
internal sealed class RemoveMailboxLicenseGraphHandler : IWorkflowAutomationActionHandler
{
    private readonly IGraphMailboxDeprovisioner deprovisioner;

    public RemoveMailboxLicenseGraphHandler(IGraphMailboxDeprovisioner deprovisioner)
    {
        this.deprovisioner = deprovisioner;
    }

    public string ActionKey => "RemoveMailboxLicense";

    public async Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = context.Payload;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
            return Failure("Payload must be a JSON object.", WorkflowAutomationRetryPolicy.FailureKindPermanent);

        var userPrincipalName = ReadRequiredString(payload.Value, "userPrincipalName");
        var skuIdRaw = ReadRequiredString(payload.Value, "skuId");

        if (string.IsNullOrWhiteSpace(userPrincipalName))
            return Failure("Missing payload field: userPrincipalName", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        if (!userPrincipalName!.Contains('@', StringComparison.Ordinal))
            return Failure($"Invalid 'userPrincipalName' format: '{userPrincipalName}' (missing '@').", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        if (string.IsNullOrWhiteSpace(skuIdRaw))
            return Failure("Missing payload field: skuId", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        if (!Guid.TryParse(skuIdRaw, out var skuId))
            return Failure($"skuId must be a UUID (got '{skuIdRaw}').", WorkflowAutomationRetryPolicy.FailureKindPermanent);

        var outcome = await deprovisioner.RemoveExchangeLicenseAsync(
            new GraphMailboxDeprovisionRequest
            {
                UserPrincipalName = userPrincipalName!,
                SkuId = skuId,
            },
            cancellationToken);

        return outcome switch
        {
            GraphMailboxDeprovisionOutcome.LicenseRemoved r => BuildRemovedResult(r),
            GraphMailboxDeprovisionOutcome.LicenseNotAssigned n => BuildNotAssignedResult(n, userPrincipalName!, skuId),
            GraphMailboxDeprovisionOutcome.UserNotFound u => BuildFailureFromOutcome(u.Reason, null, WorkflowAutomationRetryPolicy.FailureKindPermanent),
            GraphMailboxDeprovisionOutcome.PermanentFailure p => BuildFailureFromOutcome(p.Reason, p.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindPermanent),
            GraphMailboxDeprovisionOutcome.TransientFailure t => BuildFailureFromOutcome(t.Reason, t.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindTransient),
            _ => Failure("Unknown GraphMailboxDeprovisionOutcome variant.", WorkflowAutomationRetryPolicy.FailureKindTransient),
        };
    }

    public Task<AutomationLinuxPlanResult> PlanAsync(
        AutomationPlanContext ctx,
        CancellationToken ct = default)
    {
        var root = ctx.Payload.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return Task.FromResult(AutomationLinuxPlanResult.Failure("Payload must be a JSON object."));

        var upn = ReadRequiredString(root, "userPrincipalName");
        var skuIdRaw = ReadRequiredString(root, "skuId");

        if (string.IsNullOrWhiteSpace(skuIdRaw) || !Guid.TryParse(skuIdRaw, out _))
            return Task.FromResult(AutomationLinuxPlanResult.Failure(
                string.IsNullOrWhiteSpace(skuIdRaw)
                    ? "Missing payload field: skuId"
                    : $"skuId must be a UUID (got '{skuIdRaw}')."));

        var plan = new MailboxDeprovisionPlan(
            UserPrincipalName: upn,
            SkuId: skuIdRaw!,
            Note: PlanSentinels.IsSentinel(upn) || string.IsNullOrWhiteSpace(upn)
                ? "UPN aus vorgeschaltetem Schritt noch nicht bekannt — wird zur Ausführungszeit aufgelöst."
                : $"Lizenz '{skuIdRaw}' wird von '{upn}' entfernt (idempotent falls nicht zugewiesen).");

        return Task.FromResult(AutomationLinuxPlanResult.Success(JsonSerializer.SerializeToNode(plan)!));
    }

    private static WorkflowAutomationHandlerResult BuildRemovedResult(GraphMailboxDeprovisionOutcome.LicenseRemoved r)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userPrincipalName = r.UserPrincipalName,
            licenseSkuId = r.SkuId.ToString(),
            licenseNotAssigned = false,
            removedAtUtc = r.RemovedAtUtc,
        });
        var log = new WorkflowAutomationLogEntry
        {
            Level = "info",
            Message = $"Exchange license removed for '{r.UserPrincipalName}' (skuId={r.SkuId}).",
        };
        return WorkflowAutomationHandlerResult.Success(output, new[] { log });
    }

    private static WorkflowAutomationHandlerResult BuildNotAssignedResult(
        GraphMailboxDeprovisionOutcome.LicenseNotAssigned n, string upn, Guid skuId)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userPrincipalName = upn,
            licenseSkuId = skuId.ToString(),
            licenseNotAssigned = true,
            removedAtUtc = (DateTime?)null,
        });
        var log = new WorkflowAutomationLogEntry
        {
            Level = "info",
            Message = n.Reason,
        };
        return WorkflowAutomationHandlerResult.Success(output, new[] { log });
    }

    private static WorkflowAutomationHandlerResult BuildFailureFromOutcome(string reason, int? httpStatus, string failureKind)
    {
        var prefix = httpStatus.HasValue ? $"Graph HTTP {httpStatus.Value}: " : string.Empty;
        var errorMessage = $"{prefix}{reason}";
        var log = new WorkflowAutomationLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new { httpStatus, failureKind }),
        };
        return WorkflowAutomationHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static WorkflowAutomationHandlerResult Failure(string errorMessage, string failureKind)
    {
        var log = new WorkflowAutomationLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new { failureKind }),
        };
        return WorkflowAutomationHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static string? ReadRequiredString(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        var raw = value.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
