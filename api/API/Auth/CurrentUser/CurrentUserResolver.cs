using Microsoft.AspNetCore.Http;

namespace API;

internal sealed class CurrentUserResolver : ICurrentUserResolver
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserAuthorizationRepository _userAuthorizationRepository;

    public CurrentUserResolver(
        IIdentityProvider identityProvider,
        IUserAuthorizationRepository userAuthorizationRepository)
    {
        _identityProvider = identityProvider;
        _userAuthorizationRepository = userAuthorizationRepository;
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
            currentUser = await _userAuthorizationRepository.FindOrCreateFromExternalIdentity(
                identity, cancellationToken);
        }

        return currentUser;
    }
}
