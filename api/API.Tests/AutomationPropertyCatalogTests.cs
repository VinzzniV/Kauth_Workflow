using Xunit;

namespace API.Tests;

public sealed class AutomationPropertyCatalogTests
{
    [Fact]
    public void BuildDto_HasGermanLabelsForKnownProperties()
    {
        var dto = AutomationPropertyCatalog.BuildDto();

        Assert.Equal("Vorname", GetProperty(dto, AutomationPropertyCatalog.SourceWorkflow, "firstName").Label);
        Assert.Equal("Personalnummer", GetProperty(dto, AutomationPropertyCatalog.SourceWorkflow, "employeeNumber").Label);
        Assert.Equal("E-Mail", GetProperty(dto, AutomationPropertyCatalog.SourceTargetPerson, "email").Label);
        Assert.Equal("Eintrittsdatum", GetProperty(dto, AutomationPropertyCatalog.SourceTargetPerson, "entryDate").Label);
        Assert.Equal("Login-Name (UPN)", GetProperty(dto, AutomationPropertyCatalog.SourceDirectoryIdentity, "userPrincipalName").Label);
        Assert.Equal("Konto aktiv", GetProperty(dto, AutomationPropertyCatalog.SourceDirectoryIdentity, "accountEnabled").Label);
    }

