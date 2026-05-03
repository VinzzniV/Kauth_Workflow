using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class ClientSystemLogEndpoints
{
    public static IEndpointRouteBuilder MapClientSystemLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/client/log-events", async (
            ClientLogEventRequest request,
            ISystemEventLogService systemEventLogService,
            IUserContext userContext) =>
        {
            var currentUser = await userContext.GetCurrentUser();
            if (currentUser is null || !currentUser.IsActive)
            {
                return Results.Unauthorized();
            }

            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = request.Severity ?? "error",
                Source = request.Source ?? "frontend",
                Category = request.Category ?? "ui",
                EventKey = request.EventKey ?? "user_visible_error",
                Message = request.Message ?? request.UserMessage ?? "Client-seitiger Fehler gemeldet.",
                UserMessage = request.UserMessage,
                ActorUserId = currentUser.UserId,
                ClientRoute = request.ClientRoute,
                ClientFunction = request.ClientFunction,
                HttpMethod = request.HttpMethod,
                HttpPath = request.HttpPath,
                HttpStatus = request.HttpStatus,
                TraceIdentifier = request.TraceIdentifier,
                WorkflowUid = request.WorkflowUid,
                RotationPlanId = request.RotationPlanId,
                TaskRef = request.TaskRef,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Details = request.Details
            });

            return Results.Accepted();
        }).RequireRateLimiting("client-log-events")
          .Produces(StatusCodes.Status202Accepted)
          .Produces(StatusCodes.Status401Unauthorized)
          .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
