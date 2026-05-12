namespace AdAutomationWorker.Core.Handlers;

// Schmale Wrapper-Registry, damit der HostedService Handler-Lookups nicht direkt auf die
// IEnumerable-DI-Auflistung macht. Dictionary-Lookup statt linearer Suche + saubere Fehlermeldung
// bei nicht registriertem ActionKey.
public sealed class HandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IWorkerHandler> handlersByActionKey;

    public HandlerRegistry(IEnumerable<IWorkerHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        handlersByActionKey = handlers.ToDictionary(h => h.ActionKey, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string actionKey, out IWorkerHandler handler)
    {
        if (handlersByActionKey.TryGetValue(actionKey, out var found))
        {
            handler = found;
            return true;
        }

        handler = null!;
        return false;
    }

    public IWorkerHandler Get(string actionKey)
    {
        if (TryGet(actionKey, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No worker handler registered for action_key '{actionKey}'.");
    }

    public IReadOnlyCollection<string> RegisteredActionKeys => handlersByActionKey.Keys.ToArray();
}
