using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAdminRuntimeConfigEndpoints();
        app.MapAdminNotificationTemplateEndpoints();
        app.MapAdminOrgEndpoints();
        app.MapAdminSystemLogEndpoints();
        app.MapAdminSystemConfigEndpoints();
        app.MapAdminRotationConfigEndpoints();
        app.MapAdminDirectorySyncEndpoints();
        app.MapAdminWorkflowDefinitionConfigEndpoints();
        app.MapAdminWorkflowRuntimeEndpoints();
        app.MapAdminProcessConfigEndpoints();
        app.MapAdminAnswerConfigEndpoints();
        app.MapAdminRuntimeHealthEndpoints();
        app.MapAdminPeopleEndpoints();
        app.MapAdminAutomationPlanEndpoints();
        app.MapAdminAutomationApprovalEndpoints();
        return app;
    }
}
