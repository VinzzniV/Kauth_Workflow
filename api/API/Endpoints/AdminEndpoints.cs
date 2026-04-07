using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAdminRuntimeConfigEndpoints();
        app.MapAdminOrgEndpoints();
        app.MapAdminDirectorySyncEndpoints();
        app.MapAdminWorkflowDefinitionConfigEndpoints();
        app.MapAdminWorkflowRuntimeEndpoints();
        app.MapAdminProcessConfigEndpoints();
        app.MapAdminAnswerConfigEndpoints();
        return app;
    }
}
