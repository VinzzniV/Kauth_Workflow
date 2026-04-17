namespace API;

internal interface ISystemEventLogService
{
    Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminSystemLogEntryDto>> GetAdminLogsAsync(
        SystemEventLogQuery query,
        CancellationToken cancellationToken = default);
    Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(
        SystemEventLogQuery query,
        CancellationToken cancellationToken = default);
}

internal sealed class SystemEventLogQuery
{
    public IReadOnlyList<string> Severities { get; init; } = [];
    public string? Source { get; init; }
    public DateTime? Since { get; init; }
    public DateTime? Until { get; init; }
    public string? Search { get; init; }
    public long? ActorUserId { get; init; }
    public Guid? WorkflowUid { get; init; }
    public long? RotationPlanId { get; init; }
    public string? TaskRef { get; init; }
    public int Limit { get; init; } = 50;
    public int Offset { get; init; }
}

internal sealed class SystemEventLogWriteModel
{
    public string Severity { get; init; } = "info";
    public string Source { get; init; } = "system";
    public string Category { get; init; } = "general";
    public string EventKey { get; init; } = "event";
    public required string Message { get; init; }
    public string? UserMessage { get; init; }
    public long? ActorUserId { get; init; }
    public string? ClientRoute { get; init; }
    public string? ClientFunction { get; init; }
    public string? HttpMethod { get; init; }
    public string? HttpPath { get; init; }
    public int? HttpStatus { get; init; }
    public string? TraceIdentifier { get; init; }
    public Guid? WorkflowUid { get; init; }
    public long? RotationPlanId { get; init; }
    public string? TaskRef { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public object? Details { get; init; }
}
