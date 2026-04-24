using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

namespace API;

internal static class LifecycleServiceCollectionExtensions
{
    internal static bool IsProductionEnvironment()
    {
        return LifecycleRuntimeSettingsResolver.ResolveFromEnvironment().IsProduction;
    }

    /// <summary>
    /// Checks whether development simulation auth endpoints and resolvers should be active.
    /// AUTH_MODE=dev-sim is the local development default.
    /// </summary>
    internal static bool IsDevelopmentSimulationActive()
    {
        return LifecycleRuntimeSettingsResolver.ResolveFromEnvironment().DevSimulationEnabled;
    }

    /// <summary>
    /// Checks whether Entra ID token authentication is enabled via AUTH_MODE=entra.
    /// </summary>
    internal static bool IsEntraAuthEnabled()
    {
        return LifecycleRuntimeSettingsResolver.ResolveFromEnvironment().EntraAuthEnabled;
    }

    public static IServiceCollection AddLifecycleApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var runtimeSettings = LifecycleRuntimeSettingsResolver.Resolve(configuration);
        if (runtimeSettings.IsProduction
            && !string.Equals(runtimeSettings.AuthMode, "entra", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ASPNETCORE_ENVIRONMENT=Production requires AUTH_MODE=entra.");
        }

        var corsAllowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?.Select(NormalizeConfiguredOrigin)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? ["http://localhost:5173"];
        var allowDevelopmentOriginFallback = !runtimeSettings.IsProduction;

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Employee Lifecycle API", Version = "v1" });
        });
        services.AddScoped<IWorkflowRepository, PostgresWorkflowRepository>();
        services.AddScoped<IRotationRepository, PostgresRotationRepository>();
        services.AddScoped<IWorkflowDefinitionRuntimeRepository, PostgresWorkflowRepository>();
        services.AddScoped<IWorkflowAutomationRepository, PostgresWorkflowRepository>();
        services.AddScoped<IWorkflowDefinitionValidationService, WorkflowDefinitionValidationService>();
        services.AddHttpContextAccessor();
        services.AddSingleton(runtimeSettings);

        // Identity resolver chain: Entra first (if enabled), then development simulation session.
        // In Production, only Entra token auth is allowed.
        var devSimulationActive = runtimeSettings.DevSimulationEnabled;
        var entraEnabled = runtimeSettings.EntraAuthEnabled;

        if (entraEnabled)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    jwtOptions =>
                    {
                        if (!string.IsNullOrWhiteSpace(runtimeSettings.EntraAudience))
                        {
                            jwtOptions.Audience = runtimeSettings.EntraAudience;
                        }
                    },
                    identityOptions =>
                    {
                        identityOptions.Instance = "https://login.microsoftonline.com/";
                        if (!string.IsNullOrWhiteSpace(runtimeSettings.EntraTenantId))
                        {
                            identityOptions.TenantId = runtimeSettings.EntraTenantId;
                        }
                        if (!string.IsNullOrWhiteSpace(runtimeSettings.EntraClientId))
                        {
                            identityOptions.ClientId = runtimeSettings.EntraClientId;
                        }
                    });

            services.AddAuthorization();
            services.AddScoped<IRequestIdentityResolver, EntraTokenIdentityResolver>();
        }

        if (devSimulationActive)
        {
            services.AddSingleton<IDevSimulationSessionStore, InMemoryDevSimulationSessionStore>();
            services.AddScoped<IRequestIdentityResolver, DevSimulationSessionTokenIdentityResolver>();
        }

        services.AddScoped<IIdentityProvider, IdentityProvider>();
        services.AddScoped<IUserAuthorizationRepository, PostgresUserAuthorizationRepository>();
        services.AddScoped<INotificationEmailConfigurationRepository, PostgresNotificationEmailConfigurationRepository>();
        services.AddScoped<INotificationTemplateRepository, PostgresNotificationTemplateRepository>();
        services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
        services.AddScoped<IUserContext, CurrentUserContext>();
        services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();
        services.AddScoped<IWorkflowVisibilityService, WorkflowVisibilityService>();
        services.AddScoped<IWorkflowNotificationDispatchService, WorkflowNotificationDispatchService>();
        services.AddScoped<IWorkflowCatalogService, WorkflowCatalogService>();
        services.AddScoped<IPersonLifecycleProjectionService, PersonLifecycleProjectionService>();
        services.AddScoped<ISystemEventLogService, SystemEventLogService>();
        services.AddScoped<IRotationPlanningService, RotationPlanningService>();
        services.AddScoped<IRotationTemplateAdminService, RotationTemplateAdminService>();
        services.AddScoped<IRotationTaskGenerationService, RotationTaskGenerationService>();
        services.AddScoped<IRotationNotificationService, RotationNotificationService>();
        services.AddScoped<IWorkflowRuntimeService, WorkflowRuntimeService>();
        services.AddScoped<IWorkflowDefinitionRuntimeService, WorkflowDefinitionRuntimeService>();
        services.AddScoped<IWorkflowAutomationService, WorkflowAutomationService>();
        services.AddSingleton<IWorkflowAutomationActionHandler, CreateAdUserAutomationHandler>();
        services.AddSingleton<IWorkflowAutomationActionHandler, CreateMailboxAutomationHandler>();
        services.AddSingleton<IWorkflowAutomationActionHandler, AssignGroupsAutomationHandler>();
        services.AddSingleton<IWorkflowAutomationActionHandler, CreateErpEmployeeAutomationHandler>();
        services.AddSingleton<IWorkflowAutomationActionHandler, SendWelcomeMailAutomationHandler>();
        services.AddSingleton<IWorkflowAutomationHandlerRegistry, WorkflowAutomationHandlerRegistry>();
        services.AddScoped<ITaskApplicationService, TaskApplicationService>();
        services.AddScoped<ISupervisorStepService, PostgresSupervisorStepService>();
        services.Configure<NotificationEmailOptions>(
            configuration.GetSection(NotificationEmailOptions.SectionName));
        services.AddScoped<IGraphApplicationConfigurationService, GraphApplicationConfigurationService>();
        services.AddScoped<INotificationEmailConfigurationService, NotificationEmailConfigurationService>();
        services.AddScoped<INotificationTemplatePreviewRepository>(sp =>
            (INotificationTemplatePreviewRepository)sp.GetRequiredService<IWorkflowRepository>());
        services.AddScoped<IRotationNotificationPreviewRepository>(sp =>
            (IRotationNotificationPreviewRepository)sp.GetRequiredService<IRotationRepository>());
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IWorkflowEmailNotificationSender, GraphWorkflowEmailNotificationSender>();
        services.AddScoped<INotificationEmailTestSender, GraphWorkflowEmailNotificationSender>();
        services.AddScoped<IDirectorySyncService, EntraDirectorySyncService>();
        services.AddHostedService<DirectorySyncHostedService>();
        services.AddHostedService<WorkflowAutomationHostedService>();
        services.AddHostedService<RotationNotificationHostedService>();
        services.AddHttpClient("health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddCors(options =>
        {
            options.AddPolicy("vite", policy =>
                policy.SetIsOriginAllowed(origin => IsAllowedFrontendOrigin(origin, corsAllowedOrigins, allowDevelopmentOriginFallback))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
            );
        });

        return services;
    }

    private static string? NormalizeConfiguredOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        var trimmedOrigin = origin.Trim();
        if (!Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out var uri))
        {
            return trimmedOrigin;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static bool IsAllowedFrontendOrigin(
        string? origin,
        IReadOnlyCollection<string> configuredOrigins,
        bool allowDevelopmentOriginFallback)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var normalizedOrigin = NormalizeConfiguredOrigin(origin);
        if (normalizedOrigin is not null
            && configuredOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowDevelopmentOriginFallback)
        {
            return false;
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
