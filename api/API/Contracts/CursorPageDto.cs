using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace API;

// P2-Hull (Z11-F2): Cursor-Stream fuer zeitlich geordnete, unbegrenzte Listen (Audit-Logs).
// Kein total — bei Streams wuerde das jede Seite eine COUNT(*)-Query erzwingen.
// Cursor ist opaque Base64-JSON ueber (occurredAt, id), damit das FE ihn nie zerlegenm muss.
public sealed class CursorPageDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public string? NextCursor { get; init; }
    public required bool HasMore { get; init; }
}

internal sealed class CursorPageQuery
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public int Limit { get; init; }
    public string? Cursor { get; init; }

    public (DateTime CreatedAt, long Id)? DecodedCursor => TryDecodeCursor(Cursor);

    public static CursorPageQuery From(HttpRequest request)
    {
        var query = request.Query;
        var rawLimit = query["limit"].ToString();
        var rawCursor = query["cursor"].ToString();
        var limit = int.TryParse(rawLimit, out var parsed) ? parsed : DefaultLimit;
        return new CursorPageQuery
        {
            Limit = Math.Clamp(limit, 1, MaxLimit),
            Cursor = string.IsNullOrWhiteSpace(rawCursor) ? null : rawCursor
        };
    }

    public static string EncodeCursor(DateTime createdAt, long id)
    {
        var payload = JsonSerializer.Serialize(new CursorPayload
        {
            OccurredAt = createdAt.ToUniversalTime().ToString("O"),
            Id = id
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static (DateTime CreatedAt, long Id)? TryDecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload is null || string.IsNullOrWhiteSpace(payload.OccurredAt))
            {
                return null;
            }

            if (!DateTime.TryParse(
                    payload.OccurredAt,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var ts))
            {
                return null;
            }

            return (ts.ToUniversalTime(), payload.Id);
        }
        catch
        {
            return null;
        }
    }

    private sealed class CursorPayload
    {
        public string OccurredAt { get; set; } = "";
        public long Id { get; set; }
    }
}
