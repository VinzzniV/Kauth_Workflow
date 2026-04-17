using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Http;

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
                    app.Logger.LogError(
                        exception,
                        "Unhandled exception while processing request {Method} {Path} (TraceIdentifier: {TraceIdentifier}).",
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);

                    try
                    {
                        var currentUserContext = context.RequestServices.GetRequiredService<IUserContext>();
                        var currentUser = await currentUserContext.GetCurrentUser(context.RequestAborted);
                        var systemEventLogService = context.RequestServices.GetRequiredService<ISystemEventLogService>();
                        await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                        {
                            Severity = "error",
                            Source = "api",
                            Category = "http",
                            EventKey = "unhandled_exception",
                            Message = $"Unhandled exception while processing request {context.Request.Method} {context.Request.Path}.",
                            UserMessage = "Ein unerwarteter Fehler ist aufgetreten.",
                            ActorUserId = currentUser?.UserId,
                            HttpMethod = context.Request.Method,
                            HttpPath = context.Request.Path,
                            HttpStatus = StatusCodes.Status500InternalServerError,
                            TraceIdentifier = context.TraceIdentifier,
                            ClientRoute = context.Request.Path,
                            Details = new
                            {
                                exceptionType = exception.GetType().FullName,
                                exception.Message,
                                stackTrace = exception.StackTrace
                            }
                        }, context.RequestAborted);
                    }
                    catch
                    {
                        // Keep the global exception handler resilient even if logging fails.
                    }
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
        app.MapLifecycleHealthEndpoints();
        app.MapAuthEndpoints();
        app.MapClientSystemLogEndpoints();
        app.MapAdminEndpoints();
        app.MapRotationPlanningEndpoints();
        app.MapWorkflowEndpoints();
        app.MapTaskEndpoints();

        return app;
    }

    public static IEndpointRouteBuilder MapLifecycleHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Json(
            new
            {
                status = "ok"
            },
            statusCode: StatusCodes.Status200OK)).WithTags("Operations");

        app.MapGet("/health/ready", async () =>
        {
            var runtimeSettings = app.ServiceProvider.GetRequiredService<LifecycleRuntimeSettings>();
            var databaseStatus = await CheckDatabaseStatusAsync(runtimeSettings.ConnectionString);
            var overallStatus = databaseStatus == "ok" ? "ok" : "degraded";
            var statusCode = databaseStatus == "ok"
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;

            return Results.Json(
                new
                {
                    status = overallStatus,
                    database = databaseStatus
                },
                statusCode: statusCode);
        }).WithTags("Operations");

        app.MapGet("/health", async (IHttpClientFactory httpClientFactory) =>
        {
            var runtimeSettings = app.ServiceProvider.GetRequiredService<LifecycleRuntimeSettings>();
            var entraEnabled = runtimeSettings.EntraAuthEnabled;
            var devSimulationActive = runtimeSettings.DevSimulationEnabled;
            var tenantId = runtimeSettings.EntraTenantId;
            var clientId = runtimeSettings.EntraClientId;
            var audience = runtimeSettings.EntraAudience;

            var databaseStatus = await CheckDatabaseStatusAsync(runtimeSettings.ConnectionString);
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
                statusCode: StatusCodes.Status200OK);
        }).WithTags("Operations");

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
            "/health/ready",
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

    private static async Task<string> CheckDatabaseStatusAsync(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "not_configured";
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync();
            return "ok";
        }
        catch
        {
            return "unreachable";
        }
    }
}
