namespace AdAutomationWorker.Core.Ad;

// Spec für UpdateAdUserAttributesLdaps.
// Attributes-Dictionary: Key = LDAP-Attributname (aus Whitelist), Value = neuer Wert.
// null als Value = Attribut löschen (AD Replace auf leeren Wert). Fehlendes Key = nicht anfassen.
//
// Whitelist-Keys (LDAP-seitig): manager, department, title, description.
// Payload-Keys: managerDistinguishedName (-> manager), department, title, description.
public sealed class AdUserAttributeUpdateSpec
{
    public required string DistinguishedName { get; init; }

    // Key = LDAP-Attributname, Value = neuer String-Wert oder null (Clear-Semantik).
    public required IReadOnlyDictionary<string, string?> Attributes { get; init; }
}

// Snapshot der aktuellen Attribut-Werte für PlanAsync (Read-only).
public sealed class AdUserAttributeSnapshot
{
    public string? Manager { get; init; }
    public string? Department { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}
