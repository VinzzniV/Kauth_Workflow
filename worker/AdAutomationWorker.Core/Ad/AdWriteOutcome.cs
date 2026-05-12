namespace AdAutomationWorker.Core.Ad;

// Discriminated Union fuer die Antwort eines IAdUserWriter.CreateUserAsync-Calls.
// Created/AlreadyExists sind Erfolgs-Outcomes (idempotent), TransientFailure/PermanentFailure
// sind Fehler-Outcomes. Die Klassifikation trifft der Writer; der Handler mappt beide Failure-
// Varianten heute auf WorkerHandlerResult.Failure (Folge-Slice nutzt das Tagging fuer
// differenzierte Retry-Logik).
public abstract record AdWriteOutcome
{
    private AdWriteOutcome() { }

    public sealed record Created(string DistinguishedName) : AdWriteOutcome;

    public sealed record AlreadyExists(string DistinguishedName) : AdWriteOutcome;

    public sealed record TransientFailure(string Reason, int? LdapResultCode) : AdWriteOutcome;

    public sealed record PermanentFailure(string Reason, int? LdapResultCode) : AdWriteOutcome;
}
