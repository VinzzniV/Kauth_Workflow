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
            HttpRequest request,
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

            var query = AdminListQuery.From(request);
            return Results.Ok(await directorySyncService.GetIdentitiesAsync(query));
        }).Produces<AdminListPageDto<AdminDirectoryIdentityDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/responsibility-gaps", async (
            HttpRequest request,
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

            var query = AdminListQuery.From(request);
            return Results.Ok(await directorySyncService.GetResponsibilityGapsAsync(query));
        }).Produces<AdminListPageDto<DirectoryResponsibilityGapEntry>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/pending-imports", async (
            HttpRequest request,
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

            var query = AdminListQuery.From(request);
            return Results.Ok(await directorySyncService.GetPendingImportsAsync(query));
        }).Produces<AdminListPageDto<DirectoryPendingImportDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/directory/import", async (
            [FromBody] DirectoryImportRequest request,
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

            if (request.DirectoryIdentityIds is null || request.DirectoryIdentityIds.Count == 0)
            {
                return Results.BadRequest(new { message = "At least one directoryIdentityId is required." });
            }

            var result = await directorySyncService.ImportIdentitiesAsync(request, access.User?.UserId);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = result.FailedCount > 0 ? "warning" : "info",
                Source = "admin",
                Category = "directory_import",
                EventKey = "directory_import_batch",
                Message = $"Directory import batch: {result.ImportedCount} imported, {result.FailedCount} failed.",
                ActorUserId = access.User?.UserId,
                Details = result
            });
            return Results.Ok(result);
        }).Produces<DirectoryImportResultDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/directory/audit", async (
            HttpRequest request,
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

            var query = CursorPageQuery.From(request);
            return Results.Ok(await directorySyncService.GetMappingAuditAsync(query));
        }).Produces<CursorPageDto<AdminDirectoryMappingAuditEntryDto>>(StatusCodes.Status200OK)
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

        // A1: Entra-Identitaeten ohne people-Record. Basis fuer die Auswahlliste im Import-UI (A2).
        // department (optional) filtert auf eine Abteilung; onlyEnabled=true (default) blendet deaktivierte Konten aus.
        app.MapGet("/admin/directory/unlinked-identities", async (
            HttpRequest request,
            [FromQuery] string? department,
            [FromQuery] bool? onlyEnabled,
            [FromServices] IWorkflowCatalogService workflowCatalogService,
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

            var effectiveOnlyEnabled = onlyEnabled ?? true;
            var query = AdminListQuery.From(request);
            var result = await workflowCatalogService.GetUnlinkedDirectoryIdentitiesAsync(
                department,
                effectiveOnlyEnabled,
                query.Limit,
                query.Offset);
            return Results.Ok(result);
        }).Produces<AdminListPageDto<UnlinkedDirectoryIdentityDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
