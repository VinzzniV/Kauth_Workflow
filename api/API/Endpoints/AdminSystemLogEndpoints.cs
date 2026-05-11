using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminSystemLogEndpoints
{
    public static IEndpointRouteBuilder MapAdminSystemLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/system/logs", async (
            [FromQuery] string? severity,
            [FromQuery] string? source,
            [FromQuery] DateTime? since,
            [FromQuery] DateTime? until,
            [FromQuery] string? search,
            [FromQuery] long? actorUserId,
            [FromQuery] Guid? workflowUid,
            [FromQuery] long? rotationPlanId,
            [FromQuery] string? taskRef,
            [FromQuery] int? limit,
            [FromQuery] string? cursor,
            ISystemEventLogService systemEventLogService,
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

            var query = BuildQuery(
                severity,
                source,
                since,
                until,
                search,
                actorUserId,
                workflowUid,
                rotationPlanId,
                taskRef,
                limit,
                cursor);
            return Results.Ok(await systemEventLogService.GetAdminLogsAsync(query));
        }).Produces<CursorPageDto<AdminSystemLogEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/system/logs/summary", async (
            [FromQuery] string? severity,
            [FromQuery] string? source,
            [FromQuery] DateTime? since,
            [FromQuery] DateTime? until,
            [FromQuery] string? search,
            [FromQuery] long? actorUserId,
            [FromQuery] Guid? workflowUid,
            [FromQuery] long? rotationPlanId,
            [FromQuery] string? taskRef,
            ISystemEventLogService systemEventLogService,
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

            var query = BuildQuery(
                severity,
                source,
                since,
                until,
                search,
                actorUserId,
                workflowUid,
                rotationPlanId,
                taskRef,
                null,
                null /* cursor not used in summary */);
            return Results.Ok(await systemEventLogService.GetAdminLogSummaryAsync(query));
        }).Produces<AdminSystemLogSummaryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static SystemEventLogQuery BuildQuery(
        string? severity,
        string? source,
        DateTime? since,
        DateTime? until,
        string? search,
        long? actorUserId,
        Guid? workflowUid,
        long? rotationPlanId,
        string? taskRef,
        int? limit,
        string? cursor)
    {
        var severities = (severity ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new SystemEventLogQuery
        {
            Severities = severities,
            Source = source,
            Since = NormalizeQueryTimestamp(since),
            Until = NormalizeQueryTimestamp(until),
            Search = search,
            ActorUserId = actorUserId,
            WorkflowUid = workflowUid,
            RotationPlanId = rotationPlanId,
            TaskRef = taskRef,
            Limit = limit ?? 50,
            Cursor = cursor
        };
    }

    private static DateTime? NormalizeQueryTimestamp(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
