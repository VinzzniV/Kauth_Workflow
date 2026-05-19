using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminSystemConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminSystemConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/system/config", async (
            ISystemConfigSnapshotService snapshotService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(snapshotService.BuildSnapshot());
        }).Produces<SystemConfigSnapshotDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
