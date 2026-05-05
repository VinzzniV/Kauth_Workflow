using Microsoft.AspNetCore.Http;

namespace API;

// P1-Hull (Z11-F1): einheitlicher Listenrahmen fuer Admin-/Master-Data-/Lookup-Reads.
// Items + Total + Limit + Offset, kein Cursor. Bewusst flach gehalten, damit FE
// jede Liste mit demselben Pattern anbinden kann und Seitenumbruch deterministisch
// ueber Offset funktioniert.
public sealed class AdminListPageDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
}

internal sealed class AdminListQuery
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public int Limit { get; init; }
    public int Offset { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }

    public string NormalizedSearch =>
        string.IsNullOrWhiteSpace(Search) ? string.Empty : Search.Trim();

    public string SearchPattern => $"%{NormalizedSearch}%";

    public static AdminListQuery From(HttpRequest request)
    {
        var query = request.Query;
        var limit = ParseInt(query["limit"]);
        var offset = ParseInt(query["offset"]);
        return new AdminListQuery
        {
            Limit = ClampLimit(limit ?? DefaultLimit),
            Offset = Math.Max(0, offset ?? 0),
            Search = query["search"].ToString(),
            Sort = query["sort"].ToString()
        };
    }

    private static int ClampLimit(int value) => Math.Clamp(value, 1, MaxLimit);

    private static int? ParseInt(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        return int.TryParse(text, out var parsed) ? parsed : null;
    }
}
