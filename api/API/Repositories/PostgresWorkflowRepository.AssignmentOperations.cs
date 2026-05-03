namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static string? ResolveTaskProcessArea(
        string? processAreaLabel,
        string? responsibilityDepartmentName,
        string? responsibilityType,
        string? responsibilityKey = null,
        string? responsibilityName = null)
    {
        if (!string.IsNullOrWhiteSpace(processAreaLabel))
        {
            return processAreaLabel.Trim();
        }

        if (string.Equals(responsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase))
        {
            return "Abteilungsleitung";
        }

        if (!string.IsNullOrWhiteSpace(responsibilityDepartmentName))
        {
            return responsibilityDepartmentName.Trim();
        }

        var areaFromResponsibilityKey = ResolveTaskProcessAreaFromResponsibilityKey(responsibilityKey);
        if (!string.IsNullOrWhiteSpace(areaFromResponsibilityKey))
        {
            return areaFromResponsibilityKey;
        }

        return string.IsNullOrWhiteSpace(responsibilityName)
            ? null
            : responsibilityName.Trim();
    }

    private static string? ResolveTaskProcessAreaFromResponsibilityKey(string? responsibilityKey)
    {
        if (string.IsNullOrWhiteSpace(responsibilityKey))
        {
            return null;
        }

        var normalizedKey = responsibilityKey.Trim().ToLowerInvariant();
        if (normalizedKey.StartsWith("it_", StringComparison.Ordinal))
        {
            return "IT";
        }

        if (normalizedKey.StartsWith("qs_", StringComparison.Ordinal))
        {
            return "QS";
        }

        if (normalizedKey.StartsWith("av_", StringComparison.Ordinal))
        {
            return "AV";
        }

        if (normalizedKey.StartsWith("qmb_", StringComparison.Ordinal))
        {
            return "QMB";
        }

        if (normalizedKey.StartsWith("hr_", StringComparison.Ordinal))
        {
            return "HR";
        }

        return null;
    }
}
