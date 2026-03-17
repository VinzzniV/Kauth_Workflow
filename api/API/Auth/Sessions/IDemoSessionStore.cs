namespace API;

internal interface IDemoSessionStore
{
    DemoSession CreateSession(long userId, string identityKey, TimeSpan? lifetime = null);
    bool TryGetSession(string token, out DemoSession? session);
    bool RevokeSession(string token);
}
