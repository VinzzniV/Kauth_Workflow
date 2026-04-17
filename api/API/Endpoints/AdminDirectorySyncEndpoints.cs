using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminDirectorySyncEndpoints
{
    public static IEndpointRouteBuilder MapAdminDirectorySyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/directory/status", async (
            [FromServices] IDirectorySyncService directorySyncService,
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

            return Results.Ok(await directorySyncService.GetSyncStatusAsync());
        }).Produces<DirectorySyncStatusDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/directory/sync", async (
            [FromBody] DirectorySyncRequest? request,
            [FromServices] IDirectorySyncService directorySyncService,
            [FromServices] ISystemEventLogService systemEventLogService,
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

            var result = await directorySyncService.SyncAllAsync(request?.GroupPrefix);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    ? "error"
                    : string.Equals(result.Status, "partial", StringComparison.OrdinalIgnoreCase)
                        ? "warning"
                        : "info",
                Source = "admin",
                Category = "directory_sync",
                EventKey = "directory_sync_triggered",
                Message = $"Directory sync triggered from admin finished with status {result.Status}.",
                ActorUserId = access.User?.UserId,
                Details = result
            });
            return Results.Ok(result);
        }).Produces<DirectorySyncResult>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/groups", async (
            [FromServices] IDirectorySyncService directorySyncService,
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

            return Results.Ok(await directorySyncService.GetGroupsAsync());
        }).Produces<List<AdminDirectoryGroupDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/identities", async (
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            [FromServices] IDirectorySyncService directorySyncService,
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

            return Results.Ok(await directorySyncService.GetIdentitiesAsync(limit ?? 100, offset ?? 0));
        }).Produces<List<AdminDirectoryIdentityDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/audit", async (
            [FromQuery] int? limit,
            [FromServices] IDirectorySyncService directorySyncService,
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

            return Results.Ok(await directorySyncService.GetMappingAuditAsync(limit ?? 50));
        }).Produces<List<AdminDirectoryMappingAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/directory/group-mappings", async (
            [FromBody] AdminDirectoryGroupRoleMappingUpsertRequest request,
            [FromServices] IDirectorySyncService directorySyncService,
            [FromServices] ISystemEventLogService systemEventLogService,
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

            try
            {
                var mapping = await directorySyncService.UpsertGroupRoleMappingAsync(
                    request,
                    access.User?.UserId);
                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "admin",
                    Category = "directory_mapping",
                    EventKey = "directory_group_mapping_upserted",
                    Message = $"Directory group mapping {mapping.MappingId} saved.",
                    ActorUserId = access.User?.UserId,
                    EntityType = "directory_group_mapping",
                    EntityId = mapping.MappingId.ToString(),
                    Details = mapping
                });
                return Results.Ok(mapping);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDirectoryGroupRoleMappingDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/directory/group-mappings/{mappingId:int}", async (
            int mappingId,
            [FromServices] IDirectorySyncService directorySyncService,
            [FromServices] ISystemEventLogService systemEventLogService,
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

            var deleted = await directorySyncService.DeleteGroupRoleMappingAsync(mappingId, access.User?.UserId);
            if (deleted)
            {
                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "admin",
                    Category = "directory_mapping",
                    EventKey = "directory_group_mapping_deleted",
                    Message = $"Directory group mapping {mappingId} deleted.",
                    ActorUserId = access.User?.UserId,
                    EntityType = "directory_group_mapping",
                    EntityId = mappingId.ToString()
                });
            }
            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { message = "Directory mapping not found." });
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
