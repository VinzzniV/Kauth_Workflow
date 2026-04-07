namespace API;

internal interface IWorkflowDefinitionValidationService
{
    string NormalizeDefinitionKey(string? definitionKey);
    WorkflowDefinitionDraftValidationResult ValidateAndNormalize(ReplaceWorkflowDefinitionVersionRequest request);
    WorkflowDefinitionValidationSnapshot ValidateSnapshot(WorkflowDefinitionValidationContext context);
}
