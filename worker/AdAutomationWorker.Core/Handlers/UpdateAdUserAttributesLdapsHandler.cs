using System.Text.Json;
using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Change-Handler: aktualisiert eine Whitelist von AD-User-Attributen via LDAPS.
//
// Harte Payload-Whitelist (Payload-Key -> LDAP-Attribut):
//   managerDistinguishedName -> manager
//   department               -> department
//   title                    -> title
//   description              -> description
//
// Kein UPN / mail / displayName / cn / sAMAccountName — das ist der Identity-Slice (außer Scope).
//
// Clear-Semantik: explizites JSON null -> Attribut wird in AD gelöscht (Replace auf leeren Wert).
// Fehlendes Key im Payload -> Attribut wird nicht angefasst.
//
// PlanAsync: zeigt aktuellen vs. geplanten Stand der Whitelist-Felder.
public sealed class UpdateAdUserAttributesLdapsHandler : IWorkerHandler
{
    // Mapping: Payload-Key -> LDAP-Attributname.
    private static readonly IReadOnlyDictionary<string, string> PayloadToLdapAttr = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["managerDistinguishedName"] = "manager",
        ["department"] = "department",
        ["title"] = "title",
        ["description"] = "description",
    };

    private readonly IAdUserAttributeUpdater updater;

    public UpdateAdUserAttributesLdapsHandler(IAdUserAttributeUpdater updater)
    {
        this.updater = updater;
    }

    public string ActionKey => "UpdateAdUserAttributesLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerHandlerResult.Failure("Missing payload field: userDistinguishedName", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var attributes = ReadWhitelistAttributes(context.Payload);
        if (attributes.Count == 0)
            return WorkerHandlerResult.Failure(
                "Payload must contain at least one whitelisted attribute field: managerDistinguishedName, department, title, description.",
                Array.Empty<WorkerLogEntry>(),
                WorkerFailureKinds.Permanent);

        var spec = new AdUserAttributeUpdateSpec
        {
            DistinguishedName = userDn!,
            Attributes = attributes,
        };

        var outcome = await updater.UpdateAttributesAsync(spec, cancellationToken);

        return outcome switch
        {
            AdUpdateAttributesOutcome.Updated updated => BuildUpdatedResult(updated),
            AdUpdateAttributesOutcome.NoChangesNeeded noChange => BuildNoChangesResult(noChange),
            AdUpdateAttributesOutcome.NotFound notFound => BuildNotFoundResult(notFound),
            AdUpdateAttributesOutcome.PermanentFailure perm => BuildFailureResult(perm.LdapResultCode, perm.Reason, WorkerFailureKinds.Permanent),
            AdUpdateAttributesOutcome.TransientFailure transient => BuildFailureResult(transient.LdapResultCode, transient.Reason, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdUpdateAttributesOutcome variant.", Array.Empty<WorkerLogEntry>()),
        };
    }

    public async Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerPlanResult.Failure("Payload must be a JSON object.");

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerPlanResult.Failure("Missing payload field: userDistinguishedName");

        var requestedAttributes = ReadWhitelistAttributes(context.Payload);
        if (requestedAttributes.Count == 0)
            return WorkerPlanResult.Failure(
                "Payload must contain at least one whitelisted attribute field: managerDistinguishedName, department, title, description.");

        try
        {
            var (exists, snapshot) = await updater.GetUserAttributesAsync(userDn!, cancellationToken);
            if (!exists)
            {
                var plan = new
                {
                    userDistinguishedName = userDn,
                    current = (object?)null,
                    planned = (object?)null,
                    note = $"Kein AD-Eintrag unter DN '{userDn}' — Ausführung würde permanent fehlschlagen.",
                };
                return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
            }

            // Baue Current- und Planned-Übersicht für die Whitelist-Felder.
            var current = new
            {
                manager = snapshot!.Manager,
                department = snapshot.Department,
                title = snapshot.Title,
                description = snapshot.Description,
            };

            // Planned: aktueller Wert, überschrieben durch Payload-Wert wo vorhanden.
            var plannedManager = requestedAttributes.TryGetValue("manager", out var pm) ? pm : snapshot.Manager;
            var plannedDept = requestedAttributes.TryGetValue("department", out var pd) ? pd : snapshot.Department;
            var plannedTitle = requestedAttributes.TryGetValue("title", out var pt) ? pt : snapshot.Title;
            var plannedDesc = requestedAttributes.TryGetValue("description", out var pds) ? pds : snapshot.Description;

            var planned = new
            {
                manager = plannedManager,
                department = plannedDept,
                title = plannedTitle,
                description = plannedDesc,
            };

            var result = new
            {
                userDistinguishedName = userDn,
                current,
                planned,
                note = (string?)null,
            };
            return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(result)!);
        }
        catch (Exception ex)
        {
            return WorkerPlanResult.Failure($"LDAP Attribut-Abfrage fehlgeschlagen: {ex.Message}");
        }
    }

    // Liest alle vorhandenen Whitelist-Felder aus dem Payload.
    // Ergebnis-Key = LDAP-Attributname, Value = neuer Wert oder null (Clear).
    private static IReadOnlyDictionary<string, string?> ReadWhitelistAttributes(JsonElement payload)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (payloadKey, ldapAttr) in PayloadToLdapAttr)
        {
            if (!payload.TryGetProperty(payloadKey, out var element)) continue;

            if (element.ValueKind == JsonValueKind.Null)
            {
                result[ldapAttr] = null; // Clear-Semantik
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var raw = element.GetString();
                // Leerer String wird ebenfalls als Clear behandelt.
                result[ldapAttr] = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            }
            // Andere Typen (number, bool etc.) werden ignoriert — ungültige Felder stören nicht.
        }
        return result;
    }

    private static WorkerHandlerResult BuildUpdatedResult(AdUpdateAttributesOutcome.Updated updated)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = updated.DistinguishedName,
            changedAttributes = updated.ChangedAttributes,
            noChangesNeeded = false,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"AD user attributes updated: {string.Join(", ", updated.ChangedAttributes)}.",
            Details = JsonSerializer.SerializeToElement(new
            {
                distinguishedName = updated.DistinguishedName,
                changedAttributes = updated.ChangedAttributes,
            }),
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildNoChangesResult(AdUpdateAttributesOutcome.NoChangesNeeded noChange)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = noChange.DistinguishedName,
            changedAttributes = Array.Empty<string>(),
            noChangesNeeded = true,
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = "All requested attribute values were already set (idempotent, no LDAP write).",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildNotFoundResult(AdUpdateAttributesOutcome.NotFound notFound)
    {
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = $"AD user not found at DN '{notFound.DistinguishedName}' — cannot update attributes.",
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
