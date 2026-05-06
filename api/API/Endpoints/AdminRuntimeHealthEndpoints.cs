using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminRuntimeHealthEndpoints
{
    public static IEndpointRouteBuilder MapAdminRuntimeHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/runtime-health", async (
            [FromServices] IAdminRuntimeHealthService runtimeHealthService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var health = await runtimeHealthService.GetRuntimeHealthAsync();
            return Results.Ok(health);
        }).Produces<AdminRuntimeHealthDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized)
          .WithTags("Admin");

        return app;
    }
}
