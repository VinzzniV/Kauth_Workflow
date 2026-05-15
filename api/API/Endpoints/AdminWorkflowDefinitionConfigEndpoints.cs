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
            HttpRequest request,
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

            var query = AdminListQuery.From(request);
            return Results.Ok(await repository.GetAdminWorkflowDefinitions(query));
        }).Produces<AdminListPageDto<WorkflowDefinitionSummaryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/action-definitions", async (
            HttpRequest request,
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

            var query = AdminListQuery.From(request);
            return Results.Ok(await automationService.GetActionDefinitionsAsync(query));
        }).Produces<AdminListPageDto<ActionDefinitionDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/automation-property-catalog", async (
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

            return Results.Ok(AutomationPropertyCatalog.BuildDto());
        }).Produces<AutomationPropertyCatalogDto>(StatusCodes.Status200OK)
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
                var version = await repository.EnsureAdminWorkflowDefinitionWorkingDraft(definitionId);
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
            catch (WorkflowDefinitionVersionStaleException ex)
            {
                return Results.Conflict(new WorkflowDefinitionVersionConflictDto
                {
                    Message = ex.Message,
                    CurrentUpdatedAt = ex.CurrentUpdatedAt,
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces<WorkflowDefinitionVersionConflictDto>(StatusCodes.Status409Conflict)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definition-versions/{versionId:long}/publish", async (
            long versionId,
            [FromServices] IWorkflowRepository repository,
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
                var version = await repository.GetAdminWorkflowDefinitionVersion(versionId);
                if (version is null)
                {
                    return Results.NotFound(new { message = "Workflow definition version not found." });
                }

                if (!version.CanPublish)
                {
                    var issueMessages = version.ValidationIssues
                        .Select(i => i.Message)
                        .ToList();
                    return Results.BadRequest(new
                    {
                        message = "Workflow definition version cannot be published due to validation errors.",
                        issues = issueMessages
                    });
                }

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

        app.MapGet("/admin/config/workflow-definitions/{workflowDefinitionId:int}/dependency-graph", async (
            int workflowDefinitionId,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                return Results.Ok(await repository.GetAdminDependencyGraph(workflowDefinitionId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDependencyGraphDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
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

        // Slice 2: Actions sind jetzt sowohl an 'automation'- als auch an 'task'-Nodes
        // sicherheitskritisch — jeder spaeter admin-getriggert ausgefuehrte Plan haengt
        // an dieser Konfiguration. Daher behandelt der Gate beide Typen einheitlich.
        // Drift in entweder Actions ODER automation_admin_role gilt als restricted.

        // Request-Seite: jeder Action-tragende Node muss mit identischem Pendant
        // auf der Existing-Seite uebereinstimmen.
        foreach (var requestNode in request.Nodes)
        {
            if (!IsActionBearingNodeType(requestNode.NodeType))
            {
                continue;
            }

            var requestKey = Normalize(requestNode.NodeKey);
            if (requestKey is null
                || !existingNodesByKey.TryGetValue(requestKey, out var existingNode)
                || !IsActionBearingNodeType(existingNode.NodeType)
                || !string.Equals(Normalize(existingNode.NodeType), Normalize(requestNode.NodeType), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!ActionsEquivalent(existingNode.Actions, requestNode.Actions))
            {
                return true;
            }

            if (!AutomationAdminRoleEquivalent(existingNode.AutomationAdminRole, requestNode.AutomationAdminRole))
            {
                return true;
            }
        }

        // Existing-Seite: jeder existierende Action-tragende Node muss im Request mit
        // identischem Typ und identischen Actions/Role wieder auftauchen — sonst entfernt
        // bzw. veraendert ein non-Advanced-User automation-relevanten Zustand.
        foreach (var existingNode in existingVersion.Nodes)
        {
            if (!IsActionBearingNodeType(existingNode.NodeType))
            {
                // Defensive: ein non-action-Node, der zu einem Action-Node mutiert, ist
                // bereits durch die Request-Seite oben abgefangen — hier kein Re-Check.
                continue;
            }

            var existingKeyNormalized = Normalize(existingNode.NodeKey);
            if (existingKeyNormalized is null
                || !requestNodesByKey.TryGetValue(existingKeyNormalized, out var matchingRequestNode)
                || !IsActionBearingNodeType(matchingRequestNode.NodeType)
                || !string.Equals(Normalize(matchingRequestNode.NodeType), Normalize(existingNode.NodeType), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!ActionsEquivalent(existingNode.Actions, matchingRequestNode.Actions))
            {
                return true;
            }

            if (!AutomationAdminRoleEquivalent(existingNode.AutomationAdminRole, matchingRequestNode.AutomationAdminRole))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActionBearingNodeType(string? nodeType)
    {
        return WorkflowDefinitionValidationCatalog.AllowsActions(Normalize(nodeType));
    }

    private static bool AutomationAdminRoleEquivalent(string? left, string? right)
    {
        var leftNormalized = WorkflowDefinitionValidationHelpers.NormalizeAutomationAdminRole(left);
        var rightNormalized = WorkflowDefinitionValidationHelpers.NormalizeAutomationAdminRole(right);
        return string.Equals(leftNormalized, rightNormalized, StringComparison.Ordinal);
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
