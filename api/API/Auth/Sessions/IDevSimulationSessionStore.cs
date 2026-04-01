namespace API;

internal interface IDevSimulationSessionStore
{
    DevSimulationSession CreateSession(long userId, string identityKey, TimeSpan? lifetime = null);
    bool TryGetSession(string token, out DevSimulationSession? session);
    bool RevokeSession(string token);
}
