using System.Text.Json;

namespace API;

public sealed class AdminSystemLogEntryDto
{
    public required long Id { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string Severity { get; init; }
    public required string Source { get; init; }
    public required string Category { get; init; }
    public required string EventKey { get; init; }
    public required string Message { get; init; }
    public string? UserMessage { get; init; }
    public long? ActorUserId { get; init; }
    public string? ActorDisplayName { get; init; }
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
    public JsonElement? Details { get; init; }
}

public sealed class AdminSystemLogSourceCountDto
{
    public required string Source { get; init; }
    public required int Count { get; init; }
}

public sealed class AdminSystemLogSummaryDto
{
    public required int TotalCount { get; init; }
    public required int InfoCount { get; init; }
    public required int WarningCount { get; init; }
    public required int ErrorCount { get; init; }
    public required List<AdminSystemLogSourceCountDto> Sources { get; init; }
}

public sealed class ClientLogEventRequest
{
    public string? Severity { get; init; }
    public string? Source { get; init; }
    public string? Category { get; init; }
    public string? EventKey { get; init; }
    public string? Message { get; init; }
    public string? UserMessage { get; init; }
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
    public JsonElement? Details { get; init; }
}
