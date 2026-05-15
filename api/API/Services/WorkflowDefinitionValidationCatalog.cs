namespace API;

internal static class WorkflowDefinitionValidationCatalog
{
    public static readonly HashSet<string> SupportedDecisionOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "is_true",
        "is_false",
        "is_null",
        "is_not_null"
    };

    public static readonly HashSet<string> AllowedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "start",
        "form",
        "approval",
        "measure_provision",
        "measure_deprovision",
        "measure_change",
        "measure_rename",
        "task",
        "decision",
        "parallel_split",
        "parallel_join",
        "automation",
        "end"
    };

    public static readonly HashSet<string> MeasureGenerationNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "measure_provision",
        "measure_deprovision",
        "measure_change",
        "measure_rename"
    };

    public static readonly HashSet<string> AllowedSpecConditionOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "is_true",
        "is_false",
        "is_null",
        "is_not_null"
    };

    public static readonly Dictionary<string, string> ExpectedMeasureNodeTypeByDefinitionKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["onboarding"] = "measure_provision",
        ["offboarding"] = "measure_deprovision",
        ["department_change"] = "measure_change",
        ["position_change"] = "measure_change",
        ["role_change"] = "measure_change",
        ["name_change"] = "measure_rename"
    };

    // Slice 2 (Admin-Gated-Automation, Task-Automation-Binding): Whitelist der
    // Role-Slugs, die fuer workflow_nodes.automation_admin_role zulaessig sind.
    // Worker/Reader bewusst NICHT — Admin-Gated-Approval impliziert HR/Manager/
    // Admin-Level. Case-insensitive wie AuthorizationPolicyService.HasAnyRole;
    // Persistenz wird durch NormalizeAutomationAdminRole auf lowercase normiert,
    // aber die Whitelist toleriert defensiv jede Schreibweise.
    public static readonly HashSet<string> AllowedAutomationAdminRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AuthorizationRoles.Admin,
        AuthorizationRoles.Hr,
        AuthorizationRoles.Manager
    };

    // Single Source of Truth fuer "welche node_types duerfen Actions tragen".
    // Wird sowohl vom Snapshot/Draft-Validator als auch von den Action-Reference-
    // Checks in GraphMappingOperations und LifecycleStartupValidationExtensions
    // verwendet — verhindert Drift zwischen den drei Pfaden.
    public static bool AllowsActions(string? nodeType)
    {
        return string.Equals(nodeType, "automation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nodeType, "task", StringComparison.OrdinalIgnoreCase);
    }
}
