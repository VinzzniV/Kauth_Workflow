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
            c.OperationFilter<WorkflowListResponseOperationFilter>();
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
                policy.WithOrigins(corsAllowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
            );
        });

        return services;
    }
}
