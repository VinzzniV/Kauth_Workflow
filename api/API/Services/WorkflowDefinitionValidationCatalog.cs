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
}
