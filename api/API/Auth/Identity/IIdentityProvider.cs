using Microsoft.AspNetCore.Http;

namespace API;

public interface IIdentityProvider
{
    // Resolves the request identity independent of the backing auth mechanism
    // (demo session/header today, real company SSO later).
    Task<ResolvedIdentity?> ResolveIdentity(HttpContext httpContext, CancellationToken cancellationToken = default);
}
