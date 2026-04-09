using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

namespace API;

internal static class AdminWorkflowDefinitionConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminWorkflowDefinitionConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/workflow-definitions", async (
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowBuilder,
                "Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await repository.GetAdminWorkflowDefinitions());
        }).Produces<List<WorkflowDefinitionSummaryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/action-definitions", async (
            [FromServices] IWorkflowAutomationService automationService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await automationService.GetActionDefinitionsAsync());
        }).Produces<List<ActionDefinitionDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definitions", async (
            [FromBody] CreateWorkflowDefinitionRequest request,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                return Results.Ok(await repository.CreateAdminWorkflowDefinition(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionSummaryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/workflow-definitions/{definitionId:int}", async (
            int definitionId,
            [FromBody] UpdateWorkflowDefinitionRequest request,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var updated = await repository.UpdateAdminWorkflowDefinition(definitionId, request);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow definition not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionSummaryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/config/workflow-definitions/{definitionId:int}", async (
            int definitionId,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var deleted = await repository.DeleteAdminWorkflowDefinition(definitionId);
                return deleted
                    ? Results.NoContent()
                    : Results.NotFound(new { message = "Workflow definition not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definitions/{definitionId:int}/versions", async (
            int definitionId,
            [FromBody] CreateWorkflowDefinitionVersionRequest request,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var created = await repository.CreateAdminWorkflowDefinitionVersion(definitionId, request);
                return created is null
                    ? Results.NotFound(new { message = "Workflow definition not found." })
                    : Results.Created($"/admin/config/workflow-definition-versions/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionSummaryDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definitions/{definitionId:int}/working-draft", async (
            int definitionId,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowBuilder,
                "Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var version = await repository.GetOrCreateAdminWorkflowDefinitionWorkingDraft(definitionId);
                return version is null
                    ? Results.NotFound(new { message = "Workflow definition not found." })
                    : Results.Ok(version);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/workflow-definition-versions/{versionId:long}", async (
            long versionId,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowBuilder,
                "Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var version = await repository.GetAdminWorkflowDefinitionVersion(versionId);
                return version is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(version);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPut("/admin/config/workflow-definition-versions/{versionId:long}", async (
            long versionId,
            [FromBody] ReplaceWorkflowDefinitionVersionRequest request,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowBuilder,
                "Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                if (!authorizationPolicy.CanManageWorkflowBuilderAdvanced(access.User!))
                {
                    var existingVersion = await repository.GetAdminWorkflowDefinitionVersion(versionId);
                    if (existingVersion is null)
                    {
                        return Results.NotFound(new { message = "Workflow definition version not found." });
                    }

                    if (HasRestrictedAutomationChanges(existingVersion, request))
                    {
                        return EndpointSupport.Forbidden(
                            "Automation nodes and actions require advanced Workflow Builder access.");
                    }
                }

                var updated = await repository.ReplaceAdminWorkflowDefinitionVersion(versionId, request);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definition-versions/{versionId:long}/publish", async (
            long versionId,
            [FromServices] IWorkflowDefinitionRuntimeRepository runtimeRepository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageWorkflowBuilderAdvanced,
                "Advanced Workflow Builder access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var published = await runtimeRepository.PublishWorkflowDefinitionVersion(versionId);
                return published is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(published);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static bool HasRestrictedAutomationChanges(
        WorkflowDefinitionVersionDetailDto existingVersion,
        ReplaceWorkflowDefinitionVersionRequest request)
    {
        var existingNodesByKey = existingVersion.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeKey))
            .ToDictionary(
                node => node.NodeKey!.Trim().ToLowerInvariant(),
                node => node,
                StringComparer.OrdinalIgnoreCase);
        var requestNodesByKey = request.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeKey))
            .ToDictionary(
                node => node.NodeKey!.Trim().ToLowerInvariant(),
                node => node,
                StringComparer.OrdinalIgnoreCase);

        foreach (var requestNode in request.Nodes)
        {
            var requestNodeType = Normalize(requestNode.NodeType);
            if (!string.Equals(requestNodeType, "automation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var requestKey = Normalize(requestNode.NodeKey);
            if (requestKey is null
                || !existingNodesByKey.TryGetValue(requestKey, out var existingNode)
                || !string.Equals(Normalize(existingNode.NodeType), "automation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!ActionsEquivalent(existingNode.Actions, requestNode.Actions))
            {
                return true;
            }
        }

        foreach (var existingNode in existingVersion.Nodes)
        {
            var existingNodeType = Normalize(existingNode.NodeType);
            if (!string.Equals(existingNodeType, "automation", StringComparison.OrdinalIgnoreCase))
            {
                var existingKey = Normalize(existingNode.NodeKey);
                if (existingKey is not null
                    && requestNodesByKey.TryGetValue(existingKey, out var requestNode)
                    && string.Equals(Normalize(requestNode.NodeType), "automation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            var existingKeyNormalized = Normalize(existingNode.NodeKey);
            if (existingKeyNormalized is null
                || !requestNodesByKey.TryGetValue(existingKeyNormalized, out var matchingRequestNode)
                || !string.Equals(Normalize(matchingRequestNode.NodeType), "automation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!ActionsEquivalent(existingNode.Actions, matchingRequestNode.Actions))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ActionsEquivalent(
        IReadOnlyList<WorkflowNodeActionDto> existingActions,
        IReadOnlyList<WorkflowNodeActionDto> requestActions)
    {
        if (existingActions.Count != requestActions.Count)
        {
            return false;
        }

        var normalizedExisting = existingActions
            .Select(NormalizeAction)
            .OrderBy(item => item)
            .ToArray();
        var normalizedRequest = requestActions
            .Select(NormalizeAction)
            .OrderBy(item => item)
            .ToArray();

        return normalizedExisting.SequenceEqual(normalizedRequest, StringComparer.Ordinal);
    }

    private static string NormalizeAction(WorkflowNodeActionDto action)
    {
        return string.Join("|", new[]
        {
            Normalize(action.ActionKey) ?? string.Empty,
            action.ExecutionOrder.ToString(),
            Normalize(action.OnErrorBehavior) ?? string.Empty,
            NormalizeJson(action.InputMapping),
        });
    }

    private static string NormalizeJson(JsonElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(element.Value);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
