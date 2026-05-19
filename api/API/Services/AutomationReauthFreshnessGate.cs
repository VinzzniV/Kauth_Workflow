using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace API;

// Slice AGA-N2: Frische-Check des Access-Tokens vor Ausstellung des One-Shot-
// Re-Auth-Tokens. Backend-Quelle ist der `auth_time`-Claim aus dem validierten
// JWT (Microsoft Identity Web). Es wird ausschliesslich Server-Zeit gegen den
// Token-Claim verglichen — keine Frontend-Zeit, kein Soft-Success.
internal sealed class AutomationReauthFreshnessGate
{
    public static readonly TimeSpan MaxAuthTimeAge = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private const string AuthTimeClaimType = "auth_time";

    private readonly LifecycleRuntimeSettings runtimeSettings;
    private readonly IHttpContextAccessor httpContextAccessor;

    public AutomationReauthFreshnessGate(
        LifecycleRuntimeSettings runtimeSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        this.runtimeSettings = runtimeSettings;
        this.httpContextAccessor = httpContextAccessor;
    }

    public ReauthFreshnessResult Evaluate(DateTimeOffset utcNow)
    {
        if (!runtimeSettings.EntraAuthEnabled)
        {
            // dev-sim: keine echte Entra-Token-Quelle vorhanden. Bewusster
            // Fallback fuer lokale Entwicklung — in `Konfiguration.md` und
            // `Admin-Gated-Automation.md` offen dokumentiert.
            return ReauthFreshnessResult.SkippedDevSim();
        }

        var principal = httpContextAccessor.HttpContext?.User;
        var claim = principal?.FindFirst(AuthTimeClaimType);
        if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
        {
            return ReauthFreshnessResult.MissingClaim();
        }

        if (!long.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochSeconds))
        {
            return ReauthFreshnessResult.MissingClaim();
        }

        var authTime = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
        var age = utcNow - authTime;

        // Tokens duerfen leicht in der Zukunft liegen (Clock-Skew zwischen IdP und API).
        if (age < -ClockSkew)
        {
            return ReauthFreshnessResult.MissingClaim();
        }

        if (age > MaxAuthTimeAge + ClockSkew)
        {
            return ReauthFreshnessResult.Stale(authTime, age);
        }

        return ReauthFreshnessResult.Fresh(authTime);
    }
}

internal abstract record ReauthFreshnessResult
{
    public sealed record FreshResult(DateTimeOffset AuthTime) : ReauthFreshnessResult;
    public sealed record StaleResult(DateTimeOffset AuthTime, TimeSpan Age) : ReauthFreshnessResult;
    public sealed record MissingClaimResult : ReauthFreshnessResult;
    public sealed record SkippedDevSimResult : ReauthFreshnessResult;

    public static ReauthFreshnessResult Fresh(DateTimeOffset authTime) => new FreshResult(authTime);
    public static ReauthFreshnessResult Stale(DateTimeOffset authTime, TimeSpan age) => new StaleResult(authTime, age);
    public static ReauthFreshnessResult MissingClaim() => new MissingClaimResult();
    public static ReauthFreshnessResult SkippedDevSim() => new SkippedDevSimResult();
}
