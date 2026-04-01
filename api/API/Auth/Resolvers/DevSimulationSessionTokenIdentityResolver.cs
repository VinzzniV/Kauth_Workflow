using Microsoft.AspNetCore.Http;

namespace API;

internal sealed class DevSimulationSessionTokenIdentityResolver : IRequestIdentityResolver
{
    private readonly IDevSimulationSessionStore _sessionStore;

    public DevSimulationSessionTokenIdentityResolver(IDevSimulationSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public Task<ResolvedIdentity?> ResolveIdentity(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var bearerToken = ExtractBearerToken(httpContext.Request.Headers.Authorization.FirstOrDefault());
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Task.FromResult<ResolvedIdentity?>(null);
        }

        if (!_sessionStore.TryGetSession(bearerToken, out var session) || session is null)
        {
            return Task.FromResult<ResolvedIdentity?>(null);
        }

        return Task.FromResult<ResolvedIdentity?>(new ResolvedIdentity
        {
            UserId = session.UserId,
            ExternalKey = session.IdentityKey,
            Email = null,
            DisplayName = null,
            Provider = "dev-sim-session"
        });
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader[bearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
