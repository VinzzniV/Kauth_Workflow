using Microsoft.AspNetCore.Http;

namespace API;

internal sealed class CurrentUserContext : IUserContext
{
    private const string CurrentUserCacheKey = "__current_user__";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserResolver _currentUserResolver;

    public CurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserResolver currentUserResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserResolver = currentUserResolver;
    }

    public async Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        if (httpContext.Items.TryGetValue(CurrentUserCacheKey, out var cachedValue))
        {
            return cachedValue as CurrentUser;
        }

        var currentUser = await _currentUserResolver.ResolveCurrentUser(httpContext, cancellationToken);
        httpContext.Items[CurrentUserCacheKey] = currentUser!;
        return currentUser;
    }
}
