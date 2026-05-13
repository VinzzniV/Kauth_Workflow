using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.AssignLicense;
using Microsoft.Kiota.Abstractions;

namespace API;

// Etappe 9a Schritt 7 Sub-B: produktive IGraphMailboxProvisioner-Implementierung.
//
// Ablauf:
//   1. GET /users/{upn}?$select=id,mail,proxyAddresses
//      404 -> UserNotInDirectoryYet (Entra-Connect-Sync-Lag).
//   2. POST /users/{id}/assignLicense mit {addLicenses: [{skuId}], removeLicenses: []}.
//   3. Zweiter GET /users/{id}?$select=mail,proxyAddresses fuer die echte primary SMTP-Adresse.
//      Quellen-Reihenfolge: proxyAddresses mit 'SMTP:' (uppercase) > mail. KEIN UPN-Fallback.
//      Wenn beides leer: MailboxProvisioningInProgress (Exchange provisioniert noch async).
//
// Error-Mapping (siehe GraphMailSender als Vorlage; HTTP-Klassifikation analog):
//   - 401/403 auf GET    -> PermanentFailure mit "User.Read.All missing".
//   - 401/403 auf assign -> PermanentFailure mit "LicenseAssignment.ReadWrite.All missing".
//   - 400 mit "Unknown subscribedSku" -> PermanentFailure mit SKU-Hinweis.
//   - 400 "CountViolation" / Pool-exhausted -> PermanentFailure mit Pool-Hinweis.
//   - 429, 5xx, Timeouts, IO -> TransientFailure.
internal sealed class GraphMailboxProvisioner : IGraphMailboxProvisioner
{
    private readonly INotificationEmailConfigurationService configurationService;

    public GraphMailboxProvisioner(INotificationEmailConfigurationService configurationService)
    {
        this.configurationService = configurationService;
    }

