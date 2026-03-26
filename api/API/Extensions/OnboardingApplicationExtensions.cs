using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
        app.MapAuthEndpoints();
        app.MapAdminEndpoints();
        app.MapWorkflowEndpoints();
        app.MapTaskEndpoints();

        return app;
    }
}
