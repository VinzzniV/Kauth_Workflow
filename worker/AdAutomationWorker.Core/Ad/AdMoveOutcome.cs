namespace AdAutomationWorker.Core.Ad;

// Discriminated Union für das Ergebnis einer OU-Verschiebung via ModifyDNRequest.
//   - Moved: User wurde in die Ziel-OU verschoben.
//   - AlreadyInTargetOu: User war bereits in der Ziel-OU (idempotenter Erfolg).
//   - NotFound: kein AD-Eintrag unter dem DN — permanenter Fehler.
//   - PermanentFailure / TransientFailure: LDAP-Fehler (Klassifikation analog LdapsAdUserDisabler).
public abstract record AdMoveOutcome
{
    private AdMoveOutcome() { }

    public sealed record Moved(string DistinguishedName, string FromOu, string ToOu) : AdMoveOutcome;

    public sealed record AlreadyInTargetOu(string DistinguishedName, string Ou) : AdMoveOutcome;

    public sealed record NotFound(string DistinguishedName) : AdMoveOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdMoveOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdMoveOutcome;
}
