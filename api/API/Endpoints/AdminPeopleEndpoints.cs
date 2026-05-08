using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        // A1: Erstellt people-Records fuer die gewaehlten Entra-Identitaeten (kein app_user).
        // Dedupliziert via directory_identity_id und employee_number.
        // Auto-Linkt zu bestehendem app_user wenn entra_object_id matcht.
        app.MapPost("/admin/people/import-from-directory", async (
            [FromBody] ImportPeopleFromDirectoryRequest request,
            [FromServices] IWorkflowCatalogService workflowCatalogService,
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

            var result = await workflowCatalogService.ImportPeopleFromDirectoryAsync(
                request,
                access.User?.UserId);

            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = result.SkippedCount > 0 && result.CreatedCount == 0 && result.LinkedCount == 0
                    ? "warning"
                    : "info",
                Source = "admin",
                Category = "people_import",
                EventKey = "people_import_from_directory",
                Message = $"People import from directory: {result.CreatedCount} created, {result.LinkedCount} linked, {result.SkippedCount} skipped.",
                ActorUserId = access.User?.UserId,
                Details = new { result.CreatedCount, result.LinkedCount, result.SkippedCount }
            });

            return Results.Ok(result);
        }).Produces<ImportPeopleFromDirectoryResultDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        // A3: Inline-Bearbeitung fehlender Stammdaten auf der Mitarbeiterkarte.
        // Setzt Eintrittsdatum und Ausweisnummer; null loescht den jeweiligen Wert.
        app.MapPatch("/admin/people/{personId:long}", async (
            long personId,
            [FromBody] UpdatePersonRequest request,
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

            var updated = await workflowCatalogService.UpdatePersonAsync(personId, request);
            return updated ? Results.NoContent() : Results.NotFound();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
