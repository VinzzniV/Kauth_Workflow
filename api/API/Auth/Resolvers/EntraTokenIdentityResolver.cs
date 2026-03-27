using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace API;

/// <summary>
/// Resolves identity from a validated Entra ID / Azure AD JWT token.
/// The JWT middleware populates httpContext.User before this resolver runs.
/// Returns null when no authenticated ClaimsPrincipal is present.
/// </summary>
internal sealed class EntraTokenIdentityResolver : IRequestIdentityResolver
{
    // Standard claim URIs for Entra ID tokens.
    private const string ObjectIdClaimUri = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string ObjectIdShortClaim = "oid";
    private const string PreferredUsernameClaim = "preferred_username";
    private const string EmailClaim = "email";
    private const string NameClaim = "name";

    public Task<ResolvedIdentity?> ResolveIdentity(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<ResolvedIdentity?>(null);
        }

        // The oid claim is the stable, immutable Entra object identifier.
        var objectId = principal.FindFirstValue(ObjectIdClaimUri)
                       ?? principal.FindFirstValue(ObjectIdShortClaim);

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Task.FromResult<ResolvedIdentity?>(null);
        }

        var email = principal.FindFirstValue(EmailClaim)
                    ?? principal.FindFirstValue(PreferredUsernameClaim);

        var displayName = principal.FindFirstValue(NameClaim);

        return Task.FromResult<ResolvedIdentity?>(new ResolvedIdentity
        {
            ExternalKey = objectId,
            Email = email,
            DisplayName = displayName,
            Provider = "entra"
        });
    }
}