    [Theory]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "workflowId")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "workflowUid")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "definitionKey")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "departmentId")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "roleId")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "targetPersonId")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "personId")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "appUserId")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "directoryIdentityId")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "departmentId")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "roleId")]
    [InlineData(AutomationPropertyCatalog.SourceDirectoryIdentity, "directoryIdentityId")]
    public void BuildDto_MarksIdFieldsAsTechnical(string source, string propertyKey)
    {
        var dto = AutomationPropertyCatalog.BuildDto();
        var property = GetProperty(dto, source, propertyKey);
        Assert.Equal(AutomationPropertyCatalog.KindTechnical, property.Kind);
    }

    [Theory]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "firstName")]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow, "deadlineDate")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "email")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "entryDate")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "exitDate")]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson, "employmentStatus")]
    [InlineData(AutomationPropertyCatalog.SourceDirectoryIdentity, "userPrincipalName")]
    [InlineData(AutomationPropertyCatalog.SourceDirectoryIdentity, "displayName")]
    [InlineData(AutomationPropertyCatalog.SourceDirectoryIdentity, "accountEnabled")]
    public void BuildDto_MarksBusinessFieldsAsBusiness(string source, string propertyKey)
    {
        var dto = AutomationPropertyCatalog.BuildDto();
        var property = GetProperty(dto, source, propertyKey);
        Assert.Equal(AutomationPropertyCatalog.KindBusiness, property.Kind);
    }

    [Fact]
    public void BuildDto_AllResolverPropertiesArePresent()
    {
        // Source-of-truth ist PostgresWorkflowAutomationOperations.Resolve*-Helper.
        // Bei jeder neuen Property dort MUSS hier ein Eintrag dazukommen.
        var dto = AutomationPropertyCatalog.BuildDto();

        var workflowExpected = new[]
        {
            "workflowId", "workflowUid", "definitionKey", "departmentId", "roleId",
            "firstName", "lastName", "employeeNumber", "badgeNumber", "deadlineDate", "targetPersonId"
        };
        AssertSourceContains(dto, AutomationPropertyCatalog.SourceWorkflow, workflowExpected);

        var personExpected = new[]
        {
            "personId", "departmentId", "roleId", "appUserId", "directoryIdentityId",
            "firstName", "lastName", "employeeNumber", "badgeNumber",
            "employmentStatus", "entryDate", "exitDate", "displayName", "email"
        };
        AssertSourceContains(dto, AutomationPropertyCatalog.SourceTargetPerson, personExpected);

        var directoryExpected = new[]
        {
            "directoryIdentityId", "userPrincipalName", "mail", "displayName",
            "departmentName", "employeeNumber", "accountEnabled"
        };
        AssertSourceContains(dto, AutomationPropertyCatalog.SourceDirectoryIdentity, directoryExpected);
    }

    [Fact]
    public void BuildDto_AnswerAndStaticSources_AreEmpty()
    {
        var dto = AutomationPropertyCatalog.BuildDto();

        var answer = dto.Sources.Single(s => s.Source == AutomationPropertyCatalog.SourceAnswer);
        var staticSrc = dto.Sources.Single(s => s.Source == AutomationPropertyCatalog.SourceStatic);

        Assert.Empty(answer.Properties);
        Assert.Empty(staticSrc.Properties);
    }

    // Etappe 9a Schritt 8: ConditionProperties.

    [Fact]
    public void BuildDto_CreatedAdUser_HasAlreadyExistedConditionProperty()
    {
        var dto = AutomationPropertyCatalog.BuildDto();
        var src = dto.Sources.Single(s => s.Source == AutomationPropertyCatalog.SourceCreatedAdUser);

        var alreadyExisted = Assert.Single(src.ConditionProperties);
        Assert.Equal("alreadyExisted", alreadyExisted.Key);
        Assert.Equal(AutomationPropertyCatalog.KindBusiness, alreadyExisted.Kind);
    }

    [Fact]
    public void BuildDto_CreatedAdUser_AlreadyExisted_NotInInputMappingProperties()
    {
        // alreadyExisted ist eine Bedingungs-Property, KEINE Input-Mapping-Property.
        var dto = AutomationPropertyCatalog.BuildDto();
        var src = dto.Sources.Single(s => s.Source == AutomationPropertyCatalog.SourceCreatedAdUser);
        Assert.DoesNotContain(src.Properties, p => p.Key == "alreadyExisted");
    }

    [Theory]
    [InlineData(AutomationPropertyCatalog.SourceWorkflow)]
    [InlineData(AutomationPropertyCatalog.SourceTargetPerson)]
    [InlineData(AutomationPropertyCatalog.SourceDirectoryIdentity)]
    [InlineData(AutomationPropertyCatalog.SourceAnswer)]
    [InlineData(AutomationPropertyCatalog.SourceStatic)]
    [InlineData(AutomationPropertyCatalog.SourceCreatedMailbox)]
    public void BuildDto_SourcesWithoutConditionProperties_AreEmpty(string source)
    {
        var dto = AutomationPropertyCatalog.BuildDto();
        var src = dto.Sources.Single(s => s.Source == source);
        Assert.Empty(src.ConditionProperties);
    }

    [Fact]
    public void Catalog_AndEngine_AllowedConditionProperties_StayInSync()
    {
        // Drift-Schutz: alle Properties, die der Catalog als Condition-Property fuer
        // created_ad_user fuehrt, muessen auch im Engine-Parser whitelisted sein.
        // Sonst koennte das UI eine Property anbieten, die der Engine-Parser ablehnt.
        var dto = AutomationPropertyCatalog.BuildDto();
        var src = dto.Sources.Single(s => s.Source == AutomationPropertyCatalog.SourceCreatedAdUser);
        var catalogProperties = src.ConditionProperties.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            WorkflowRuntimeEngine.AllowedConditionProperties.TryGetValue("CreateAdUserLdaps", out var engineProperties),
            "Engine-Parser kennt keinen Eintrag fuer 'CreateAdUserLdaps' in AllowedConditionProperties.");

        foreach (var property in catalogProperties)
        {
            Assert.True(
                engineProperties!.Contains(property),
                $"Catalog fuehrt Condition-Property '{property}' fuer 'CreateAdUserLdaps', aber der Engine-Parser kennt sie nicht.");
        }
    }

    private static AutomationPropertyCatalogPropertyDto GetProperty(
        AutomationPropertyCatalogDto dto,
        string source,
        string key)
    {
        var src = dto.Sources.Single(s => s.Source == source);
        return src.Properties.Single(p => p.Key == key);
    }

    private static void AssertSourceContains(
        AutomationPropertyCatalogDto dto,
        string source,
        IEnumerable<string> expectedKeys)
    {
        var src = dto.Sources.Single(s => s.Source == source);
        var actualKeys = src.Properties.Select(p => p.Key).ToHashSet();
        foreach (var key in expectedKeys)
        {
            Assert.True(actualKeys.Contains(key), $"Source '{source}' is missing property '{key}'.");
        }
    }
}
