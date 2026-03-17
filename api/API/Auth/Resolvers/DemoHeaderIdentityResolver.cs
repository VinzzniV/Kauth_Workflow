using Microsoft.AspNetCore.Http;

namespace API;

internal sealed class DemoHeaderIdentityResolver : IRequestIdentityResolver
{
    private const string DemoUserHeader = "X-Demo-User";
    private const string DemoEmailHeader = "X-Demo-Email";
    private const string DemoDisplayNameHeader = "X-Demo-Display-Name";
    private const string DefaultUserEnvVar = "DEMO_AUTH_DEFAULT_USER";

    public Task<ResolvedIdentity?> ResolveIdentity(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var externalKey = Normalize(httpContext.Request.Headers[DemoUserHeader].FirstOrDefault());
        var email = Normalize(httpContext.Request.Headers[DemoEmailHeader].FirstOrDefault());
        var displayName = Normalize(httpContext.Request.Headers[DemoDisplayNameHeader].FirstOrDefault());

        if (string.IsNullOrWhiteSpace(externalKey) && string.IsNullOrWhiteSpace(email))
        {
            externalKey = Normalize(Environment.GetEnvironmentVariable(DefaultUserEnvVar));
        }

        if (string.IsNullOrWhiteSpace(externalKey) && string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<ResolvedIdentity?>(null);
        }

        return Task.FromResult<ResolvedIdentity?>(new ResolvedIdentity
        {
            UserId = null,
            ExternalKey = externalKey,
            Email = email,
            DisplayName = displayName,
            Provider = "demo-header"
        });
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
