using Microsoft.AspNetCore.Builder;

namespace API;

internal static class OnboardingApplicationExtensions
{
    public static WebApplication ConfigureOnboardingApi(this WebApplication app)
    {
        app.UseCors("vite");
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "RoleTaskPerms API v1");
            c.RoutePrefix = "swagger";
        });
        app.MapControllers();

        return app;
    }

    public static WebApplication MapOnboardingApiEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints();
        app.MapAdminEndpoints();
        app.MapWorkflowEndpoints();
        app.MapTaskEndpoints();

        return app;
    }
}
