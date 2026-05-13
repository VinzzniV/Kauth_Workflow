using Microsoft.Graph.Models;
using Xunit;

namespace API.Tests;

// Etappe 9a Schritt 7 Sub-B: Tests fuer die HTTP-/Message-Klassifikation und die
// SMTP-Extraktion im GraphMailboxProvisioner. Die eigentlichen Graph-SDK-Calls sind
// in den manuellen E2E-Tests gegen einen Test-Tenant abgedeckt (analog Schritt 5).
public sealed class GraphMailboxProvisionerOutcomeTests
{
    private static readonly Guid SkuId = new("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void MapAssignLicenseBadRequest_UnknownSku_ReturnsPermanentWithSkuHint()
    {
        var outcome = GraphMailboxProvisioner.MapAssignLicenseBadRequest(
            "Unknown subscribedSku '00000000-...'",
            SkuId);

        var permanent = Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
        Assert.Contains("Unknown skuId", permanent.Reason);
        Assert.Contains("Get-MgSubscribedSku", permanent.Reason);
        Assert.Equal(400, permanent.HttpStatus);
    }

    [Fact]
    public void MapAssignLicenseBadRequest_DoesNotExist_AlsoMatchesUnknownPath()
    {
        var outcome = GraphMailboxProvisioner.MapAssignLicenseBadRequest(
            "License sku does not exist for tenant",
            SkuId);

        var permanent = Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
        Assert.Contains("Unknown skuId", permanent.Reason);
    }

    [Fact]
    public void MapAssignLicenseBadRequest_CountViolation_ReturnsPermanentWithPoolHint()
    {
        var outcome = GraphMailboxProvisioner.MapAssignLicenseBadRequest(
            "Operation aborted due to CountViolation. SKU limit reached.",
            SkuId);

        var permanent = Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
        Assert.Contains("license pool exhausted", permanent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(400, permanent.HttpStatus);
    }

    [Fact]
    public void MapAssignLicenseBadRequest_Exceeded_AlsoMatchesPoolPath()
    {
        var outcome = GraphMailboxProvisioner.MapAssignLicenseBadRequest(
            "License count exceeded.",
            SkuId);

        var permanent = Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
        Assert.Contains("license pool exhausted", permanent.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapAssignLicenseBadRequest_GenericBadRequest_StillPermanent()
    {
        var outcome = GraphMailboxProvisioner.MapAssignLicenseBadRequest(
            "Malformed request payload.",
            SkuId);

        var permanent = Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
        Assert.Contains("400", permanent.Reason);
    }

    [Fact]
    public void ClassifyHttpStatus_500_ReturnsTransient()
    {
        var outcome = GraphMailboxProvisioner.ClassifyHttpStatus(500, "Internal server error");
        var transient = Assert.IsType<GraphMailboxProvisionOutcome.TransientFailure>(outcome);
        Assert.Equal(500, transient.HttpStatus);
    }

    [Fact]
    public void ClassifyHttpStatus_429_ReturnsTransient()
    {
        var outcome = GraphMailboxProvisioner.ClassifyHttpStatus(429, "Too Many Requests");
        Assert.IsType<GraphMailboxProvisionOutcome.TransientFailure>(outcome);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public void ClassifyHttpStatus_ClientErrors_ReturnsPermanent(int status)
    {
        var outcome = GraphMailboxProvisioner.ClassifyHttpStatus(status, "err");
        Assert.IsType<GraphMailboxProvisionOutcome.PermanentFailure>(outcome);
    }

    // --- SMTP-Strictness ----------------------------------------------------------------

    [Fact]
    public void ExtractPrimarySmtpAddress_UppercaseSmtpInProxyAddresses_IsPrimary()
    {
        var user = new User
        {
            ProxyAddresses = new List<string> { "smtp:secondary@x.y", "SMTP:primary@x.y" },
            Mail = "mail-fallback@x.y",
        };

        Assert.Equal("primary@x.y", GraphMailboxProvisioner.ExtractPrimarySmtpAddress(user));
    }

    [Fact]
    public void ExtractPrimarySmtpAddress_NoUppercaseSmtp_FallsBackToMail()
    {
        var user = new User
        {
            ProxyAddresses = new List<string> { "smtp:secondary@x.y" },
            Mail = "mail-fallback@x.y",
        };

        Assert.Equal("mail-fallback@x.y", GraphMailboxProvisioner.ExtractPrimarySmtpAddress(user));
    }

    [Fact]
    public void ExtractPrimarySmtpAddress_EmptyProxyAddresses_FallsBackToMail()
    {
        var user = new User
        {
            ProxyAddresses = new List<string>(),
            Mail = "only-mail@x.y",
        };

        Assert.Equal("only-mail@x.y", GraphMailboxProvisioner.ExtractPrimarySmtpAddress(user));
    }

    [Fact]
    public void ExtractPrimarySmtpAddress_NoSmtpAndNoMail_ReturnsNull()
    {
        // CRITICAL: kein UPN-Fallback. Wenn weder proxyAddresses noch mail eine echte Adresse
        // tragen, muss der Provisioner MailboxProvisioningInProgress liefern -- nicht stumm
        // den UPN als "primary SMTP" verkaufen.
        var user = new User
        {
            ProxyAddresses = new List<string>(),
            Mail = null,
            UserPrincipalName = "would-be-fallback@x.y",
        };

        Assert.Null(GraphMailboxProvisioner.ExtractPrimarySmtpAddress(user));
    }

    [Fact]
    public void ExtractPrimarySmtpAddress_NullUser_ReturnsNull()
    {
        Assert.Null(GraphMailboxProvisioner.ExtractPrimarySmtpAddress(null));
    }

    [Fact]
    public void ExtractPrimarySmtpAddress_EmptySmtpPrefixValue_FallsBackToMail()
    {
        // Defensiv: leerer Wert nach "SMTP:" wird ignoriert, mail bleibt.
        var user = new User
        {
            ProxyAddresses = new List<string> { "SMTP:" },
            Mail = "mail@x.y",
        };

        Assert.Equal("mail@x.y", GraphMailboxProvisioner.ExtractPrimarySmtpAddress(user));
    }
}
