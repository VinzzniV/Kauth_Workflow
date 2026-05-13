using System.Text.Json;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Zweiter echter LDAPS-Handler. Fuegt einen bestehenden AD-User in N Gruppen ein.
// Layering wie CreateAdUserLdapsHandler: Logik (Payload-Validierung, Outcome-Mapping) lebt im
// Core; die LDAPS-Roundtrips macht `LdapsAdGroupMembershipWriter` im Windows-Host.
//
// Payload-Vertrag (Pflicht):
//   userDistinguishedName: string (typischerweise via `created_ad_user`-Source aus dem
//                                   vorgaengigen CreateAdUserLdaps-Step)
//   groupDistinguishedNames: string[] (mind. 1)
//
// Idempotenz: AD-Code 20 (AttributeOrValueAlreadyExists) wird vom Writer als „schon Mitglied"
// gemappt — zweiter Lauf gegen denselben User+Groups liefert AllAdded mit allen Groups im
// AlreadyMember-Bucket.
public sealed class AssignGroupsLdapsHandler : IWorkerHandler
{
    private readonly IAdGroupMembershipWriter writer;

    public AssignGroupsLdapsHandler(IAdGroupMembershipWriter writer)
    {
        this.writer = writer;
    }

    public string ActionKey => "AssignGroupsLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
        {
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);
        }

        var userDistinguishedName = ReadRequiredString(context.Payload, "userDistinguishedName");
        if (string.IsNullOrWhiteSpace(userDistinguishedName))
        {
            return WorkerHandlerResult.Failure("Missing payload field: userDistinguishedName", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);
        }

        var groupDns = ReadGroupDistinguishedNames(context.Payload);
        if (groupDns is null)
        {
            return WorkerHandlerResult.Failure(
                "Missing or empty payload field: groupDistinguishedNames (expected string array with at least one entry).",
                Array.Empty<WorkerLogEntry>(),
                WorkerFailureKinds.Permanent);
        }

        var spec = new AdGroupMembershipSpec
        {
            UserDistinguishedName = userDistinguishedName,
            GroupDistinguishedNames = groupDns
        };

        var outcome = await writer.AddMembershipsAsync(spec, cancellationToken);

        return outcome switch
        {
            AdGroupMembershipOutcome.AllAdded all => BuildAllAddedResult(spec, all),
            AdGroupMembershipOutcome.PartiallyAdded partial => BuildPartiallyAddedResult(spec, partial),
            AdGroupMembershipOutcome.PermanentFailure permanent => BuildTopLevelFailure(permanent.Reason, permanent.LdapResultCode, WorkerFailureKinds.Permanent),
            AdGroupMembershipOutcome.TransientFailure transient => BuildTopLevelFailure(transient.Reason, transient.LdapResultCode, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdGroupMembershipOutcome variant.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Transient),
        };
    }

    private static WorkerHandlerResult BuildAllAddedResult(AdGroupMembershipSpec spec, AdGroupMembershipOutcome.AllAdded outcome)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userDistinguishedName = spec.UserDistinguishedName,
            newlyAdded = outcome.NewlyAddedGroups,
            alreadyMember = outcome.AlreadyMemberGroups,
            failed = Array.Empty<object>(),
        });
        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = outcome.NewlyAddedGroups.Count > 0
                ? $"Added user to {outcome.NewlyAddedGroups.Count} group(s); {outcome.AlreadyMemberGroups.Count} already member."
                : $"User was already member of all {outcome.AlreadyMemberGroups.Count} group(s) (idempotent).",
        };
        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildPartiallyAddedResult(AdGroupMembershipSpec spec, AdGroupMembershipOutcome.PartiallyAdded outcome)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            userDistinguishedName = spec.UserDistinguishedName,
            newlyAdded = outcome.NewlyAddedGroups,
            alreadyMember = outcome.AlreadyMemberGroups,
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
            ? $"PartiallyAdded with at least one permanent failure ({outcome.FailedGroups.Count(f => f.IsPermanent)} permanent / {outcome.FailedGroups.Count} total)."
            : $"PartiallyAdded with {outcome.FailedGroups.Count} transient failure(s).";
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new
            {
                newlyAddedCount = outcome.NewlyAddedGroups.Count,
                alreadyMemberCount = outcome.AlreadyMemberGroups.Count,
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

    private static IReadOnlyList<string>? ReadGroupDistinguishedNames(JsonElement payload)
    {
        if (!payload.TryGetProperty("groupDistinguishedNames", out var element)) return null;
        if (element.ValueKind != JsonValueKind.Array) return null;
        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return null;
            var dn = item.GetString();
            if (string.IsNullOrWhiteSpace(dn)) return null;
            result.Add(dn.Trim());
        }
        return result.Count == 0 ? null : result;
    }
}
