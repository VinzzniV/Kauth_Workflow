using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API;

internal sealed class CurrentUserResolver : ICurrentUserResolver
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserAuthorizationRepository _userAuthorizationRepository;
    private readonly ILogger<CurrentUserResolver> _logger;

    public CurrentUserResolver(
        IIdentityProvider identityProvider,
        IUserAuthorizationRepository userAuthorizationRepository,
        ILogger<CurrentUserResolver> logger)
    {
        _identityProvider = identityProvider;
        _userAuthorizationRepository = userAuthorizationRepository;
        _logger = logger;
    }

    public async Task<CurrentUser?> ResolveCurrentUser(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var identity = await _identityProvider.ResolveIdentity(httpContext, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var currentUser = await _userAuthorizationRepository.ResolveCurrentUser(identity, cancellationToken);

        // Auto-provision: when an Entra-authenticated user has no app_users record yet,
        // create one automatically so they can start using the application.
        if (currentUser is null
            && string.Equals(identity.Provider, "entra", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(identity.ExternalKey))
        {
            _logger.LogInformation(
                "Provisioning current user from external identity provider {Provider}.",
                identity.Provider);
            currentUser = await _userAuthorizationRepository.FindOrCreateFromExternalIdentity(
                identity, cancellationToken);
        }

        if (currentUser is null)
        {
            _logger.LogWarning(
                "Identity resolved for provider {Provider} but no current user mapping was found.",
                identity.Provider);
            return null;
        }

        if (!currentUser.IsActive)
        {
            _logger.LogWarning(
                "Inactive current user {UserId} resolved for provider {Provider}.",
                currentUser.UserId,
                identity.Provider);
        }

        return IsDevelopmentSimulationIdentity(identity.Provider)
            ? CreateDevelopmentSimulationUser(currentUser)
            : currentUser;
    }

    private static bool IsDevelopmentSimulationIdentity(string provider)
    {
        return provider.StartsWith("dev-sim", StringComparison.OrdinalIgnoreCase);
    }

    private static CurrentUser CreateDevelopmentSimulationUser(CurrentUser currentUser)
    {
        return new CurrentUser
        {
            UserId = currentUser.UserId,
            ExternalKey = currentUser.ExternalKey,
            DisplayName = currentUser.DisplayName,
            Email = currentUser.Email,
            IsActive = true,
            DepartmentId = currentUser.DepartmentId,
            DepartmentName = currentUser.DepartmentName,
            IdentityProvider = currentUser.IdentityProvider,
            DirectorySynced = currentUser.DirectorySynced,
            DepartmentSource = currentUser.DepartmentSource,
            DepartmentOverrideActive = currentUser.DepartmentOverrideActive,
            Groups = currentUser.Groups,
            DirectRoles = currentUser.DirectRoles,
            GroupRoles = currentUser.GroupRoles,
            EffectiveRoles = currentUser.EffectiveRoles,
            EffectivePermissions = currentUser.EffectivePermissions,
            PermissionScopes = currentUser.PermissionScopes,
            PermissionOverrides = currentUser.PermissionOverrides,
            DirectResponsibilities = currentUser.DirectResponsibilities,
            GroupResponsibilities = currentUser.GroupResponsibilities,
            EffectiveResponsibilities = currentUser.EffectiveResponsibilities
        };
    }
}
