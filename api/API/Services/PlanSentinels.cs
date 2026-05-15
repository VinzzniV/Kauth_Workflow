using System.Text.Json;
using System.Text.Json.Nodes;

namespace API;

internal static class PlanSentinels
{
    // AD-Vorgänger noch nicht geplant — Status nicht bestimmbar
    public const string AdOutputNotYetKnown = "plan:ad-output-not-yet-known";

    // Vault: AD-Plan hat AlreadyExists=false bestätigt — Eintrag wird zur Ausführungszeit erzeugt
    public const string CredentialWillBeGenerated = "plan:will-be-generated";

    // Vault: AD-Vorgänger noch nicht geplant — Status nicht bestimmbar
    public const string CredentialStatusUnknown = "plan:credential-status-unknown";

    // SMTP: CreateMailboxGraph noch nicht gelaufen
    public const string SmtpAddressNotYetKnown = "plan:smtp-not-yet-known";

    public static bool IsSentinel(string? value) => value?.StartsWith("plan:") == true;

    // Baut ein JsonElement, das den created_ad_user-Eintrag im Resolver-Dict simuliert.
    // credVaultId: PlanSentinels.CredentialStatusUnknown (default pre-seed)
    //              oder PlanSentinels.CredentialWillBeGenerated (nach AlreadyExists=false)
    //              oder null (nach AlreadyExists=true)
    public static JsonElement BuildAdUserSentinelOutput(string? credVaultId)
    {
        var obj = new JsonObject
        {
            ["distinguishedName"] = AdOutputNotYetKnown,
            ["userPrincipalName"] = AdOutputNotYetKnown,
            ["samAccountName"] = AdOutputNotYetKnown,
            ["credentialVaultId"] = credVaultId
        };
        return JsonSerializer.Deserialize<JsonElement>(obj.ToJsonString());
    }

    public static JsonElement BuildMailboxSentinelOutput()
    {
        var obj = new JsonObject
        {
            ["primarySmtpAddress"] = SmtpAddressNotYetKnown
        };
        return JsonSerializer.Deserialize<JsonElement>(obj.ToJsonString());
    }
}
