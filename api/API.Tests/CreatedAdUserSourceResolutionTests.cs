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

    // Etappe 9a Schritt 6 Sub-C: credentialVaultId als zweite erlaubte Property.

    [Fact]
    public void Resolve_CredentialVaultId_UuidString_ReturnsUuid()
    {
        var uuid = "11111111-2222-3333-4444-555555555555";
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\",\"credentialVaultId\":\"" + uuid + "\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"credentialVaultId\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup);

        Assert.Equal(uuid, resolved);
    }

    [Fact]
    public void Resolve_CredentialVaultId_Null_ReturnsNullWithoutThrow()
    {
        // AlreadyExists-Case: CreateAdUserLdaps schreibt credentialVaultId=null. Resolver
        // muss null durchreichen, damit der Welcome-Mail-Handler einen sauberen Permanent-
        // Failure liefern kann statt einen Payload-Build-Loop zu triggern.
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\",\"credentialVaultId\":null}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"credentialVaultId\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_CredentialVaultId_Missing_ReturnsNullWithoutThrow()
    {
        // Aelterer Output-Pfad ohne credentialVaultId-Feld -> null durchreichen.
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"credentialVaultId\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_CredentialVaultId_NonStringKind_Throws()
    {
        // Misconfig: credentialVaultId als Object/Array. Resolver soll hart fehlschlagen.
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\",\"credentialVaultId\":{\"nested\":1}}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"credentialVaultId\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("unexpected kind", ex.Message);
    }

    // Etappe 9a Schritt 7 Sub-D: userPrincipalName als zusaetzliche Whitelist-Property
    // (fuer die CreateAdUserLdaps -> CreateMailboxGraph Verkettung).

    [Fact]
    public void Resolve_UserPrincipalName_ReturnsUpnFromOutput()
    {
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\",\"userPrincipalName\":\"john.doe@example.com\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"userPrincipalName\"}").RootElement;

        var resolved = PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
            element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup);

        Assert.Equal("john.doe@example.com", resolved);
    }

    [Fact]
    public void Resolve_UserPrincipalName_Missing_Throws()
    {
        var output = JsonDocument.Parse("{\"distinguishedName\":\"CN=John\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"userPrincipalName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("no usable 'userPrincipalName'", ex.Message);
    }

    [Fact]
    public void Resolve_UserPrincipalName_Empty_Throws()
    {
        var output = JsonDocument.Parse("{\"userPrincipalName\":\"\"}").RootElement;
        var lookup = new Dictionary<string, JsonElement> { ["create-user"] = output };
        var element = JsonDocument.Parse("{\"source\":\"created_ad_user\",\"nodeKey\":\"create-user\",\"property\":\"userPrincipalName\"}").RootElement;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresWorkflowAutomationOperations.ResolveAutomationReferenceForTesting(
                element, "created_ad_user", MinimalContext(), EmptyAnswers(), lookup));

        Assert.Contains("no usable 'userPrincipalName'", ex.Message);
    }

    private static IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> EmptyAnswers()
        => new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
}
