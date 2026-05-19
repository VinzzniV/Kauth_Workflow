namespace AdAutomationWorker.Core.Ad;

// Discriminated Union für das Ergebnis einer Attribut-Aktualisierung via ModifyRequest.
//   - Updated: Mindestens ein Attribut wurde geändert.
//   - NoChangesNeeded: Alle angeforderten Werte waren bereits gesetzt (kein LDAP-Write nötig).
//   - NotFound: kein AD-Eintrag unter dem DN — permanenter Fehler.
//   - PermanentFailure / TransientFailure: LDAP-Fehler.
public abstract record AdUpdateAttributesOutcome
{
    private AdUpdateAttributesOutcome() { }

    public sealed record Updated(string DistinguishedName, IReadOnlyList<string> ChangedAttributes) : AdUpdateAttributesOutcome;

    public sealed record NoChangesNeeded(string DistinguishedName) : AdUpdateAttributesOutcome;

    public sealed record NotFound(string DistinguishedName) : AdUpdateAttributesOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdUpdateAttributesOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdUpdateAttributesOutcome;
}
