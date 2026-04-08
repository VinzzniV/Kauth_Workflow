namespace API;

internal interface IWorkflowAutomationHandlerRegistry
{
    IWorkflowAutomationActionHandler Resolve(string actionKey);
}
