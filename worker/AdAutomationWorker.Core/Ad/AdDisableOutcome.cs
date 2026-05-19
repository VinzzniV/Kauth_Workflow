namespace AdAutomationWorker.Core.Ad;

// Discriminated Union fuer das Ergebnis einer Disable-User-Operation.
//   - Disabled: userAccountControl.ACCOUNTDISABLE-Bit wurde neu gesetzt.
//   - AlreadyDisabled: User war bereits deaktiviert (idempotenter Erfolg).
//   - NotFound: kein AD-Eintrag unter dem DN — permanenter Fehler.
//   - PermanentFailure / TransientFailure: LDAP-Fehler (Klassifikation wie LdapsAdUserWriter).
public abstract record AdDisableOutcome
{
    private AdDisableOutcome() { }

    public sealed record Disabled(string DistinguishedName) : AdDisableOutcome;

    public sealed record AlreadyDisabled(string DistinguishedName) : AdDisableOutcome;

    public sealed record NotFound(string DistinguishedName) : AdDisableOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdDisableOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdDisableOutcome;
}
