using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace API;

internal sealed class InMemoryDemoSessionStore : IDemoSessionStore
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, DemoSession> _sessions = new(StringComparer.Ordinal);

    public DemoSession CreateSession(long userId, string identityKey, TimeSpan? lifetime = null)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("User ID is required to create a demo session.");
        }

        var normalizedIdentityKey = identityKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdentityKey))
        {
            throw new InvalidOperationException("Identity key is required to create a demo session.");
        }

        var token = CreateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime ?? DefaultLifetime);

        var session = new DemoSession
        {
            Token = token,
            UserId = userId,
            IdentityKey = normalizedIdentityKey,
            ExpiresAtUtc = expiresAtUtc
        };

        _sessions[token] = session;
        return session;
    }

    public bool TryGetSession(string token, out DemoSession? session)
    {
        session = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!_sessions.TryGetValue(token, out var existingSession))
        {
            return false;
        }

        if (existingSession.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        session = existingSession;
        return true;
    }

    public bool RevokeSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _sessions.TryRemove(token, out _);
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
