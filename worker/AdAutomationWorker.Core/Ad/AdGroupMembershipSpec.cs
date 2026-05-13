namespace AdAutomationWorker.Core.Ad;

// Eingabevertrag fuer IAdGroupMembershipWriter. UserDistinguishedName + Liste der Ziel-Group-DNs.
// Beide kommen aus dem Linux-API-Input-Mapping (Schritt-5-Pattern: API ist Quelle der Wahrheit,
// Worker validiert nur Form).
public sealed record AdGroupMembershipSpec
{
    public required string UserDistinguishedName { get; init; }
    public required IReadOnlyList<string> GroupDistinguishedNames { get; init; }
}
