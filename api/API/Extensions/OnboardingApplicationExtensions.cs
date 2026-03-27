using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API;

internal static class LifecycleApplicationExtensions
{
    public static WebApplication ConfigureLifecycleApi(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (exception is not null)
                {
                    app.Logger.LogError(exception, "Unhandled exception while processing request {Method} {Path}.", context.Request.Method, context.Request.Path);
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Results.Problem(
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError)
                    .ExecuteAsync(context);
            });
        });

        app.UseCors("vite");

        if (LifecycleServiceCollectionExtensions.IsEntraAuthEnabled())
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.HasStarted)
            {
                return;
            }

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized
                && LifecycleServiceCollectionExtensions.IsEntraAuthEnabled()
                && !context.Response.Headers.ContainsKey("WWW-Authenticate"))
            {
                context.Response.Headers.Append("WWW-Authenticate", "Bearer");
            }
        });

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Lifecycle API v1");
            c.RoutePrefix = "swagger";
        });
        app.MapControllers();

        return app;
    }

    public static WebApplication MapLifecycleApiEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (IHttpClientFactory httpClientFactory) =>
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var entraEnabled = LifecycleServiceCollectionExtensions.IsEntraAuthEnabled();
            var demoActive = LifecycleServiceCollectionExtensions.IsDemoAuthActive();
            var tenantId = Environment.GetEnvironmentVariable("ENTRA_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID");
            var audience = Environment.GetEnvironmentVariable("ENTRA_AUDIENCE");

            var databaseStatus = "ok";
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                databaseStatus = "not_configured";
            }
            else
            {
                try
                {
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();
                    await using var command = new NpgsqlCommand("SELECT 1;", connection);
                    await command.ExecuteScalarAsync();
                }
                catch
                {
                    databaseStatus = "unreachable";
                }
            }

            var authStatus = "disabled";
            var authReachability = "not_applicable";
            if (entraEnabled)
            {
                if (string.IsNullOrWhiteSpace(tenantId)
                    || string.IsNullOrWhiteSpace(clientId)
                    || string.IsNullOrWhiteSpace(audience))
                {
                    authStatus = "misconfigured";
                    authReachability = "not_checked";
                }
                else
                {
                    authStatus = "enabled";
                    try
                    {
                        var client = httpClientFactory.CreateClient("health");
                        var openIdConfigUrl =
                            $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
                        using var response = await client.GetAsync(openIdConfigUrl);
                        authReachability = response.IsSuccessStatusCode ? "reachable" : "unreachable";
                    }
                    catch
                    {
                        authReachability = "unreachable";
                    }
                }
            }

            var overallStatus =
                databaseStatus == "ok"
                && (!entraEnabled || (authStatus == "enabled" && authReachability == "reachable"))
                    ? "ok"
                    : "degraded";

            var statusCode = overallStatus == "ok"
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;

            return Results.Json(
                new
                {
                    status = overallStatus,
                    database = databaseStatus,
                    auth = new
                    {
                        mode = entraEnabled ? "entra" : demoActive ? "demo" : "none",
                        status = authStatus,
                        reachability = authReachability,
                        demoActive
                    }
                },
                statusCode: statusCode);
        }).WithTags("Operations");

        app.MapAuthEndpoints();
        app.MapAdminEndpoints();
        app.MapWorkflowEndpoints();
        app.MapTaskEndpoints();

        return app;
    }
}
