namespace API;

// Schmales Deprovisioning-Interface fuer Exchange-Online-Lizenzen via Microsoft Graph.
// Spiegelung zu IGraphMailboxProvisioner (Schritt 7 Sub-B), aber fuer den Offboarding-Pfad.
//
// Permissions (Application, Admin-Consent):
//   - User.Read.All              (GET /users/{upn})
//   - LicenseAssignment.ReadWrite.All  (POST /users/{id}/assignLicense mit removeLicenses)
internal interface IGraphMailboxDeprovisioner
{
    Task<GraphMailboxDeprovisionOutcome> RemoveExchangeLicenseAsync(
        GraphMailboxDeprovisionRequest request,
        CancellationToken cancellationToken);
}

internal sealed record GraphMailboxDeprovisionRequest
{
    public required string UserPrincipalName { get; init; }
    public required Guid SkuId { get; init; }
}

// Discriminated Union fuer das Deprovisioning-Ergebnis.
internal abstract record GraphMailboxDeprovisionOutcome
{
    private GraphMailboxDeprovisionOutcome() { }

    // Lizenz wurde entfernt.
    public sealed record LicenseRemoved(string UserPrincipalName, Guid SkuId, DateTime RemovedAtUtc) : GraphMailboxDeprovisionOutcome;

    // Lizenz war dem User nicht zugewiesen — idempotenter Erfolg.
    public sealed record LicenseNotAssigned(string Reason) : GraphMailboxDeprovisionOutcome;

    // User existiert in Entra nicht. Bei Offboarding typischerweise permanent (User sollte
    // bereits existieren); ein kurzer Entra-Connect-Lag ist unwahrscheinlich im Offboarding-Pfad.
    public sealed record UserNotFound(string Reason) : GraphMailboxDeprovisionOutcome;

    public sealed record PermanentFailure(string Reason, int? HttpStatus) : GraphMailboxDeprovisionOutcome;

    public sealed record TransientFailure(string Reason, int? HttpStatus) : GraphMailboxDeprovisionOutcome;
}
