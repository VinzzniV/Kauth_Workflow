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
    public const string SourceCreatedAdUser = "created_ad_user";
    public const string SourceCreatedMailbox = "created_mailbox";
    // Slice 5: Cloud-Gruppen eines per person_lookup-Antwort gewaehlten Referenzusers.
    // licenseSkus ist out-of-scope (CreateMailboxGraph erwartet single-skuId).
    public const string SourceReferenceUser = "reference_user";

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

    // Enge Allow-List fuer Werte aus dem CreateAdUserLdaps-Output. distinguishedName +
    // credentialVaultId + userPrincipalName sind exponiert -- das Passwort wird ueber keine
    // Mapping-Source erreichbar gemacht (Vault-Grenze ist im Code, siehe Schritt-6-Sub-C).
    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> CreatedAdUserProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "distinguishedName", Label = "AD Distinguished Name", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "userPrincipalName", Label = "User Principal Name (UPN)", Kind = KindTechnical },
        new AutomationPropertyCatalogPropertyDto { Key = "credentialVaultId", Label = "Vault-ID des Initial-Passworts (UUID)", Kind = KindTechnical }
    };

    // Etappe 9a Schritt 8: schmale Whitelist fuer Decision-Bedingungen auf
    // CreateAdUserLdaps-Output. Bewusst getrennt von CreatedAdUserProperties (Input-Mapping)
    // -- jede neue Bedingungs-Property ist eine eigene Entscheidung und braucht zusaetzlich
    // einen Eintrag in WorkflowRuntimeEngine.AllowedConditionProperties (Drift-Schutz-Test).
    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> CreatedAdUserConditionProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "alreadyExisted", Label = "AD-User existierte bereits", Kind = KindBusiness }
    };

    // Enge Allow-List fuer Werte aus dem CreateMailboxGraph-Output (Etappe 9a Schritt 7).
    // Nur die echte beobachtete primary SMTP-Adresse ist exponiert; licenseSkuId etc. bleiben
    // bewusst draussen, damit jede zusaetzliche Property eine bewusste Entscheidung ist.
    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> CreatedMailboxProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "primarySmtpAddress", Label = "Primary SMTP Address", Kind = KindBusiness }
    };

    // Slice 5: einzige Property bewusst eng (Multi-SKU-Mapping waere Folge-Slice).
    private static readonly IReadOnlyList<AutomationPropertyCatalogPropertyDto> ReferenceUserProperties = new[]
    {
        new AutomationPropertyCatalogPropertyDto { Key = "groups", Label = "Gruppen-Mitgliedschaften", Kind = KindBusiness }
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
                },
                new()
                {
                    Source = SourceCreatedAdUser,
                    Label = "AD-User aus Vorgaengerschritt",
                    Properties = CreatedAdUserProperties,
                    ConditionProperties = CreatedAdUserConditionProperties
                },
                new()
                {
                    Source = SourceCreatedMailbox,
                    Label = "Mailbox aus Vorgaengerschritt",
                    Properties = CreatedMailboxProperties
                },
                new()
                {
                    Source = SourceReferenceUser,
                    Label = "Referenzuser (aus Formular-Antwort)",
                    Properties = ReferenceUserProperties
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
    public IReadOnlyList<AutomationPropertyCatalogPropertyDto> ConditionProperties { get; init; }
        = Array.Empty<AutomationPropertyCatalogPropertyDto>();
}

public sealed class AutomationPropertyCatalogPropertyDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Kind { get; init; }
}
