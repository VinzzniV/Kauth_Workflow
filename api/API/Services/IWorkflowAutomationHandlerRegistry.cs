namespace API;

internal interface IWorkflowAutomationHandlerRegistry
{
    IReadOnlyCollection<string> GetRegisteredKeys();
    IWorkflowAutomationActionHandler Resolve(string actionKey);
}
