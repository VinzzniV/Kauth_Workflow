using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace API;

internal static class LifecycleServiceCollectionExtensions
{
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
        services.AddSingleton<IDemoSessionStore, InMemoryDemoSessionStore>();
        // Current demo identity chain.
        // TODO(real-auth): add an Entra/Windows/SSO resolver implementation here
        // and register it before/alongside demo resolvers.
        services.AddScoped<IRequestIdentityResolver, DemoSessionTokenIdentityResolver>();
        services.AddScoped<IRequestIdentityResolver, DemoHeaderIdentityResolver>();
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
