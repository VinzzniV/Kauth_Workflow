using Microsoft.AspNetCore.Http;

namespace API;

/// <summary>
/// Resolves an identity candidate from an incoming HTTP request.
/// Implementations should return null when they cannot resolve the request.
/// </summary>
public interface IRequestIdentityResolver
{
    Task<ResolvedIdentity?> ResolveIdentity(HttpContext httpContext, CancellationToken cancellationToken = default);
}
