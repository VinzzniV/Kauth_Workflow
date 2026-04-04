using Microsoft.AspNetCore.Http;

namespace API;

internal static class EndpointSupport
{
    public static MeDto ToMeDto(CurrentUser currentUser)
    {
        var username = !string.IsNullOrWhiteSpace(currentUser.ExternalKey)
            ? currentUser.ExternalKey!
            : currentUser.Email;

        var roles = currentUser.EffectiveRoles
            .Select(role => role.RoleKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleKey => roleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = currentUser.Groups
            .Select(group => group.GroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupKey => groupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = currentUser.EffectivePermissions
            .Select(permission => permission.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permissionKey => permissionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MeDto
        {
            Username = username,
            DisplayName = currentUser.DisplayName,
            Email = currentUser.Email,
            Roles = roles,
            Groups = groups,
            Permissions = permissions,
            PermissionScopes = currentUser.PermissionScopes,
            DirectorySynced = currentUser.DirectorySynced,
            DepartmentSource = currentUser.DepartmentSource,
            DepartmentOverrideActive = currentUser.DepartmentOverrideActive
        };
    }

    public static string? ExtractBearerToken(string? authorizationHeader)
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

    public static async Task<(CurrentUser? User, IResult? Error)> RequireAuthorization(
        IUserContext userContext,
        Func<CurrentUser, bool> authorizationRule,
        string forbiddenMessage)
    {
        var currentUser = await userContext.GetCurrentUser();
        if (currentUser is null || !currentUser.IsActive)
        {
            return (null, Results.Unauthorized());
        }

        if (!authorizationRule(currentUser))
        {
            return (null, Forbidden(forbiddenMessage));
        }

        return (currentUser, null);
    }

    public static IResult Forbidden(string message)
    {
        return Results.Json(new { message }, statusCode: StatusCodes.Status403Forbidden);
    }
}
