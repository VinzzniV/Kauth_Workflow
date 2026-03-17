using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/demo-users", async (IUserAuthorizationRepository userAuthorizationRepository) =>
        {
            return Results.Ok(await userAuthorizationRepository.GetDemoLoginUsers());
        }).Produces<List<DemoLoginUserOptionDto>>(StatusCodes.Status200OK);

        app.MapPost("/auth/demo-login", async (
            [FromBody] DemoLoginRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IDemoSessionStore sessionStore,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var username = request.Username?.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                return Results.BadRequest(new { message = "username is required." });
            }

            var currentUser = await userAuthorizationRepository.ResolveCurrentUser(new ResolvedIdentity
            {
                UserId = null,
                ExternalKey = username,
                Provider = "demo-login"
            });

            if (currentUser is null || !currentUser.IsActive)
            {
                return Results.Unauthorized();
            }

            if (!authorizationPolicy.CanReadAllowedViews(currentUser))
            {
                return Results.BadRequest(new { message = "Selected user has no application role for this demo." });
            }

            var identityKey = !string.IsNullOrWhiteSpace(currentUser.ExternalKey)
                ? currentUser.ExternalKey!
                : currentUser.Email;

            var session = sessionStore.CreateSession(currentUser.UserId, identityKey);

            return Results.Ok(new DemoLoginResponse
            {
                Token = session.Token,
                ExpiresAtUtc = session.ExpiresAtUtc,
                User = EndpointSupport.ToMeDto(currentUser)
            });
        }).Produces<DemoLoginResponse>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/demo-logout", (HttpContext httpContext, IDemoSessionStore sessionStore) =>
        {
            var token = EndpointSupport.ExtractBearerToken(httpContext.Request.Headers.Authorization.FirstOrDefault());
            if (!string.IsNullOrWhiteSpace(token))
            {
                sessionStore.RevokeSession(token);
            }

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        app.MapGet("/me", async (IUserContext userContext) =>
        {
            var currentUser = await userContext.GetCurrentUser();
            if (currentUser is null || !currentUser.IsActive)
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
