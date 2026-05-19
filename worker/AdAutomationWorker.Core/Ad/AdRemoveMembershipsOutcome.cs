namespace AdAutomationWorker.Core.Ad;

// Discriminated Union fuer das Ergebnis einer RemoveAllMemberships-Operation.
//   - AllRemoved: alle Gruppen-Mitgliedschaften entfernt (idempotent via Code 16).
//   - PartiallyRemoved: einige entfernt, andere fehlgeschlagen.
//   - PermanentFailure / TransientFailure: Top-Level-Fehler (Connection, BaseDn etc.).
public abstract record AdRemoveMembershipsOutcome
{
    private AdRemoveMembershipsOutcome() { }

    public sealed record AllRemoved(
        IReadOnlyList<string> RemovedGroups,
        IReadOnlyList<string> AlreadyRemovedGroups) : AdRemoveMembershipsOutcome;

    public sealed record PartiallyRemoved(
        IReadOnlyList<string> RemovedGroups,
        IReadOnlyList<string> AlreadyRemovedGroups,
        IReadOnlyList<GroupRemoveFailure> FailedGroups) : AdRemoveMembershipsOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdRemoveMembershipsOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdRemoveMembershipsOutcome;
}

public sealed record GroupRemoveFailure(
    string GroupDistinguishedName,
    int? LdapResultCode,
    string Reason,
    bool IsPermanent);
