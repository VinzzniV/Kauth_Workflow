namespace API;

// Etappe 9a Schritt 5 Sub-C: schmales Mail-Versand-Interface, das von neuen Action-Handlern
// (zunaechst SendWelcomeMailGraphHandler) konsumiert wird. Versand-Pfad ist App-only via
// `Users[senderEmail].SendMail.PostAsync(...)`, identisch zum bestehenden GraphWorkflowEmailNotificationSender.
//
// Outcome ist eine Discriminated Union (Sent / PermanentFailure / TransientFailure), damit Handler
// das Ergebnis direkt auf `WorkflowAutomationHandlerResult.Success/Failure` mappen koennen ohne
// HTTP-Exception-Klassifikation jedes Mal neu zu schreiben.
internal interface IGraphMailSender
{
    Task<GraphMailSendOutcome> SendAsync(GraphMailSendRequest request, CancellationToken cancellationToken);
}

internal sealed record GraphMailSendRequest
{
    public required string ToAddress { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }
}

internal abstract record GraphMailSendOutcome
{
    private GraphMailSendOutcome() { }

    public sealed record Sent(string? MessageId, DateTime SentAtUtc) : GraphMailSendOutcome;

    public sealed record PermanentFailure(string Reason, int? HttpStatus) : GraphMailSendOutcome;

    public sealed record TransientFailure(string Reason, int? HttpStatus) : GraphMailSendOutcome;
}
