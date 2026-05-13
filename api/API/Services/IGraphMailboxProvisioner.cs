namespace API;

// Etappe 9a Schritt 7 Sub-B: schmales Provisioning-Interface fuer Exchange-Online-Mailbox.
// Konsumiert wird der Provisioner vom `CreateMailboxGraphHandler` (Sub-C). Auth ist App-only
// via ClientSecretCredential (gleiche App-Registrierung wie GraphMailSender, aber zusaetzliche
// Application-Permissions: User.Read.All fuer GET /users + LicenseAssignment.ReadWrite.All
// fuer assignLicense).
//
// Outcome ist eine Discriminated Union, damit Handler das Ergebnis direkt auf
// `WorkflowAutomationHandlerResult.Success/Failure` mappen koennen.
internal interface IGraphMailboxProvisioner
{
    Task<GraphMailboxProvisionOutcome> AssignExchangeLicenseAsync(
        GraphMailboxProvisionRequest request,
        CancellationToken cancellationToken);
}

internal sealed record GraphMailboxProvisionRequest
{
    public required string UserPrincipalName { get; init; }
    public required Guid SkuId { get; init; }
}

// Discriminated union. Gleiche Form wie GraphMailSendOutcome aus Schritt 5, plus zwei
// transient-Sub-Varianten fuer die zwei verschiedenen Sync-/Provisioning-Wartepunkte.
internal abstract record GraphMailboxProvisionOutcome
{
    private GraphMailboxProvisionOutcome() { }

    // Provisioned ist NUR dann gerechtfertigt, wenn eine echte SMTP-Adresse aus proxyAddresses
    // oder mail beobachtbar ist. Kein UPN-Fallback hier -- siehe MailboxProvisioningInProgress.
    public sealed record Provisioned(string PrimarySmtpAddress, Guid SkuId, DateTime AssignedAtUtc) : GraphMailboxProvisionOutcome;

    // User existiert in Entra noch nicht -- Entra-Connect-Sync hat noch nicht gelaufen.
    // Retry mit Backoff ist sinnvoll. Eigene Variante (statt einfach TransientFailure), damit
    // der Handler eine zielgerichtete Diagnose-Meldung + Log-Marker setzen kann.
    public sealed record UserNotInDirectoryYet(string Reason) : GraphMailboxProvisionOutcome;

    // assignLicense ist erfolgreich, aber Exchange Online hat die Mailbox noch nicht voll
    // provisioniert -- proxyAddresses/mail enthalten noch keine SMTP. Transient; Retry liest
    // nach. Bewusst eigene Outcome-Variante, damit operative Diagnose (Logs) klar erkennbar
    // bleibt, ob das Problem Sync-Lag oder Mailbox-Provisioning-Lag ist.
    public sealed record MailboxProvisioningInProgress(string Reason) : GraphMailboxProvisionOutcome;

    public sealed record PermanentFailure(string Reason, int? HttpStatus) : GraphMailboxProvisionOutcome;

    public sealed record TransientFailure(string Reason, int? HttpStatus) : GraphMailboxProvisionOutcome;
}
