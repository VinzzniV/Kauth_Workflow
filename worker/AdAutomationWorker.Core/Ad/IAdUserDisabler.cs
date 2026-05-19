namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag fuer das Deaktivieren eines AD-Users via LDAPS.
// Produktive Implementierung: `LdapsAdUserDisabler` im Windows-Host.
// Tests: `FakeAdUserDisabler`.
//
// Implementierungs-Vertrag:
//   1. GET auf den uebergebenen DN via SearchRequest (Scope=Base), Attribut: userAccountControl.
//      Kein Eintrag -> NotFound (permanent — DN ist sauber berechnet, kein Retry-Nutzen).
//   2. Lese userAccountControl; wenn Bit 0x2 (ACCOUNTDISABLE) gesetzt -> AlreadyDisabled (idempotent).
//   3. ModifyRequest: Replace userAccountControl mit aktueller Wert | 0x2.
//   4. Fehler-Mapping: Codes 49/50/21/19 -> PermanentFailure. Alles andere -> TransientFailure.
public interface IAdUserDisabler
{
    Task<AdDisableOutcome> DisableUserAsync(string distinguishedName, CancellationToken cancellationToken);

    // Plan-Mode: Read-only-Abfrage des Aktivierungsstatus. Gibt (true, isDisabled) wenn gefunden,
    // (false, _) wenn nicht gefunden, wirft bei Verbindungsfehlern.
    Task<(bool Exists, bool IsDisabled)> GetUserStatusAsync(string distinguishedName, CancellationToken cancellationToken);
}
