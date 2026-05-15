using System.Text.Json;
using System.Text.Json.Nodes;

namespace API;

// Etappe 9a Schritt 7 Sub-C: erster echter Linux-side-Handler fuer Exchange-Online-Mailbox-
// Provisioning. Weist via Graph App-only eine Exchange-Lizenz zu; die Mailbox wird durch
// Exchange Online automatisch provisioniert.
//
// Pflicht-Payload:
//   - userPrincipalName (string, UPN-Format) -- kommt typischerweise ueber die
//     `created_ad_user.userPrincipalName`-Mapping-Source aus einem vorgeschalteten
//     CreateAdUserLdaps-Job.
//   - skuId (string, UUID-parsebar) -- die Exchange-Online-SKU. `Get-MgSubscribedSku` listet
//     die im Tenant verfuegbaren SKUs.
//
// Output (bei Success):
//   - primarySmtpAddress: echte beobachtete SMTP-Adresse (kein UPN-Fallback)
//   - licenseSkuId: die zugewiesene SKU
//   - assignedAtUtc: Zeitpunkt der Lizenz-Zuweisung
//
// FailureKind-Vertrag:
//   - Payload-Validation, ungueltige UPN/SKU -> Permanent
//   - GraphMailboxProvisionOutcome.PermanentFailure -> Permanent
//   - UserNotInDirectoryYet / MailboxProvisioningInProgress / TransientFailure -> Transient
//
// Retry-Budget kommt aus dem per-Action-Retry-Override (Schritt 7 Sub-A): die Action-Definition
// fuer 'CreateMailboxGraph' setzt max_attempts_override=10 + subsequent_retry_delay_seconds=300
// = ~41 min Wartezeit-Budget bis zum 10. Versuch -- sicher ueber einen Entra-Connect-Sync-Zyklus.
internal sealed class CreateMailboxGraphHandler : IWorkflowAutomationActionHandler
{
    private readonly IGraphMailboxProvisioner provisioner;

    public CreateMailboxGraphHandler(IGraphMailboxProvisioner provisioner)
    {
        this.provisioner = provisioner;
    }

    public string ActionKey => "CreateMailboxGraph";

    public async Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = context.Payload;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return Failure("Payload must be a JSON object.", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        var userPrincipalName = ReadRequiredString(payload.Value, "userPrincipalName");
        var skuIdRaw = ReadRequiredString(payload.Value, "skuId");

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return Failure("Missing payload field: userPrincipalName", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }
        if (!userPrincipalName!.Contains('@', StringComparison.Ordinal))
        {
            return Failure(
                $"Invalid 'userPrincipalName' format: '{userPrincipalName}' (missing '@').",
                WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        if (string.IsNullOrWhiteSpace(skuIdRaw))
        {
            return Failure("Missing payload field: skuId", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }
        if (!Guid.TryParse(skuIdRaw, out var skuId))
        {
            return Failure(
                $"skuId must be a UUID (got '{skuIdRaw}').",
                WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        var outcome = await provisioner.AssignExchangeLicenseAsync(
            new GraphMailboxProvisionRequest
            {
                UserPrincipalName = userPrincipalName!,
                SkuId = skuId,
            },
            cancellationToken);

        return outcome switch
        {
            GraphMailboxProvisionOutcome.Provisioned p => BuildProvisionedResult(p),
            GraphMailboxProvisionOutcome.UserNotInDirectoryYet u => BuildTransientWithInfoLog(
                u.Reason,
                "Awaiting Entra Connect sync; retry will pick up automatically."),
            GraphMailboxProvisionOutcome.MailboxProvisioningInProgress m => BuildTransientWithInfoLog(
                m.Reason,
                "License assigned, Exchange Online provisioning still pending; retry will read the primary SMTP address."),
            GraphMailboxProvisionOutcome.PermanentFailure perm => BuildFailureFromOutcome(
                perm.Reason, perm.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindPermanent),
            GraphMailboxProvisionOutcome.TransientFailure trans => BuildFailureFromOutcome(
                trans.Reason, trans.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindTransient),
            _ => Failure("Unknown GraphMailboxProvisionOutcome variant.", WorkflowAutomationRetryPolicy.FailureKindTransient),
        };
    }

    public async Task<AutomationLinuxPlanResult> PlanAsync(
        AutomationPlanContext ctx,
        CancellationToken ct = default)
    {
        var root = ctx.Payload.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return AutomationLinuxPlanResult.Failure("Payload must be a JSON object.");

        var upn = ReadRequiredString(root, "userPrincipalName");
        var skuIdRaw = ReadRequiredString(root, "skuId");

        if (string.IsNullOrWhiteSpace(skuIdRaw) || !Guid.TryParse(skuIdRaw, out var skuId))
            return AutomationLinuxPlanResult.Failure(
                string.IsNullOrWhiteSpace(skuIdRaw)
                    ? "Missing payload field: skuId"
                    : $"skuId must be a UUID (got '{skuIdRaw}').");

        if (PlanSentinels.IsSentinel(upn) || string.IsNullOrWhiteSpace(upn))
        {
            var plan = new MailboxPlan(
                UpnKnown: false,
                UserPrincipalName: null,
                SkuId: skuIdRaw!,
                SkuDisplayName: null,
                SmtpAddressKnown: false,
                SmtpNote: "SMTP-Adresse wird erst nach Exchange-Provisioning bekannt (Ausführungszeit).",
                UpnNote: "UPN aus AD-Anlage noch nicht bekannt — wird zur Ausführungszeit aufgelöst.");
            return AutomationLinuxPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
        }

        var skuDisplayName = await provisioner.TryGetSkuDisplayNameAsync(skuId, ct);

        var result = new MailboxPlan(
            UpnKnown: true,
            UserPrincipalName: upn,
            SkuId: skuIdRaw!,
            SkuDisplayName: skuDisplayName,
            SmtpAddressKnown: false,
            SmtpNote: "SMTP-Adresse wird erst nach Exchange-Provisioning bekannt (Ausführungszeit).",
            UpnNote: null);
        return AutomationLinuxPlanResult.Success(JsonSerializer.SerializeToNode(result)!);
    }

    private static WorkflowAutomationHandlerResult BuildProvisionedResult(GraphMailboxProvisionOutcome.Provisioned p)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            primarySmtpAddress = p.PrimarySmtpAddress,
            licenseSkuId = p.SkuId.ToString(),
            assignedAtUtc = p.AssignedAtUtc,
        });
        var log = new WorkflowAutomationLogEntry
        {
            Level = "info",
            Message = $"Mailbox provisioned for {p.PrimarySmtpAddress} (skuId={p.SkuId}).",
        };
        return WorkflowAutomationHandlerResult.Success(output, new[] { log });
    }

    private static WorkflowAutomationHandlerResult BuildTransientWithInfoLog(string reason, string infoLogMessage)
    {
        var infoLog = new WorkflowAutomationLogEntry
        {
            Level = "info",
            Message = infoLogMessage,
        };
        var errorLog = new WorkflowAutomationLogEntry
        {
            Level = "error",
            Message = reason,
        };
        return WorkflowAutomationHandlerResult.Failure(
            reason,
            new[] { infoLog, errorLog },
            WorkflowAutomationRetryPolicy.FailureKindTransient);
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
