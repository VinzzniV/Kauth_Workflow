namespace API;

internal sealed class WorkflowAutomationHandlerRegistry : IWorkflowAutomationHandlerRegistry
{
    private readonly Dictionary<string, IWorkflowAutomationActionHandler> _handlers;

    public WorkflowAutomationHandlerRegistry(IEnumerable<IWorkflowAutomationActionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.ActionKey, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> GetRegisteredKeys()
    {
        return _handlers.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IWorkflowAutomationActionHandler Resolve(string actionKey)
    {
        if (_handlers.TryGetValue(actionKey, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No automation handler is registered for action '{actionKey}'.");
    }
}
