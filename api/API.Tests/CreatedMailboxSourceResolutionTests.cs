using System.Text.Json;
using Xunit;

namespace API.Tests;

// Etappe 9a Schritt 7 Sub-D: Tests fuer die enge `created_mailbox`-Source.
// Property-Whitelist: nur `primarySmtpAddress`. Andere Properties + Misconfig werfen.
public sealed class CreatedMailboxSourceResolutionTests
{
    private static AutomationPayloadContextRecord MinimalContext() => new()
    {
        WorkflowId = 1,
        WorkflowUid = Guid.NewGuid(),
        DepartmentId = 1,
        RoleId = 1
    };

    [Fact]
    public void Resolve_ValidNodeKeyAndProperty_ReturnsSmtp()
    {
        var output = JsonDocument.Parse("{\"primarySmtpAddress\":\"john.doe@example.com\",\"licenseSkuId\":\"abc\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-mailbox"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"nodeKey\":\"create-mailbox\",\"property\":\"primarySmtpAddress\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_mailbox", MinimalContext(), EmptyAnswers(),
            createdAdUserOutputsByNodeKey: EmptyOutputs(),
            createdMailboxOutputsByNodeKey: lookup);

        Assert.Equal("john.doe@example.com", resolved);
    }

    [Fact]
    public void Resolve_DisallowedProperty_LicenseSkuId_Throws()
    {
        // licenseSkuId steht im Output, ist aber NICHT in der Mapping-Whitelist.
        var output = JsonDocument.Parse("{\"primarySmtpAddress\":\"x@y.z\",\"licenseSkuId\":\"abc\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-mailbox"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"nodeKey\":\"create-mailbox\",\"property\":\"licenseSkuId\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_mailbox", MinimalContext(), EmptyAnswers(),
                createdAdUserOutputsByNodeKey: EmptyOutputs(),
                createdMailboxOutputsByNodeKey: lookup));

        Assert.Contains("only property 'primarySmtpAddress'", ex.Message);
    }

    [Fact]
    public void Resolve_UnknownNodeKey_Throws()
    {
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"nodeKey\":\"nope\",\"property\":\"primarySmtpAddress\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_mailbox", MinimalContext(), EmptyAnswers(),
                createdAdUserOutputsByNodeKey: EmptyOutputs(),
                createdMailboxOutputsByNodeKey: EmptyOutputs()));

        Assert.Contains("unknown nodeKey 'nope'", ex.Message);
    }

    [Fact]
    public void Resolve_MissingPrimarySmtpAddress_Throws()
    {
        // succeeded Output ohne SMTP -- Handler/Provisioner-Bug, harter Fail mit klarer Meldung.
        var output = JsonDocument.Parse("{\"licenseSkuId\":\"abc\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-mailbox"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"nodeKey\":\"create-mailbox\",\"property\":\"primarySmtpAddress\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_mailbox", MinimalContext(), EmptyAnswers(),
                createdAdUserOutputsByNodeKey: EmptyOutputs(),
                createdMailboxOutputsByNodeKey: lookup));

        Assert.Contains("handler/provisioner contract bug", ex.Message);
    }

    [Fact]
    public void Resolve_EmptyPrimarySmtpAddress_Throws()
    {
        var output = JsonDocument.Parse("{\"primarySmtpAddress\":\"\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-mailbox"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"nodeKey\":\"create-mailbox\",\"property\":\"primarySmtpAddress\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_mailbox", MinimalContext(), EmptyAnswers(),
                createdAdUserOutputsByNodeKey: EmptyOutputs(),
                createdMailboxOutputsByNodeKey: lookup));

        Assert.Contains("handler/provisioner contract bug", ex.Message);
    }

    [Fact]
    public void Resolve_MissingNodeKey_Throws()
    {
        var element = JsonDocument.Parse("{\"source\":\"created_mailbox\",\"property\":\"primarySmtpAddress\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_mailbox", MinimalContext(), EmptyAnswers(),
                createdAdUserOutputsByNodeKey: EmptyOutputs(),
                createdMailboxOutputsByNodeKey: EmptyOutputs()));

        Assert.Contains("requires non-empty 'nodeKey'", ex.Message);
    }

    private static IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> EmptyAnswers()
        => new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, JsonElement> EmptyOutputs()
        => new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
