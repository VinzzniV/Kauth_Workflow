namespace AdAutomationWorker.Core.Ad;

// Discriminated Union fuer das Ergebnis einer Group-Membership-Add-Operation.
//   - AllAdded: alle Groups erfolgreich (entweder neu hinzugefuegt oder schon Mitglied per Code 20).
//   - PartiallyAdded: einige Groups erfolgreich, andere mit Fehlern. Worker-Handler mapped das
//     in den Failure-Pfad MIT Output (Failure-Output-Pfad aus Schritt 5 Sub-A).
//   - PermanentFailure / TransientFailure: Top-Level-Failure ohne Per-Group-Detail (z. B.
//     Connection nicht aufgebaut). Output ist leer.
//
// Code 20 (AttributeOrValueAlreadyExists) wird vom Writer NICHT als Failure gemappt sondern in
// `AlreadyMemberGroups` einsortiert — damit ist die Aktion idempotent.
public abstract record AdGroupMembershipOutcome
{
    private AdGroupMembershipOutcome() { }

    public sealed record AllAdded(IReadOnlyList<string> NewlyAddedGroups, IReadOnlyList<string> AlreadyMemberGroups) : AdGroupMembershipOutcome;

    public sealed record PartiallyAdded(
        IReadOnlyList<string> NewlyAddedGroups,
        IReadOnlyList<string> AlreadyMemberGroups,
        IReadOnlyList<GroupFailure> FailedGroups) : AdGroupMembershipOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdGroupMembershipOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdGroupMembershipOutcome;
}

public sealed record GroupFailure(string GroupDistinguishedName, int? LdapResultCode, string Reason, bool IsPermanent);
