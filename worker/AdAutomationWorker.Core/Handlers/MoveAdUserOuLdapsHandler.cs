using System.Text.Json;
using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Change-Handler: verschiebt einen AD-User per ModifyDNRequest in eine andere OU.
// RDN (CN) bleibt erhalten — kein Identity-Slice.
//
// Payload-Vertrag (Pflicht):
//   userDistinguishedName: string (aktueller DN des Users)
//   targetOu: string (Ziel-OU-DN, z. B. "OU=Archiv,DC=example,DC=local")
//
// Idempotenz: User bereits in Ziel-OU -> Success mit alreadyInTargetOu=true.
//
// PlanAsync: zeigt aktuelle OU und ob User schon am Ziel liegt.
public sealed class MoveAdUserOuLdapsHandler : IWorkerHandler
{
    private readonly IAdUserMover mover;

    public MoveAdUserOuLdapsHandler(IAdUserMover mover)
    {
        this.mover = mover;
    }

    public string ActionKey => "MoveAdUserOuLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerHandlerResult.Failure("Missing payload field: userDistinguishedName", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var targetOu = ReadRequiredString(context.Payload, "targetOu");
        if (string.IsNullOrWhiteSpace(targetOu))
            return WorkerHandlerResult.Failure("Missing payload field: targetOu", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var outcome = await mover.MoveUserAsync(userDn!, targetOu!, cancellationToken);

        return outcome switch
        {
            AdMoveOutcome.Moved moved => BuildMovedResult(moved),
            AdMoveOutcome.AlreadyInTargetOu already => BuildAlreadyInTargetOuResult(already),
            AdMoveOutcome.NotFound notFound => BuildNotFoundResult(notFound),
            AdMoveOutcome.PermanentFailure perm => BuildFailureResult(perm.LdapResultCode, perm.Reason, WorkerFailureKinds.Permanent),
            AdMoveOutcome.TransientFailure transient => BuildFailureResult(transient.LdapResultCode, transient.Reason, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdMoveOutcome variant.", Array.Empty<WorkerLogEntry>()),
        };
    }

    public async Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerPlanResult.Failure("Payload must be a JSON object.");

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerPlanResult.Failure("Missing payload field: userDistinguishedName");

        var targetOu = ReadRequiredString(context.Payload, "targetOu");
        if (string.IsNullOrWhiteSpace(targetOu))
            return WorkerPlanResult.Failure("Missing payload field: targetOu");

        try
        {
            var (exists, currentOu) = await mover.GetUserOuAsync(userDn!, cancellationToken);
            object plan;
            if (!exists)
            {
                plan = new
                {
                    userDistinguishedName = userDn,
                    targetOu,
                    currentOu = (string?)null,
                    alreadyInTargetOu = (bool?)null,
                    note = $"Kein AD-Eintrag unter DN '{userDn}' gefunden — Ausführung würde permanent fehlschlagen.",
                };
            }
            else
            {
                var alreadyInTarget = string.Equals(currentOu, targetOu, StringComparison.OrdinalIgnoreCase);
                plan = new
                {
                    userDistinguishedName = userDn,
                    targetOu,
                    currentOu,
                    alreadyInTargetOu = (bool?)alreadyInTarget,
                    note = alreadyInTarget
                        ? "User ist bereits in der Ziel-OU — Ausführung wäre idempotenter Erfolg."
                        : $"User wird von '{currentOu}' nach '{targetOu}' verschoben (RDN bleibt erhalten).",
                };
            }
            return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
        }
        catch (Exception ex)
        {
            return WorkerPlanResult.Failure($"LDAP OU-Abfrage fehlgeschlagen: {ex.Message}");
        }
    }

    private static WorkerHandlerResult BuildMovedResult(AdMoveOutcome.Moved moved)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = moved.DistinguishedName,
            fromOu = moved.FromOu,
            toOu = moved.ToOu,
            alreadyInTargetOu = false,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"AD user moved from '{moved.FromOu}' to '{moved.ToOu}'.",
            Details = JsonSerializer.SerializeToElement(new
            {
                distinguishedName = moved.DistinguishedName,
                fromOu = moved.FromOu,
                toOu = moved.ToOu,
            }),
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildAlreadyInTargetOuResult(AdMoveOutcome.AlreadyInTargetOu already)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = already.DistinguishedName,
            fromOu = already.Ou,
            toOu = already.Ou,
            alreadyInTargetOu = true,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"AD user is already in target OU '{already.Ou}' (idempotent).",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildNotFoundResult(AdMoveOutcome.NotFound notFound)
    {
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = $"AD user not found at DN '{notFound.DistinguishedName}' — cannot move.",
        };
        return WorkerHandlerResult.Failure(
            $"AD user not found at DN '{notFound.DistinguishedName}'.",
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
