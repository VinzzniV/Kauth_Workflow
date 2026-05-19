using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace API.Tests;

// Slice AGA-N2: Frische-Check des auth_time-Claims vor Ausstellung des
// One-Shot-Re-Auth-Tokens. Tests halten die Sicherheits-Schranke fest:
// fehlender Claim oder zu alter Login muss zu strukturiertem Fehler fuehren,
// dev-sim umgeht die Schranke bewusst.
public sealed class AutomationReauthFreshnessGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_DevSimMode_SkipsCheck()
    {
        var gate = BuildGate(entraAuthEnabled: false, authTime: null);

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.SkippedDevSimResult>(result);
    }

    [Fact]
    public void Evaluate_EntraMode_FreshClaim_ReturnsFresh()
    {
        var authTime = Now - TimeSpan.FromSeconds(30);
        var gate = BuildGate(entraAuthEnabled: true, authTime: authTime);

        var result = gate.Evaluate(Now);

        var fresh = Assert.IsType<ReauthFreshnessResult.FreshResult>(result);
        Assert.Equal(authTime, fresh.AuthTime);
    }

    [Fact]
    public void Evaluate_EntraMode_AtMaxAge_StillFresh()
    {
        // Exact MaxAge (120s) plus Skew (30s) liegt noch innerhalb des Fensters.
        var authTime = Now - (AutomationReauthFreshnessGate.MaxAuthTimeAge + AutomationReauthFreshnessGate.ClockSkew);
        var gate = BuildGate(entraAuthEnabled: true, authTime: authTime);

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.FreshResult>(result);
    }

    [Fact]
    public void Evaluate_EntraMode_StaleClaim_ReturnsStale()
    {
        var authTime = Now - (AutomationReauthFreshnessGate.MaxAuthTimeAge + AutomationReauthFreshnessGate.ClockSkew + TimeSpan.FromSeconds(1));
        var gate = BuildGate(entraAuthEnabled: true, authTime: authTime);

        var result = gate.Evaluate(Now);

        var stale = Assert.IsType<ReauthFreshnessResult.StaleResult>(result);
        Assert.True(stale.Age > AutomationReauthFreshnessGate.MaxAuthTimeAge);
    }

    [Fact]
    public void Evaluate_EntraMode_NoClaim_ReturnsMissingClaim()
    {
        var gate = BuildGate(entraAuthEnabled: true, authTime: null);

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.MissingClaimResult>(result);
    }

    [Fact]
    public void Evaluate_EntraMode_NoHttpContext_ReturnsMissingClaim()
    {
        var gate = new AutomationReauthFreshnessGate(
            BuildSettings(entraAuthEnabled: true),
            new HttpContextAccessor { HttpContext = null });

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.MissingClaimResult>(result);
    }

    [Fact]
    public void Evaluate_EntraMode_NonNumericClaim_ReturnsMissingClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("auth_time", "not-a-number")
        }, "test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        var gate = new AutomationReauthFreshnessGate(
            BuildSettings(entraAuthEnabled: true),
            accessor);

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.MissingClaimResult>(result);
    }

    [Fact]
    public void Evaluate_EntraMode_FutureBeyondSkew_ReturnsMissingClaim()
    {
        // Token "aus der Zukunft" weit jenseits der Skew-Toleranz ist nicht
        // vertrauenswuerdig — wie fehlender Claim behandeln, statt Soft-Success.
        var authTime = Now + TimeSpan.FromMinutes(5);
        var gate = BuildGate(entraAuthEnabled: true, authTime: authTime);

        var result = gate.Evaluate(Now);

        Assert.IsType<ReauthFreshnessResult.MissingClaimResult>(result);
    }

    private static AutomationReauthFreshnessGate BuildGate(bool entraAuthEnabled, DateTimeOffset? authTime)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildPrincipal(authTime)
            }
        };
        return new AutomationReauthFreshnessGate(BuildSettings(entraAuthEnabled), accessor);
    }

    private static ClaimsPrincipal BuildPrincipal(DateTimeOffset? authTime)
    {
        var claims = new List<Claim>();
        if (authTime.HasValue)
        {
            claims.Add(new Claim("auth_time", authTime.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static LifecycleRuntimeSettings BuildSettings(bool entraAuthEnabled)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Development",
            IsProduction = false,
            AuthMode = entraAuthEnabled ? "entra" : "dev-sim",
            DevSimulationEnabled = !entraAuthEnabled,
            EntraAuthEnabled = entraAuthEnabled,
            SwaggerEnabled = true,
            DirectorySyncEnabled = true,
            ConnectionString = null,
            PublicBaseUrl = null,
            EntraTenantId = null,
            EntraClientId = null,
            EntraAudience = null,
            EntraClientSecret = null,
            GraphClientSecret = null,
            DirectoryGroupPrefix = null,
            DirectoryExplicitGroupIds = null,
            DirectorySyncScheduled = false,
            DirectorySyncIntervalMinutes = 60,
            AutoProvisionDefaultRoleKey = null,
            RuntimeHealthStoragePaths = null,
            HostRuntimeHealthEnabled = false,
            HostRuntimeProcfsPath = null,
            HostRuntimeRootPath = null
        };
    }
}
