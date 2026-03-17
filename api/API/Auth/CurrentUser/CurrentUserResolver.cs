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

        // TODO(real-auth): when Entra/Windows SSO is introduced, this is the
        // integration point for optional user auto-provisioning/synchronization.
        return await _userAuthorizationRepository.ResolveCurrentUser(identity, cancellationToken);
    }
}
