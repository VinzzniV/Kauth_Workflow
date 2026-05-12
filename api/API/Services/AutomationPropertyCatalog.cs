namespace API;

// Source-of-truth catalog of source/property pairs that the workflow-action
// input-mapping resolver in PostgresWorkflowAutomationOperations.ResolveAutomationReference
// understands. The frontend Mapping-Editor consumes this catalog through a
// dedicated endpoint instead of duplicating the property names.
//
// Property keys are camelCase (matching the JSON the editor produces); the backend
// resolver lowercases before its switch, so casing is irrelevant on the wire.
//
// Each property carries a German label for the builder UI plus a `kind`:
// - "business" for fachliche Felder (Vorname, E-Mail, Eintrittsdatum, ...)
// - "technical" for ID-/Schluesselfelder (workflowId, personId, ...)
// The frontend groups properties accordingly via <optgroup>; technical fields
// are pushed to the end of the dropdown so fachlich klare Felder kommen zuerst.
//
// Whenever a new property is added to one of the Resolve* helpers in
// PostgresWorkflowAutomationOperations.cs, the matching entry here MUST be extended.
internal static class AutomationPropertyCatalog
{
    public const string SourceWorkflow = "workflow";
    public const string SourceTargetPerson = "target_person";
    public const string SourceDirectoryIdentity = "directory_identity";
    public const string SourceAnswer = "answer";
    public const string SourceStatic = "static";

    public const string KindBusiness = "business";
    public const string KindTechnical = "technical";

    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> WorkflowProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "firstName", Label = "Vorname", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "lastName", Label = "Nachname", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "employeeNumber", Label = "Personalnummer", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "badgeNumber", Label = "Ausweisnummer", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "deadlineDate", Label = "Frist", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "workflowId", Label = "Workflow-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "workflowUid", Label = "Workflow-UID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "definitionKey", Label = "Definition-Schluessel (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "departmentId", Label = "Abteilungs-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "roleId", Label = "Rollen-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "targetPersonId", Label = "Zielperson-ID (technisch)", Kind = KindTechnical }
    };

    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> TargetPersonProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "firstName", Label = "Vorname", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "lastName", Label = "Nachname", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "displayName", Label = "Anzeigename", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "email", Label = "E-Mail", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "employeeNumber", Label = "Personalnummer", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "badgeNumber", Label = "Ausweisnummer", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "employmentStatus", Label = "Beschaeftigungsstatus", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "entryDate", Label = "Eintrittsdatum", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "exitDate", Label = "Austrittsdatum", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "personId", Label = "Person-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "appUserId", Label = "Benutzerkonto-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "directoryIdentityId", Label = "Verzeichnis-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "departmentId", Label = "Abteilungs-ID (technisch)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "roleId", Label = "Rollen-ID (technisch)", Kind = KindTechnical }
    };

    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> DirectoryIdentityProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "userPrincipalName", Label = "Login-Name (UPN)", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "mail", Label = "E-Mail", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "displayName", Label = "Anzeigename", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "departmentName", Label = "Abteilungsname", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "employeeNumber", Label = "Personalnummer", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "accountEnabled", Label = "Konto aktiv", Kind = KindBusiness },
        new AutomationPropertyCatalogPropertyDto { Key = "directoryIdentityId", Label = "Verzeichnis-ID (technisch)", Kind = KindTechnical }
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
                    Properties = Array.Empty<AutomationPropertyCatalogPropertyDto>()
                },
                new()
                {
                    Source = SourceStatic,
                    Label = "Statischer Wert",
                    Properties = Array.Empty<AutomationPropertyCatalogPropertyDto>()
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
    public required IReadOnlyList<AutomationPropertyCatalogPropertyDto> Properties { get; init; }
}

public sealed class AutomationPropertyCatalogPropertyDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Kind { get; init; }
}