    public async Task<GraphMailboxProvisionOutcome> AssignExchangeLicenseAsync(
        GraphMailboxProvisionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure("Request must not be null.", null);
        }
        if (string.IsNullOrWhiteSpace(request.UserPrincipalName))
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure("UserPrincipalName is required.", null);
        }

        NotificationEmailRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(ex.Message, null);
        }

        var client = CreateGraphClient(configuration);

        // Schritt 1: GET /users/{upn} — prueft, ob der User in Entra schon existiert.
        User? user;
        try
        {
            user = await client.Users[request.UserPrincipalName].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "proxyAddresses" };
            }, cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            return new GraphMailboxProvisionOutcome.UserNotInDirectoryYet(
                $"User '{request.UserPrincipalName}' not found in Entra — likely Entra Connect sync lag. Retry will pick up.");
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(
                "Graph application permission 'User.Read.All' is missing (or admin consent not granted). " +
                $"The GET /users/{{upn}} call returned {ex.ResponseStatusCode}. Inner: {ex.Message}",
                ex.ResponseStatusCode);
        }
        catch (ApiException ex)
        {
            return ClassifyApiException(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Operation cancelled.", null);
        }
        catch (TaskCanceledException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph GET /users timed out: " + ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph GET /users HTTP failure: " + ex.Message, null);
        }
        catch (IOException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph GET /users IO failure: " + ex.Message, null);
        }

        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            return new GraphMailboxProvisionOutcome.UserNotInDirectoryYet(
                $"User '{request.UserPrincipalName}' GET succeeded but returned no id — likely Entra Connect sync lag.");
        }

        // Schritt 2: POST /users/{id}/assignLicense.
        var assignBody = new AssignLicensePostRequestBody
        {
            AddLicenses = new List<AssignedLicense>
            {
                new() { SkuId = request.SkuId, DisabledPlans = new List<Guid?>() }
            },
            RemoveLicenses = new List<Guid?>(),
        };

        try
        {
            await client.Users[user.Id].AssignLicense.PostAsync(assignBody, cancellationToken: cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(
                "Graph application permission 'LicenseAssignment.ReadWrite.All' is missing (or admin consent not granted). " +
                $"The assignLicense call returned {ex.ResponseStatusCode}. Inner: {ex.Message}",
                ex.ResponseStatusCode);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 400)
        {
            return MapAssignLicenseBadRequest(ex.Message ?? string.Empty, request.SkuId);
        }
        catch (ApiException ex)
        {
            return ClassifyApiException(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Operation cancelled.", null);
        }
        catch (TaskCanceledException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph assignLicense timed out: " + ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph assignLicense HTTP failure: " + ex.Message, null);
        }
        catch (IOException ex)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph assignLicense IO failure: " + ex.Message, null);
        }

        // Schritt 3: zweiter GET fuer die echte primary SMTP-Adresse.
        User? refreshed;
        try
        {
            refreshed = await client.Users[user.Id].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "mail", "proxyAddresses" };
            }, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ClassifyApiException(ex);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException or HttpRequestException or IOException)
        {
            return new GraphMailboxProvisionOutcome.TransientFailure("Graph GET /users (post-assign) failure: " + ex.Message, null);
        }

        var primarySmtp = ExtractPrimarySmtpAddress(refreshed);
        if (string.IsNullOrWhiteSpace(primarySmtp))
        {
            return new GraphMailboxProvisionOutcome.MailboxProvisioningInProgress(
                "Exchange Online has not yet published a primary SMTP address for the user; retry will pick it up.");
        }

        return new GraphMailboxProvisionOutcome.Provisioned(
            PrimarySmtpAddress: primarySmtp!,
            SkuId: request.SkuId,
            AssignedAtUtc: DateTime.UtcNow);
    }

    // Quellen-Reihenfolge: proxyAddresses 'SMTP:'-Praefix (uppercase = primary), dann mail.
    // KEIN UPN-Fallback -- siehe Plan-Mode-Review zu Schritt 7.
    internal static string? ExtractPrimarySmtpAddress(User? user)
    {
        if (user is null) return null;

        var proxyAddresses = user.ProxyAddresses;
        if (proxyAddresses is not null)
        {
            foreach (var address in proxyAddresses)
            {
                if (address is null) continue;
                if (address.StartsWith("SMTP:", StringComparison.Ordinal))
                {
                    var value = address["SMTP:".Length..].Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(user.Mail))
        {
            return user.Mail.Trim();
        }

        return null;
    }

    // Internal-static fuer Unit-Tests: trennt das Pattern-Matching auf der Fehlermeldung von
    // der ApiException-Konstruktion (die im Kiota-SDK schwer zu mocken ist).
    internal static GraphMailboxProvisionOutcome MapAssignLicenseBadRequest(string message, Guid skuId)
    {
        message ??= string.Empty;
        if (message.Contains("Unknown", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(
                $"Unknown skuId '{skuId}' — verify the SKU via Get-MgSubscribedSku. Inner: {message}",
                400);
        }
        if (message.Contains("CountViolation", StringComparison.OrdinalIgnoreCase)
            || message.Contains("exceed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("exhausted", StringComparison.OrdinalIgnoreCase))
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(
                $"Exchange Online license pool exhausted for skuId '{skuId}'. Free up a license or contact licensing. Inner: {message}",
                400);
        }
        return new GraphMailboxProvisionOutcome.PermanentFailure(
            $"Graph assignLicense returned 400: {message}",
            400);
    }

    private static GraphMailboxProvisionOutcome ClassifyApiException(ApiException ex)
        => ClassifyHttpStatus(ex.ResponseStatusCode, ex.Message ?? "Unknown Graph error.");

    internal static GraphMailboxProvisionOutcome ClassifyHttpStatus(int httpStatus, string message)
    {
        if (httpStatus == 400 || httpStatus == 401 || httpStatus == 403 || httpStatus == 404)
        {
            return new GraphMailboxProvisionOutcome.PermanentFailure(message, httpStatus);
        }
        return new GraphMailboxProvisionOutcome.TransientFailure(message, httpStatus);
    }

    private static GraphServiceClient CreateGraphClient(NotificationEmailRuntimeConfiguration configuration)
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var credentialOptions = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var credential = new ClientSecretCredential(
            configuration.TenantId!,
            configuration.ClientId!,
            configuration.ClientSecret!,
            credentialOptions);

        return new GraphServiceClient(credential, scopes);
    }
}
