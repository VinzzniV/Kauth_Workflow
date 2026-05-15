namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag fuer einen AD-User-Schreiber. Einzige produktive Implementierung
// ist `LdapsAdUserWriter` im Windows-Host (net8.0-windows). Tests verwenden FakeAdUserWriter.
//
// Implementierungs-Vertrag:
//   1. Pre-Search auf `(&(objectClass=user)(sAMAccountName=<spec.SamAccountName>))`. Treffer ->
//      `AlreadyExists` mit DN aus dem Treffer (idempotent).
//   2. AddRequest mit Pflicht-Attributen + initial disabled (`userAccountControl=0x202`).
//   3. ModifyRequest setzt `unicodePwd` (LDAPS-verschluesselt), aktiviert das Konto
//      (`userAccountControl=0x200`), und setzt `pwdLastSet=0` fuer Force-Change-at-Next-Logon.
//   4. EntryAlreadyExists-Race (LDAP-Code 68) wird als `AlreadyExists` mit DN aus spec gemappt.
//   5. Klar-permanente LDAP-Codes (49, 50, 32, 21, 19) → `PermanentFailure`. Alles andere
//      (51, 52, 81, IOException, OperationCanceled) → `TransientFailure`.
public interface IAdUserWriter
{
    Task<AdWriteOutcome> CreateUserAsync(AdUserSpec spec, CancellationToken cancellationToken);

    // Plan-Mode: Read-only-Suche nach einem User via sAMAccountName. Gibt (true, DN) wenn gefunden,
    // (false, null) wenn nicht gefunden, wirft bei Verbindungsfehlern.
    Task<(bool Exists, string? DistinguishedName)> FindUserAsync(
        string samAccountName, CancellationToken cancellationToken);
}
