using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API;

internal static class LifecycleApplicationExtensions
{
    public static WebApplication ConfigureLifecycleApi(this WebApplication app)
    {
        var runtimeSettings = app.Services.GetRequiredService<LifecycleRuntimeSettings>();

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

        if (runtimeSettings.EntraAuthEnabled)
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
                && runtimeSettings.EntraAuthEnabled
                && !context.Response.Headers.ContainsKey("WWW-Authenticate"))
            {
                context.Response.Headers.Append("WWW-Authenticate", "Bearer");
            }
        });

        if (runtimeSettings.SwaggerEnabled)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Lifecycle API v1");
                c.RoutePrefix = "swagger";
            });
        }

        return app;
    }

    public static WebApplication MapLifecycleApiEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Json(
            new
            {
                status = "ok"
            },
            statusCode: StatusCodes.Status200OK)).WithTags("Operations");

        app.MapGet("/health", async (IHttpClientFactory httpClientFactory) =>
        {
            var runtimeSettings = app.Services.GetRequiredService<LifecycleRuntimeSettings>();
            var connectionString = runtimeSettings.ConnectionString;
            var entraEnabled = runtimeSettings.EntraAuthEnabled;
            var devSimulationActive = runtimeSettings.DevSimulationEnabled;
            var tenantId = runtimeSettings.EntraTenantId;
            var clientId = runtimeSettings.EntraClientId;
            var audience = runtimeSettings.EntraAudience;

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
                        mode = entraEnabled ? "entra" : devSimulationActive ? "dev-sim" : "none",
                        status = authStatus,
                        reachability = authReachability,
                        devSimulationActive
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

    public static WebApplication ValidateLifecycleRouteRegistration(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var routeBuilder = (IEndpointRouteBuilder)app;
        var routeEndpoints = routeBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
            .ToArray();

        if (routeEndpoints.Length == 0)
        {
            throw new InvalidOperationException(
                "Application started without any mapped route endpoints. " +
                "Minimal API route registration failed.");
        }

        var routePatterns = routeEndpoints
            .Select(endpoint => NormalizeRoutePattern(endpoint.RoutePattern.RawText!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Minimal API mode active. Registered {RouteCount} route endpoints.",
            routePatterns.Length);

        var requiredRoutes = new[]
        {
            "/health",
            "/health/live",
            "/me",
            "/auth/current-user"
        };

        var missingRoutes = requiredRoutes
            .Where(requiredRoute => !routePatterns.Contains(requiredRoute, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missingRoutes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Critical API routes are missing: {string.Join(", ", missingRoutes)}.");
        }

        logger.LogInformation(
            "Startup validation passed: critical API routes are registered ({Routes}).",
            string.Join(", ", requiredRoutes));

        return app;
    }

    private static string NormalizeRoutePattern(string rawText)
    {
        var trimmed = rawText.Trim();
        if (trimmed.Length == 0)
        {
            return "/";
        }

        return trimmed.StartsWith("/", StringComparison.Ordinal)
            ? trimmed
            : $"/{trimmed}";
    }
}
