namespace API;

// Source-of-truth catalog of source/property pairs that the workflow-action
// input-mapping resolver in PostgresWorkflowAutomationOperations.ResolveAutomationReference
// understands. The frontend Mapping-Editor consumes this catalog through a
// dedicated endpoint instead of duplicating the property names.
//
// Property keys are camelCase (matching the JSON the editor produces); the backend
// resolver lowercases before its switch, so casing is irrelevant on the wire.
//
// Whenever a new property is added to one of the Resolve* helpers in
// PostgresWorkflowAutomationOperations.cs, the matching list here MUST be extended.
internal static class AutomationPropertyCatalog
{
    public const string SourceWorkflow = "workflow";
    public const string SourceTargetPerson = "target_person";
    public const string SourceDirectoryIdentity = "directory_identity";
    public const string SourceAnswer = "answer";
    public const string SourceStatic = "static";

    private static readonly IReadOnlyList<string> WorkflowProperties = new[]
    {
        "workflowId",
        "workflowUid",
        "definitionKey",
        "departmentId",
        "roleId",
        "firstName",
        "lastName",
        "employeeNumber",
        "badgeNumber",
        "deadlineDate",
        "targetPersonId"
    };

    private static readonly IReadOnlyList<string> TargetPersonProperties = new[]
    {
        "personId",
        "departmentId",
        "roleId",
        "appUserId",
        "directoryIdentityId",
        "firstName",
        "lastName",
        "employeeNumber",
        "badgeNumber",
        "employmentStatus",
        "entryDate",
        "exitDate",
        "displayName",
        "email"
    };

    private static readonly IReadOnlyList<string> DirectoryIdentityProperties = new[]
    {
        "directoryIdentityId",
        "userPrincipalName",
        "mail",
        "displayName",
        "departmentName",
        "employeeNumber",
        "accountEnabled"
    };

    public static AutomationPropertyCatalogDto BuildDto()
    {
        return new AutomationPropertyCatalogDto
        {
            Sources = new List<AutomationPropertyCatalogSourceDto>
            {
                new()
                {
                    Source = SourceWorkflow,
                    Label = "Workflow-Feld",
                    Properties = WorkflowProperties
                },
                new()
                {
                    Source = SourceTargetPerson,
                    Label = "Person (Ziel)",
                    Properties = TargetPersonProperties
                },
                new()
                {
                    Source = SourceDirectoryIdentity,
                    Label = "Verzeichnis-Identität",
                    Properties = DirectoryIdentityProperties
                },
                new()
                {
                    Source = SourceAnswer,
                    Label = "Antwort aus Formular",
                    Properties = Array.Empty<string>()
                },
                new()
                {
                    Source = SourceStatic,
                    Label = "Statischer Wert",
                    Properties = Array.Empty<string>()
                }
            }
        };
    }
}

public sealed class AutomationPropertyCatalogDto
{
    public required IReadOnlyList<AutomationPropertyCatalogSourceDto> Sources { get; init; }
}

public sealed class AutomationPropertyCatalogSourceDto
{
    public required string Source { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<string> Properties { get; init; }
}
