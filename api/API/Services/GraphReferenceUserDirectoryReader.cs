using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;

namespace API;

// Slice 5 Sub-C: Graph-Implementierung des IReferenceUserDirectoryReader.
// Auth-Pattern analog GraphMailboxProvisioner (App-only via ClientSecretCredential).
// Pflicht-Permissions: User.Read.All (bereits seit Schritt 7) + Group.Read.All (neu).
//
// Endpunkte:
//   - GET /users/{upn}?$select=id,displayName  -> Anzeige-Name fuer sourceLabel.
//   - GET /users/{id}/memberOf?$select=id,displayName,onPremisesDistinguishedName
//     gefiltert auf microsoft.graph.group (keine Directory-Roles).
internal sealed class GraphReferenceUserDirectoryReader : IReferenceUserDirectoryReader
{
    private readonly INotificationEmailConfigurationService configurationService;

    public GraphReferenceUserDirectoryReader(INotificationEmailConfigurationService configurationService)
    {
        this.configurationService = configurationService;
    }

    public async Task<ReferenceUserDirectoryResult> LoadGroupsAsync(string userPrincipalName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return new ReferenceUserDirectoryResult.PermissionMissing("UserPrincipalName is required.");
        }

        NotificationEmailRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ReferenceUserDirectoryResult.PermissionMissing(ex.Message);
        }

        var client = CreateGraphClient(configuration);

        User? user;
        try
        {
            user = await client.Users[userPrincipalName].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "displayName" };
            }, cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            return new ReferenceUserDirectoryResult.NotFound(userPrincipalName);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            return new ReferenceUserDirectoryResult.PermissionMissing(
                $"Graph application permission 'User.Read.All' is missing. GET /users returned {ex.ResponseStatusCode}.");
        }
        catch (ApiException ex)
        {
            return new ReferenceUserDirectoryResult.TransientFailure(ex.Message ?? "Unknown Graph error.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ReferenceUserDirectoryResult.TransientFailure("Operation cancelled.");
        }

        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            return new ReferenceUserDirectoryResult.NotFound(userPrincipalName);
        }

        DirectoryObjectCollectionResponse? memberOf;
        try
        {
            memberOf = await client.Users[user.Id].MemberOf.GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "displayName" };
            }, cancellationToken);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            return new ReferenceUserDirectoryResult.PermissionMissing(
                $"Graph application permission 'Group.Read.All' is missing. GET /users/{{id}}/memberOf returned {ex.ResponseStatusCode}.");
        }
        catch (ApiException ex)
        {
            return new ReferenceUserDirectoryResult.TransientFailure(ex.Message ?? "Unknown Graph error.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ReferenceUserDirectoryResult.TransientFailure("Operation cancelled.");
        }

        var groups = new List<ReferenceUserGroupDto>();
        if (memberOf?.Value is not null)
        {
            foreach (var item in memberOf.Value)
            {
                if (item is not Group g) continue;  // Directory-Roles ueberspringen.
                if (string.IsNullOrWhiteSpace(g.Id)) continue;
                // Group.OnPremisesDistinguishedName ist in der aktuell vendor-ten Graph-SDK-Version
                // nicht direkt exponiert; AdditionalData traegt es jedoch, wenn $select es anfordert
                // — fuer Slice 5 fallen wir konservativ auf null zurueck (UI rendert dann nur
                // displayName + sourceLabel, der DN-Match in der 360°-Karte-4b bleibt Best-Effort
                // ueber displayName).
                groups.Add(new ReferenceUserGroupDto(
                    GroupId: g.Id,
                    DisplayName: g.DisplayName ?? g.Id,
                    OnPremDistinguishedName: null));
            }
        }

        return new ReferenceUserDirectoryResult.Loaded(user.DisplayName ?? userPrincipalName, groups);
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
