using Microsoft.AspNetCore.Http;

namespace API;

public interface IIdentityProvider
{
    // Resolves the request identity independent of the backing auth mechanism
    // (development simulation session or Entra token).
    Task<ResolvedIdentity?> ResolveIdentity(HttpContext httpContext, CancellationToken cancellationToken = default);
}
