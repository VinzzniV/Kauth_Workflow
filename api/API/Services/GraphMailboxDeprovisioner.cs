using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.AssignLicense;
using Microsoft.Kiota.Abstractions;

namespace API;

// Produktive IGraphMailboxDeprovisioner-Implementierung.
//
// Ablauf:
//   1. GET /users/{upn}?$select=id,assignedLicenses
//      404 -> UserNotFound (permanent: beim Offboarding sollte der User bereits existieren).
//   2. Prüfe assignedLicenses auf skuId. Nicht vorhanden -> LicenseNotAssigned (idempotent).
//   3. POST /users/{id}/assignLicense mit {addLicenses: [], removeLicenses: [skuId]}.
//      Ergebnis -> LicenseRemoved.
//
// Error-Mapping analog zu GraphMailboxProvisioner:
//   - 401/403 -> PermanentFailure (fehlende Permissions / kein Admin-Consent).
//   - 400    -> PermanentFailure (ungültige SKU etc.).
//   - 429, 5xx, Timeouts -> TransientFailure.
internal sealed class GraphMailboxDeprovisioner : IGraphMailboxDeprovisioner
{
    private readonly INotificationEmailConfigurationService configurationService;

    public GraphMailboxDeprovisioner(INotificationEmailConfigurationService configurationService)
    {
        this.configurationService = configurationService;
    }

    public async Task<GraphMailboxDeprovisionOutcome> RemoveExchangeLicenseAsync(
        GraphMailboxDeprovisionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return new GraphMailboxDeprovisionOutcome.PermanentFailure("Request must not be null.", null);

        NotificationEmailRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new GraphMailboxDeprovisionOutcome.PermanentFailure(ex.Message, null);
        }

        var client = CreateGraphClient(configuration);

        // Schritt 1: GET /users/{upn} — User-ID + aktuell zugewiesene Lizenzen.
        User? user;
        try
        {
            user = await client.Users[request.UserPrincipalName].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "assignedLicenses" };
            }, cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            return new GraphMailboxDeprovisionOutcome.UserNotFound(
                $"User '{request.UserPrincipalName}' not found in Entra — cannot remove license.");
        }
        catch (ApiException ex) when (ex.ResponseStatusCode is 401 or 403)
        {
            return new GraphMailboxDeprovisionOutcome.PermanentFailure(
                $"Graph permission 'User.Read.All' missing or no admin consent (HTTP {ex.ResponseStatusCode}). Inner: {ex.Message}",
                ex.ResponseStatusCode);
        }
        catch (ApiException ex)
        {
            return ClassifyApiException(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Operation cancelled.", null);
        }
        catch (TaskCanceledException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph GET /users timed out: " + ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph GET /users HTTP failure: " + ex.Message, null);
        }
        catch (IOException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph GET /users IO failure: " + ex.Message, null);
        }

        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            return new GraphMailboxDeprovisionOutcome.UserNotFound(
                $"User '{request.UserPrincipalName}' GET returned no id.");
        }

        // Schritt 2: Prüfe ob die SKU aktuell zugewiesen ist (Idempotenz).
        var isAssigned = user.AssignedLicenses?.Any(l => l.SkuId == request.SkuId) ?? false;
        if (!isAssigned)
        {
            return new GraphMailboxDeprovisionOutcome.LicenseNotAssigned(
                $"SKU '{request.SkuId}' is not assigned to user '{request.UserPrincipalName}' — treating as success (idempotent).");
        }

        // Schritt 3: POST /users/{id}/assignLicense mit removeLicenses.
        var removeBody = new AssignLicensePostRequestBody
        {
            AddLicenses = new List<AssignedLicense>(),
            RemoveLicenses = new List<Guid?> { request.SkuId },
        };

        try
        {
            await client.Users[user.Id].AssignLicense.PostAsync(removeBody, cancellationToken: cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode is 401 or 403)
        {
            return new GraphMailboxDeprovisionOutcome.PermanentFailure(
                $"Graph permission 'LicenseAssignment.ReadWrite.All' missing (HTTP {ex.ResponseStatusCode}). Inner: {ex.Message}",
                ex.ResponseStatusCode);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 400)
        {
            return new GraphMailboxDeprovisionOutcome.PermanentFailure(
                $"Graph removeLicense bad request for SKU '{request.SkuId}': {ex.Message}", 400);
        }
        catch (ApiException ex)
        {
            return ClassifyApiException(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Operation cancelled.", null);
        }
        catch (TaskCanceledException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph removeLicense timed out: " + ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph removeLicense HTTP failure: " + ex.Message, null);
        }
        catch (IOException ex)
        {
            return new GraphMailboxDeprovisionOutcome.TransientFailure("Graph removeLicense IO failure: " + ex.Message, null);
        }

        return new GraphMailboxDeprovisionOutcome.LicenseRemoved(
            UserPrincipalName: request.UserPrincipalName,
            SkuId: request.SkuId,
            RemovedAtUtc: DateTime.UtcNow);
    }

    private static GraphServiceClient CreateGraphClient(NotificationEmailRuntimeConfiguration configuration)
    {
        var credential = new ClientSecretCredential(
            configuration.TenantId,
            configuration.ClientId,
            configuration.ClientSecret);
        return new GraphServiceClient(credential);
    }

    private static GraphMailboxDeprovisionOutcome ClassifyApiException(ApiException ex)
    {
        var status = ex.ResponseStatusCode;
        return status is >= 500 or 429
            ? new GraphMailboxDeprovisionOutcome.TransientFailure($"Graph HTTP {status}: {ex.Message}", status)
            : new GraphMailboxDeprovisionOutcome.PermanentFailure($"Graph HTTP {status}: {ex.Message}", status);
    }
}
