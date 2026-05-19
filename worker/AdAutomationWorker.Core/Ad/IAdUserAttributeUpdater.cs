namespace AdAutomationWorker.Core.Ad;

// Plattform-neutraler Vertrag für das Aktualisieren einer Whitelist von AD-User-Attributen via LDAPS.
// Produktive Implementierung: `LdapsAdUserAttributeUpdater` im Windows-Host.
// Tests: `FakeAdUserAttributeUpdater`.
//
// Whitelist (LDAP): manager, department, title, description.
// null-Wert = Attribut löschen (AD Replace auf leer-Liste).
//
// Implementierungs-Vertrag:
//   1. SearchRequest (Scope=Base) auf DN, liest aktuelle Whitelist-Werte.
//      Kein Eintrag -> NotFound.
//   2. Vergleich: nur tatsächlich geänderte Attribute in den ModifyRequest aufnehmen.
//      Keine Änderungen -> NoChangesNeeded.
//   3. ModifyRequest mit Replace-Operationen für alle geänderten Attribute.
//   4. Fehler-Mapping: 49/50/32/21/19 -> PermanentFailure. Alles andere -> TransientFailure.
public interface IAdUserAttributeUpdater
{
    Task<AdUpdateAttributesOutcome> UpdateAttributesAsync(
        AdUserAttributeUpdateSpec spec,
        CancellationToken cancellationToken);

    // Plan-Mode: liest aktuelle Werte der Whitelist-Attribute, ohne zu schreiben.
    Task<(bool Exists, AdUserAttributeSnapshot? Snapshot)> GetUserAttributesAsync(
        string distinguishedName,
        CancellationToken cancellationToken);
}
