using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace API.Services.Directory;

internal sealed class EntraGraphClient : IEntraGraphClient
{
    private readonly IGraphApplicationConfigurationService _graphApplicationConfigurationService;
    private GraphServiceClient? _client;

    public EntraGraphClient(IGraphApplicationConfigurationService graphApplicationConfigurationService)
    {
        _graphApplicationConfigurationService = graphApplicationConfigurationService;
    }

    public async Task<EntraGraphInitResult> InitializeAsync(CancellationToken cancellationToken)
    {
        var configuration = await _graphApplicationConfigurationService.GetRuntimeConfiguration(cancellationToken);
        if (string.IsNullOrWhiteSpace(configuration.TenantId)
            || string.IsNullOrWhiteSpace(configuration.ClientId)
            || string.IsNullOrWhiteSpace(configuration.ClientSecret))
        {
            return new EntraGraphInitResult(EntraGraphInitStatus.MissingCredentials);
        }

        try
        {
            var credential = new ClientSecretCredential(
                configuration.TenantId,
                configuration.ClientId,
                configuration.ClientSecret,
                new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });
            _client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            return new EntraGraphInitResult(EntraGraphInitStatus.Ready);
        }
        catch (Exception ex)
        {
            return new EntraGraphInitResult(EntraGraphInitStatus.Failed, ex.Message, ex.GetType().FullName);
        }
    }

    public async Task<IReadOnlyList<EntraSecurityGroup>> LoadSecurityGroupsAsync(CancellationToken cancellationToken)
    {
        var client = RequireClient();
        var groups = new List<EntraSecurityGroup>();

        var response = await client.Groups.GetAsync(config =>
        {
            config.QueryParameters.Select = ["id", "displayName", "description", "securityEnabled"];
            config.QueryParameters.Filter = "securityEnabled eq true";
            config.QueryParameters.Top = 999;
        }, cancellationToken);

        while (response is not null)
        {
            if (response.Value is not null)
            {
                foreach (var group in response.Value)
                {
                    groups.Add(new EntraSecurityGroup(group.Id, group.DisplayName, group.Description));
                }
            }

            var nextLink = response.OdataNextLink;
            if (string.IsNullOrWhiteSpace(nextLink))
            {
                break;
            }

            response = await client.Groups.WithUrl(nextLink).GetAsync(cancellationToken: cancellationToken);
        }

        return groups;
    }

    public async Task<IReadOnlyList<EntraDirectoryUser>> LoadGroupMembersAsync(string groupId, CancellationToken cancellationToken)
    {
        var client = RequireClient();
        var members = new List<EntraDirectoryUser>();

        var response = await client.Groups[groupId].Members.GetAsync(config =>
        {
            config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "accountEnabled", "department", "employeeId"];
            config.QueryParameters.Top = 999;
        }, cancellationToken);

        while (response is not null)
        {
            if (response.Value is not null)
            {
                foreach (var member in response.Value)
                {
                    if (member is User user)
                    {
                        members.Add(new EntraDirectoryUser(
                            user.Id,
                            user.UserPrincipalName,
                            user.Mail,
                            user.DisplayName,
                            user.AccountEnabled,
                            user.Department,
                            user.EmployeeId));
                    }
                }
            }

            var nextLink = response.OdataNextLink;
            if (string.IsNullOrWhiteSpace(nextLink))
            {
                break;
            }

            response = await client.Groups[groupId].Members.WithUrl(nextLink).GetAsync(cancellationToken: cancellationToken);
        }

        return members;
    }

    private GraphServiceClient RequireClient()
    {
        return _client ?? throw new InvalidOperationException(
            "EntraGraphClient must be initialized via InitializeAsync before use.");
    }
}
