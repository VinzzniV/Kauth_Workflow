namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag fuer das Hinzufuegen eines AD-Users zu N Gruppen via LDAPS.
// Einzige produktive Implementierung ist `LdapsAdGroupMembershipWriter` im Windows-Host. Tests
// verwenden FakeAdGroupMembershipWriter.
//
// Implementierungs-Vertrag:
//   1. Pro Group eine ModifyRequest auf den Group-DN mit DirectoryAttributeOperation.Add fuer
//      das `member`-Attribut (Wert: User-DN).
//   2. Code 20 (AttributeOrValueAlreadyExists) wird als "schon Mitglied" gemappt (idempotent).
//   3. Codes 49/50/32/21/19 → PermanentFailure pro Group (siehe LdapsAdUserWriter-Whitelist).
//   4. Alles andere → TransientFailure pro Group.
//   5. Wenn der Writer KEIN ModifyRequest absetzen kann (z. B. Connection-Level-Fehler) →
//      Top-Level PermanentFailure / TransientFailure ohne PartiallyAdded.
public interface IAdGroupMembershipWriter
{
    Task<AdGroupMembershipOutcome> AddMembershipsAsync(AdGroupMembershipSpec spec, CancellationToken cancellationToken);
}
