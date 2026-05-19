namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag für das Verschieben eines AD-Users in eine andere OU via LDAPS.
// Produktive Implementierung: `LdapsAdUserMover` im Windows-Host.
// Tests: `FakeAdUserMover`.
//
// Implementierungs-Vertrag:
//   1. SearchRequest (Scope=Base) auf den übergebenen DN — kein Eintrag -> NotFound.
//   2. Extrahiere übergeordnete OU aus dem DN. Stimmt mit targetOu überein -> AlreadyInTargetOu.
//   3. ModifyDNRequest: newParent=targetOu, newName=aktueller RDN (bleibt erhalten), deleteOldRdn=true.
//   4. Fehler-Mapping: 49/50/32/21/19 -> PermanentFailure. Alles andere -> TransientFailure.
public interface IAdUserMover
{
    Task<AdMoveOutcome> MoveUserAsync(
        string userDistinguishedName,
        string targetOu,
        CancellationToken cancellationToken);

    // Plan-Mode: Read-only-Abfrage. Gibt die aktuelle übergeordnete OU zurück.
    Task<(bool Exists, string? CurrentOu)> GetUserOuAsync(
        string userDistinguishedName,
        CancellationToken cancellationToken);
}
