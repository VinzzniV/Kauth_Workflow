namespace API;

// Read-only-Snapshot der aktiven Laufzeit-Konfiguration der API.
// Wird vom Admin-Workspace im /admin/system/config-Endpoint ausgeliefert.
// Secrets sind redacted: statt Klartext-Wert nur "set"/"unset".
public sealed class SystemConfigSnapshotDto
{
    public required string Environment { get; init; }
    public required bool IsProduction { get; init; }
    public required string AuthMode { get; init; }
    public required bool SwaggerEnabled { get; init; }
    public required string? PublicBaseUrl { get; init; }
    public required SystemConfigEntraDto Entra { get; init; }
    public required SystemConfigDirectoryDto Directory { get; init; }
    public required SystemConfigEmailDto Email { get; init; }
    public required SystemConfigVaultDto Vault { get; init; }
    public required SystemConfigRetryDto Retry { get; init; }
    public required SystemConfigWorkerLeaseDto WorkerLease { get; init; }
    public required SystemConfigHostHealthDto HostHealth { get; init; }
}

public sealed class SystemConfigEntraDto
{
    public required string? TenantId { get; init; }
    public required string? ClientId { get; init; }
    public required string? Audience { get; init; }
    // Redaction: "set" wenn nicht-leer, sonst "unset".
    public required string ClientSecretStatus { get; init; }
    public required string GraphClientSecretStatus { get; init; }
}

public sealed class SystemConfigDirectoryDto
{
    public required bool SyncScheduled { get; init; }
    public required int SyncIntervalMinutes { get; init; }
    public required string? GroupPrefix { get; init; }
    public required string? ExplicitGroupIds { get; init; }
    public required string? AutoProvisionDefaultRoleKey { get; init; }
}

public sealed class SystemConfigEmailDto
{
    public required bool Enabled { get; init; }
    public required string Provider { get; init; }
    public required string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public required bool SaveToSentItems { get; init; }
}

public sealed class SystemConfigVaultDto
{
    // Redaction: "present" oder "missing"; kein Klartext-Key.
    public required string KeyStatus { get; init; }
}

public sealed class SystemConfigRetryDto
{
    public required int MaxAttempts { get; init; }
    public required int FirstRetryDelaySeconds { get; init; }
    public required int SubsequentRetryDelaySeconds { get; init; }
}

public sealed class SystemConfigWorkerLeaseDto
{
    public required int StaleClaimTimeoutMinutes { get; init; }
}

public sealed class SystemConfigHostHealthDto
{
    public required bool Enabled { get; init; }
    public required string? ProcfsPath { get; init; }
    public required string? RootPath { get; init; }
    public required string? StoragePaths { get; init; }
}
