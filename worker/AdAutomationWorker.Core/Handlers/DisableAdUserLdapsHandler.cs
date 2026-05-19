using System.Text.Json;
using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Offboarding-Handler: deaktiviert einen bestehenden AD-User via LDAPS.
//
// Payload-Vertrag (Pflicht):
//   userDistinguishedName: string (DN des zu deaktivierenden Users, z. B. aus dem Output
//                                   eines vorgeschalteten CreateAdUserLdaps-Steps)
//
// Idempotenz: wenn der User bereits deaktiviert ist (ACCOUNTDISABLE-Bit gesetzt), liefert der
// Handler Success mit alreadyDisabled=true — ein zweiter Lauf schadet nicht.
//
// PlanAsync zeigt den aktuellen Aktivierungsstatus ohne Schreibzugriff.
public sealed class DisableAdUserLdapsHandler : IWorkerHandler
{
    private readonly IAdUserDisabler disabler;

    public DisableAdUserLdapsHandler(IAdUserDisabler disabler)
    {
        this.disabler = disabler;
    }

    public string ActionKey => "DisableAdUserLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var dn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(dn))
            return WorkerHandlerResult.Failure("Missing payload field: userDistinguishedName", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var outcome = await disabler.DisableUserAsync(dn!, cancellationToken);

        return outcome switch
        {
            AdDisableOutcome.Disabled d => BuildDisabledResult(d),
            AdDisableOutcome.AlreadyDisabled a => BuildAlreadyDisabledResult(a),
            AdDisableOutcome.NotFound n => BuildNotFoundResult(n),
            AdDisableOutcome.PermanentFailure p => BuildFailureResult(p.LdapResultCode, p.Reason, WorkerFailureKinds.Permanent),
            AdDisableOutcome.TransientFailure t => BuildFailureResult(t.LdapResultCode, t.Reason, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdDisableOutcome variant.", Array.Empty<WorkerLogEntry>()),
        };
    }

    public async Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerPlanResult.Failure("Payload must be a JSON object.");

        var dn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(dn))
            return WorkerPlanResult.Failure("Missing payload field: userDistinguishedName");

        try
        {
            var (exists, isDisabled) = await disabler.GetUserStatusAsync(dn!, cancellationToken);
            object plan;
            if (!exists)
            {
                plan = new
                {
                    userDistinguishedName = dn,
                    currentlyEnabled = (bool?)null,
                    note = $"Kein AD-Eintrag unter DN '{dn}' gefunden — Ausführung würde permanent fehlschlagen.",
                };
            }
            else if (isDisabled)
            {
                plan = new
                {
                    userDistinguishedName = dn,
                    currentlyEnabled = (bool?)false,
                    note = "User ist bereits deaktiviert — Ausführung wäre idempotenter Erfolg.",
                };
            }
            else
            {
                plan = new
                {
                    userDistinguishedName = dn,
                    currentlyEnabled = (bool?)true,
                    note = "User ist aktiv — Ausführung setzt ACCOUNTDISABLE-Bit.",
                };
            }
            return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
        }
        catch (Exception ex)
        {
            return WorkerPlanResult.Failure($"LDAP status check failed: {ex.Message}");
        }
    }

    private static WorkerHandlerResult BuildDisabledResult(AdDisableOutcome.Disabled d)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = d.DistinguishedName,
            alreadyDisabled = false,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"AD user disabled: {d.DistinguishedName}",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildAlreadyDisabledResult(AdDisableOutcome.AlreadyDisabled a)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = a.DistinguishedName,
            alreadyDisabled = true,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"AD user was already disabled (idempotent): {a.DistinguishedName}",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildNotFoundResult(AdDisableOutcome.NotFound n)
    {
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = $"AD user not found at DN '{n.DistinguishedName}' — cannot disable.",
        };
        return WorkerHandlerResult.Failure(
            $"AD user not found at DN '{n.DistinguishedName}'.",
            new[] { log },
            WorkerFailureKinds.Permanent);
    }

    private static WorkerHandlerResult BuildFailureResult(int? ldapResultCode, string reason, string failureKind)
    {
        var prefix = ldapResultCode.HasValue ? $"LDAP {ldapResultCode.Value}: " : string.Empty;
        var errorMessage = $"{prefix}{reason}";
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new { ldapResultCode, failureKind }),
        };
        return WorkerHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static string? ReadRequiredString(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        var raw = value.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
