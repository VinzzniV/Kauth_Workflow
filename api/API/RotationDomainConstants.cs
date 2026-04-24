namespace API;

internal static class RotationPlanStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Archived = "archived";
}

internal static class RotationStationStatuses
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

internal static class RotationTaskStatuses
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

internal static class RotationTriggerTypes
{
    public const string Enter = "enter";
    public const string Exit = "exit";
}

internal static class RotationTaskTypes
{
    public const string Manual = "manual";
    public const string Technical = "technical";
    public const string Approval = "approval";
    public const string Information = "information";
}
