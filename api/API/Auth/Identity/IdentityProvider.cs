using Microsoft.AspNetCore.Http;

namespace API;

internal sealed class IdentityProvider : IIdentityProvider
{
    private readonly IReadOnlyList<IRequestIdentityResolver> _resolvers;

    public IdentityProvider(IEnumerable<IRequestIdentityResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    public async Task<ResolvedIdentity?> ResolveIdentity(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        // TODO(real-auth): plug in additional resolvers (for example Entra/OIDC
        // token resolver or Windows identity resolver) without changing callers.
        foreach (var resolver in _resolvers)
        {
            var identity = await resolver.ResolveIdentity(httpContext, cancellationToken);
            if (identity is not null)
            {
                return identity;
            }
        }

        return null;
    }
}
