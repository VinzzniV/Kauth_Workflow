using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace API;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var runtimeSettings = app.ServiceProvider.GetRequiredService<LifecycleRuntimeSettings>();
        var devSimulationEnabled = runtimeSettings.DevSimulationEnabled;
        var entraEnabled = runtimeSettings.EntraAuthEnabled;
        var authMode = runtimeSettings.AuthMode;

        app.MapGet("/auth/provider-info", () => Results.Ok(new
        {
            mode = authMode,
            entraEnabled,
            devSimulationEnabled
        }));

        if (devSimulationEnabled)
        {
            app.MapGet("/auth/sim-users", async (IUserAuthorizationRepository userAuthorizationRepository) =>
            {
                return Results.Ok(await userAuthorizationRepository.GetSimulationLoginUsers());
            }).Produces<List<SimulationLoginUserOptionDto>>(StatusCodes.Status200OK);

            app.MapPost("/auth/sim-login", async (
                [FromBody] SimulationLoginRequest request,
                IUserAuthorizationRepository userAuthorizationRepository,
                IDevSimulationSessionStore sessionStore) =>
            {
                if (request.UserId is not > 0)
                {
                    return Results.BadRequest(new { message = "userId is required." });
                }

                var currentUser = await userAuthorizationRepository.ResolveCurrentUser(new ResolvedIdentity
                {
                    UserId = request.UserId,
                    ExternalKey = null,
                    Provider = "dev-sim-login"
                });

                if (currentUser is null)
                {
                    return Results.Unauthorized();
                }

                var identityKey = !string.IsNullOrWhiteSpace(currentUser.ExternalKey)
                    ? currentUser.ExternalKey!
                    : currentUser.Email;

                var session = sessionStore.CreateSession(currentUser.UserId, identityKey);

                return Results.Ok(new SimulationLoginResponse
                {
                    Token = session.Token,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                    User = EndpointSupport.ToMeDto(currentUser)
                });
            }).Produces<SimulationLoginResponse>(StatusCodes.Status200OK)
              .Produces(StatusCodes.Status400BadRequest)
              .Produces(StatusCodes.Status401Unauthorized);

            app.MapPost("/auth/sim-logout", (HttpContext httpContext, IDevSimulationSessionStore sessionStore) =>
            {
                var token = EndpointSupport.ExtractBearerToken(httpContext.Request.Headers.Authorization.FirstOrDefault());
                if (!string.IsNullOrWhiteSpace(token))
                {
                    sessionStore.RevokeSession(token);
                }

                return Results.NoContent();
            }).Produces(StatusCodes.Status204NoContent);
        }

        app.MapGet("/me", async (
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var currentUser = await userContext.GetCurrentUser();
            if (currentUser is null || !currentUser.IsActive)
            {
                return Results.Unauthorized();
            }

            if (!devSimulationEnabled && !authorizationPolicy.CanReadAllowedViews(currentUser))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(EndpointSupport.ToMeDto(currentUser));
        }).Produces<MeDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/auth/current-user", async (
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            if (devSimulationEnabled)
            {
                var currentUser = await userContext.GetCurrentUser();
                if (currentUser is null || !currentUser.IsActive)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(currentUser);
            }

            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanReadAllowedViews,
                "Leser role or higher is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(access.User);
        }).Produces<CurrentUser>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
