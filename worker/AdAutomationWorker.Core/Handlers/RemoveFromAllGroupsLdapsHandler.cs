using System.Text.Json;
using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Offboarding-Handler: entfernt einen AD-User aus allen seinen Gruppen via LDAPS.
//
// Payload-Vertrag (Pflicht):
//   userDistinguishedName: string (DN des Users)
//
// Ablauf: der Remover sucht per LDAP-Search alle Gruppen, in denen der User Mitglied ist,
// und entfernt ihn aus jeder einzeln. Code 16 (NoSuchAttribute = bereits kein Mitglied)
// wird idempotent als AlreadyRemoved behandelt.
//
// PlanAsync zeigt die aktuellen Gruppenmitgliedschaften ohne Schreibzugriff.
public sealed class RemoveFromAllGroupsLdapsHandler : IWorkerHandler
{
    private readonly IAdGroupMembershipRemover remover;

    public RemoveFromAllGroupsLdapsHandler(IAdGroupMembershipRemover remover)
    {
        this.remover = remover;
    }

    public string ActionKey => "RemoveFromAllGroupsLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerHandlerResult.Failure("Missing payload field: userDistinguishedName", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);

        var outcome = await remover.RemoveAllMembershipsAsync(userDn!, cancellationToken);

        return outcome switch
        {
            AdRemoveMembershipsOutcome.AllRemoved all => BuildAllRemovedResult(userDn!, all),
            AdRemoveMembershipsOutcome.PartiallyRemoved partial => BuildPartiallyRemovedResult(userDn!, partial),
            AdRemoveMembershipsOutcome.PermanentFailure p => BuildTopLevelFailure(p.Reason, p.LdapResultCode, WorkerFailureKinds.Permanent),
            AdRemoveMembershipsOutcome.TransientFailure t => BuildTopLevelFailure(t.Reason, t.LdapResultCode, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdRemoveMembershipsOutcome variant.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Transient),
        };
    }

    public async Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerPlanResult.Failure("Payload must be a JSON object.");

        var userDn = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDn))
            return WorkerPlanResult.Failure("Missing payload field: userDistinguishedName");

        IReadOnlyList<string> groups;
        try
        {
            groups = await remover.FindMemberOfGroupsAsync(userDn!, cancellationToken);
        }
        catch (Exception ex)
        {
            return WorkerPlanResult.Failure($"LDAP group membership query failed: {ex.Message}");
        }

        var plan = new
        {
            userDistinguishedName = userDn,
            currentGroupMemberships = groups,
            groupCount = groups.Count,
            note = groups.Count == 0
                ? "User ist in keiner Gruppe Mitglied — Ausführung ist sofortiger idempotenter Erfolg."
                : $"Ausführung entfernt User aus {groups.Count} Gruppe(n).",
        };
        return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
    }

    private static WorkerHandlerResult BuildAllRemovedResult(string userDn, AdRemoveMembershipsOutcome.AllRemoved outcome)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userDistinguishedName = userDn,
            removed = outcome.RemovedGroups,
            alreadyRemoved = outcome.AlreadyRemovedGroups,
            failed = Array.Empty<object>(),
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = outcome.RemovedGroups.Count > 0
                ? $"Removed user from {outcome.RemovedGroups.Count} group(s); {outcome.AlreadyRemovedGroups.Count} already removed."
                : $"User was already removed from all {outcome.AlreadyRemovedGroups.Count} group(s) (idempotent).",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildPartiallyRemovedResult(string userDn, AdRemoveMembershipsOutcome.PartiallyRemoved outcome)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userDistinguishedName = userDn,
            removed = outcome.RemovedGroups,
            alreadyRemoved = outcome.AlreadyRemovedGroups,
            failed = outcome.FailedGroups.Select(f => new
            {
                groupDistinguishedName = f.GroupDistinguishedName,
                ldapResultCode = f.LdapResultCode,
                reason = f.Reason,
                isPermanent = f.IsPermanent,
            }).ToArray(),
        });

        var anyPermanent = outcome.FailedGroups.Any(f => f.IsPermanent);
        var failureKind = anyPermanent ? WorkerFailureKinds.Permanent : WorkerFailureKinds.Transient;
        var errorMessage = anyPermanent
            ? $"PartiallyRemoved with at least one permanent failure ({outcome.FailedGroups.Count(f => f.IsPermanent)} permanent / {outcome.FailedGroups.Count} total)."
            : $"PartiallyRemoved with {outcome.FailedGroups.Count} transient failure(s).";
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new
            {
                removedCount = outcome.RemovedGroups.Count,
                alreadyRemovedCount = outcome.AlreadyRemovedGroups.Count,
                failedCount = outcome.FailedGroups.Count,
                failureKind,
            }),
        };
        return WorkerHandlerResult.Failure(errorMessage, new[] { log }, failureKind, output);
    }

    private static WorkerHandlerResult BuildTopLevelFailure(string reason, int? ldapResultCode, string failureKind)
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
