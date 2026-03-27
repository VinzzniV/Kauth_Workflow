using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

namespace API;

internal static class LifecycleServiceCollectionExtensions
{
    /// <summary>
    /// Checks whether demo auth endpoints and resolvers should be active.
    /// Demo is disabled when DEMO_ENDPOINTS_ENABLED=false OR ASPNETCORE_ENVIRONMENT=Production.
    /// </summary>
    internal static bool IsDemoAuthActive()
    {
        var explicitlyDisabled = string.Equals(
            Environment.GetEnvironmentVariable("DEMO_ENDPOINTS_ENABLED"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        var isProduction = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        return !explicitlyDisabled && !isProduction;
    }

    /// <summary>
    /// Checks whether Entra ID authentication is enabled via ENTRA_AUTH_ENABLED=true.
    /// </summary>
    internal static bool IsEntraAuthEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("ENTRA_AUTH_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AddLifecycleApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsAllowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? ["http://localhost:5173"];

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Employee Lifecycle API", Version = "v1" });
        });
        services.AddScoped<IWorkflowRepository, PostgresWorkflowRepository>();
        services.AddHttpContextAccessor();

        // Identity resolver chain: Entra first (if enabled), then demo resolvers (if active).
        // In Production, demo resolvers are never registered.
        var demoActive = IsDemoAuthActive();
        var entraEnabled = IsEntraAuthEnabled();

        if (entraEnabled)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    jwtOptions =>
                    {
                        var audience = Environment.GetEnvironmentVariable("ENTRA_AUDIENCE");
                        if (!string.IsNullOrWhiteSpace(audience))
                        {
                            jwtOptions.Audience = audience;
                        }
                    },
                    identityOptions =>
                    {
                        identityOptions.Instance = "https://login.microsoftonline.com/";
                        var tenantId = Environment.GetEnvironmentVariable("ENTRA_TENANT_ID");
                        if (!string.IsNullOrWhiteSpace(tenantId))
                        {
                            identityOptions.TenantId = tenantId;
                        }
                        var clientId = Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID");
                        if (!string.IsNullOrWhiteSpace(clientId))
                        {
                            identityOptions.ClientId = clientId;
                        }
                    });

            services.AddAuthorization();
            services.AddScoped<IRequestIdentityResolver, EntraTokenIdentityResolver>();
        }

        if (demoActive)
        {
            services.AddSingleton<IDemoSessionStore, InMemoryDemoSessionStore>();
            services.AddScoped<IRequestIdentityResolver, DemoSessionTokenIdentityResolver>();
            services.AddScoped<IRequestIdentityResolver, DemoHeaderIdentityResolver>();
        }

        services.AddScoped<IIdentityProvider, IdentityProvider>();
        services.AddScoped<IUserAuthorizationRepository, PostgresUserAuthorizationRepository>();
        services.AddScoped<INotificationEmailConfigurationRepository, PostgresNotificationEmailConfigurationRepository>();
        services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
        services.AddScoped<IUserContext, CurrentUserContext>();
        services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();
        services.AddScoped<ISupervisorStepService, PostgresSupervisorStepService>();
        services.Configure<NotificationEmailOptions>(
            configuration.GetSection(NotificationEmailOptions.SectionName));
        services.AddScoped<INotificationEmailConfigurationService, NotificationEmailConfigurationService>();
        services.AddScoped<IWorkflowEmailNotificationSender, GraphWorkflowEmailNotificationSender>();
        services.AddScoped<INotificationEmailTestSender, GraphWorkflowEmailNotificationSender>();
        services.AddScoped<IDirectorySyncService, EntraDirectorySyncService>();
        services.AddHttpClient("health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddCors(options =>
        {
            options.AddPolicy("vite", policy =>
                policy.SetIsOriginAllowed(origin => IsAllowedFrontendOrigin(origin, corsAllowedOrigins))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
            );
        });

        return services;
    }

    private static bool IsAllowedFrontendOrigin(string? origin, IReadOnlyCollection<string> configuredOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Port is not (5173 or 4173))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        var bytes = ipAddress.GetAddressBytes();
        return ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               && (bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168));
    }
}
