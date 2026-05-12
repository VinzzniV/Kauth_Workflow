namespace AdAutomationWorker.Core.Ad;

// Eingabevertrag fuer IAdUserWriter.CreateUserAsync. Felder kommen aus dem Workflow-Input-
// Mapping; der Worker validiert Form, nicht Existenz/Eindeutigkeit (Sache der Linux-API).
public sealed record AdUserSpec
{
    public required string SamAccountName { get; init; }
    public required string UserPrincipalName { get; init; }
    public required string DisplayName { get; init; }
    public required string GivenName { get; init; }
    public required string Surname { get; init; }
    public required string Mail { get; init; }
    public required string TargetOuDn { get; init; }
    public required string Password { get; init; }
    public string? EmployeeNumber { get; init; }

    // Distinguished Name fuer den neuen User. CN-Komponente verwendet displayName 1:1; bei
    // Sonderzeichen muss der Caller selbst escapen — bewusst minimal, weil Linux-API ohnehin
    // saubere displayName-Strings liefert (Person-Felder, nicht freie Texte).
    public string BuildDistinguishedName() => $"CN={DisplayName},{TargetOuDn}";
}
