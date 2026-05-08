using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminPeopleEndpoints
{
    public static IEndpointRouteBuilder MapAdminPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/people", async (
            HttpRequest request,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessPeopleDirectory,
                "HR oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var query = AdminListQuery.From(request);
            return Results.Ok(await workflowCatalogService.GetPeopleDirectoryAsync(query));
        }).Produces<AdminListPageDto<PersonDirectoryItemDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
