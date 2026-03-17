using Microsoft.AspNetCore.Http;

namespace API;

public interface ICurrentUserResolver
{
    Task<CurrentUser?> ResolveCurrentUser(HttpContext httpContext, CancellationToken cancellationToken = default);
}
