namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag fuer das Entfernen eines AD-Users aus allen seinen Gruppen.
// Produktive Implementierung: `LdapsAdGroupMembershipRemover` im Windows-Host.
// Tests: `FakeAdGroupMembershipRemover`.
//
// Implementierungs-Vertrag:
//   1. SearchRequest fuer alle Gruppen, in denen der User Mitglied ist:
//      Filter `(&(objectClass=group)(member=<escaped_dn>))` im BaseDn-Subtree.
//   2. Pro Gruppe: ModifyRequest mit Operation=Delete auf das `member`-Attribut (Wert: User-DN).
//   3. Code 16 (NoSuchAttribute) = User war kein Mitglied mehr -> AlreadyRemoved (idempotent).
//   4. Codes 49/50/32/21/19 -> PermanentFailure pro Gruppe. Alles andere -> TransientFailure.
//   5. Connection-Level-Fehler vor der Suche -> Top-Level TransientFailure/PermanentFailure.
public interface IAdGroupMembershipRemover
{
    Task<AdRemoveMembershipsOutcome> RemoveAllMembershipsAsync(
        string userDistinguishedName, CancellationToken cancellationToken);

    // Plan-Mode: listet aktuelle Gruppenmitgliedschaften des Users (read-only).
    Task<IReadOnlyList<string>> FindMemberOfGroupsAsync(
        string userDistinguishedName, CancellationToken cancellationToken);
}
