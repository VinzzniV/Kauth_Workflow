using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace API;

internal sealed class WorkflowListResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ApiDescription.RelativePath, "workflows", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var responses = operation.Responses;
        if (responses is null
            || !responses.TryGetValue(StatusCodes.Status200OK.ToString(), out var response)
            || response?.Content is null
            || !response.Content.TryGetValue("application/json", out var content))
        {
            return;
        }

        var workflowListItemSchema = context.SchemaGenerator.GenerateSchema(
            typeof(WorkflowListItemDto),
            context.SchemaRepository);
        var workflowListPageSchema = context.SchemaGenerator.GenerateSchema(
            typeof(WorkflowListPageDto),
            context.SchemaRepository);

        content.Schema = new OpenApiSchema
        {
            OneOf =
            [
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = workflowListItemSchema
                },
                workflowListPageSchema
            ]
        };
    }
}
