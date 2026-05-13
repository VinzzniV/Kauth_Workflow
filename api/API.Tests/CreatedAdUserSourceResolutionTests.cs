using System.Text.Json;
using Xunit;

namespace API.Tests;

// Etappe 9a Schritt 5 Sub-A: Tests fuer die enge `created_ad_user`-Source. Allow-List nur
// `distinguishedName`; alle vier Fehler-Cases werfen InvalidOperationException, sodass der
// Fehler beim Payload-Build (vor Job-Erzeugung) sichtbar wird statt spaeter als kryptischer
// LDAP-Fehler.
public sealed class CreatedAdUserSourceResolutionTests
{
    private static AutomationPayloadContextRecord MinimalContext() => new()
    {
        WorkflowId = 1,
        WorkflowUid = Guid.NewGuid(),
        DepartmentId = 1,
        RoleId = 1
    };

    [Fact]
    public void Resolve_ValidNodeKeyAndProperty_ReturnsDn()
    {
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John,OU=Sales,DC=test,DC=local\",\"samAccountName\":\"john\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"distinguishedName\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup);

        Assert.Equal("CN=John,OU=Sales,DC=test,DC=local", resolved);
    }

    [Fact]
    public void Resolve_DisallowedProperty_ThrowsWithVaultGrenzeHint()
    {
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\",\"temporaryPassword\":\"hidden\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"temporaryPassword\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("distinguishedName", ex.Message);
        Assert.Contains("Vault-Grenze", ex.Message);
    }

    [Fact]
    public void Resolve_UnknownNodeKey_Throws()
    {
        var lookup = new Dictionary<string, JsonElement>();
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"nope\",\"property\":\"distinguishedName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("unknown nodeKey 'nope'", ex.Message);
    }

    [Fact]
    public void Resolve_OutputWithoutDn_Throws()
    {
        var output = JsonDocument.Parse("{\"otherField\":\"value\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"distinguishedName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("no usable 'distinguishedName'", ex.Message);
    }

    [Fact]
    public void Resolve_OutputWithEmptyDn_Throws()
    {
        var output = JsonDocument.Parse("{\"distinguishedName\":\"\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"distinguishedName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("no usable 'distinguishedName'", ex.Message);
    }

    [Fact]
    public void Resolve_MissingNodeKey_Throws()
    {
        var lookup = new Dictionary<string, JsonElement>();
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"property\":\"distinguishedName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("requires non-empty 'nodeKey'", ex.Message);
    }

    private static IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> EmptyAnswers()
        => new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
}
