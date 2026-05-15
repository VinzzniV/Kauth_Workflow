using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace API;

// Slice 3 (Admin-Gated-Automation): Kanonischer SHA-256-Hash über ein NodePlanResult.
// Wird im /admin/automation/plan-Endpoint (zur Auslieferung) und im /admin/automation/
// approve-Endpoint (zum Drift-Check) verwendet — Konsistenz ist per Code-Pfad
// garantiert, nicht per JSON-Spec.
//
// Kanonisierung: Object-Keys werden rekursiv lexikografisch sortiert; Array-Order bleibt
// (Reihenfolge der Steps und PlannedGroups ist semantisch). null-/undefined-Felder werden
// in den Hash aufgenommen, weil die Plan-Records init-Defaults haben.
internal static class PlanHash
{
    public const string Algorithm = "SHA-256";

    public static string ComputeHash(NodePlanResult plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var json = JsonSerializer.SerializeToNode(plan);
        var canonical = CanonicalizeJson(json);
        var canonicalJson = canonical?.ToJsonString(SerializerOptions) ?? "null";
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private static JsonNode? CanonicalizeJson(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
            {
                var sorted = new JsonObject();
                foreach (var key in obj.Select(kvp => kvp.Key).OrderBy(k => k, StringComparer.Ordinal))
                {
                    sorted[key] = CanonicalizeJson(obj[key]);
                }
                return sorted;
            }
            case JsonArray arr:
            {
                var canonical = new JsonArray();
                foreach (var item in arr)
                {
                    canonical.Add(CanonicalizeJson(item));
                }
                return canonical;
            }
            default:
                // JsonValue (primitives) — deep-copy via re-parsing so JsonNode ownership is clean.
                return JsonNode.Parse(node.ToJsonString(SerializerOptions));
        }
    }
}
