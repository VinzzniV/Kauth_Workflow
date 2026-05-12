namespace API;

public sealed class AdminRuntimeHealthDto
{
    public required DateTime GeneratedAt { get; init; }
    public required string OverallSeverity { get; init; }
    public required ApplicationHealthDto Application { get; init; }
    public required DependenciesHealthDto Dependencies { get; init; }
    public required DirectoryHealthDto Directory { get; init; }
    public required List<StorageHealthDto> Storage { get; init; }
    // null when HOST_RUNTIME_HEALTH_ENABLED is false, not Linux, or metrics unreadable
    public HostHealthDto? Host { get; init; }
    public required RuntimeFailuresHealthDto AutomationFailures { get; init; }
    public required RuntimeFailuresHealthDto NotificationFailures { get; init; }
}

// Aggregat ueber kuerzlich fehlgeschlagene Automation-Jobs bzw. Mail-Dispatches.
// Der Admin sieht damit "letzte 24h" + die juengsten Fehler ohne Logsuche.
public sealed class RuntimeFailuresHealthDto
{
    public required string Severity { get; init; }
    public required int WindowHours { get; init; }
    public required int TotalCount { get; init; }
    public required List<RuntimeFailureItemDto> RecentFailures { get; init; }
}

public sealed class RuntimeFailureItemDto
{
    public required long Id { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required string Label { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ApplicationHealthDto
{
    public required string Severity { get; init; }
    public required DateTime ProcessStartedAt { get; init; }
    public required long UptimeSeconds { get; init; }
    public required long ManagedHeapBytes { get; init; }
    public long? ManagedHeapHighThresholdBytes { get; init; }
    public required long WorkingSetBytes { get; init; }
    public ThreadPoolHealthDto? ThreadPool { get; init; }
}

public sealed class ThreadPoolHealthDto
{
    public required int WorkerThreadsAvailable { get; init; }
    public required int CompletionPortThreadsAvailable { get; init; }
}

public sealed class DependenciesHealthDto
{
    public required string Severity { get; init; }
    public required DependencyHealthDto Database { get; init; }
    public required AuthDependencyHealthDto Auth { get; init; }
    public required MailDependencyHealthDto Mail { get; init; }
}

public sealed class DependencyHealthDto
{
    public required string Severity { get; init; }
    public required bool Reachable { get; init; }
    public required DateTime LastCheckedAt { get; init; }
    public long? LatencyMs { get; init; }
    public string? LastError { get; init; }
}

public sealed class AuthDependencyHealthDto
{
    public required string Severity { get; init; }
    public required string Mode { get; init; }
    public required string Reachability { get; init; }
    public required DateTime LastCheckedAt { get; init; }
    public long? LatencyMs { get; init; }
    public string? LastError { get; init; }
}

public sealed class MailDependencyHealthDto
{
    public required string Severity { get; init; }
    public required string Mode { get; init; }
    public required string ConfigurationStatus { get; init; }
    public DateTime? LastProbeAt { get; init; }
    public required string LastProbeStatus { get; init; }
}

public sealed class DirectoryHealthDto
{
    public required string Severity { get; init; }
    public DateTime? LastSyncAt { get; init; }
    public required string LastSyncStatus { get; init; }
    public string? LastError { get; init; }
    public DateTime? NextScheduledSyncAt { get; init; }
    public required int PendingImportsCount { get; init; }
}

public sealed class StorageHealthDto
{
    public required string Label { get; init; }
    public required string Path { get; init; }
    public required long TotalBytes { get; init; }
    public required long FreeBytes { get; init; }
    public required double UsedPercent { get; init; }
    public required string Severity { get; init; }
}

// Host-/VM-Metriken: nur befuellt wenn HOST_RUNTIME_HEALTH_ENABLED=true und Plattform Linux.
// Bewusst getrennt vom Application-Block (App-Prozess vs. Gastgeber-VM).
public sealed class HostHealthDto
{
    public required string Severity { get; init; }
    public required long UptimeSeconds { get; init; }
    // Load Average 1 Minute aus /proc/loadavg — bewusst kein cpuPercent (zwei /proc/stat-Snapshots noetig).
    public required double LoadAverage1m { get; init; }
    public required long MemTotalBytes { get; init; }
    public required long MemAvailableBytes { get; init; }
    public required double MemUsedPercent { get; init; }
    public required long RootFsTotalBytes { get; init; }
    public required long RootFsFreeBytes { get; init; }
    public required double RootFsUsedPercent { get; init; }
    public required int ZombieProcessCount { get; init; }
}
