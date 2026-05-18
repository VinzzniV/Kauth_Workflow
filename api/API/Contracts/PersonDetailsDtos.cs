namespace API;

// Slice 4 (Admin-Gated-Automation, 360°-Karte-Aggregator): Person-zentrierte
// Read-only-Sicht unter /people/{id}/360-view. Aggregiert Identity-Snapshot,
// aktuellen Cloud-Gruppen-Stand (4a), Automation-Provenienz (4b), Mailbox,
// Workflow-Spur und Initial-Passwort-Vault-Status.

public sealed class Person360ViewDto
{
    public required long PersonId { get; init; }
    public required PersonIdentitySnapshotDto IdentitySnapshot { get; init; }
    // 4a: aktueller Stand aus directory_group_members (Entra-Sync). Beantwortet
    // "welche Cloud-Gruppen hat X jetzt?" inkl. manueller Aenderungen und Entziehungen.
    public required IReadOnlyList<PersonCurrentGroupDto> CurrentGroupMemberships { get; init; }
    // 4b: historische Workflow-Spur (payload-basiert aus automation_jobs).
    public required IReadOnlyList<PersonGroupAutomationTraceDto> GroupAutomationTrace { get; init; }
    public PersonMailboxDto? Mailbox { get; init; }
    public required IReadOnlyList<PersonWorkflowTraceDto> WorkflowTrace { get; init; }
    public required IReadOnlyList<PersonInitialPasswordStatusDto> InitialPasswords { get; init; }
}

public sealed class PersonIdentitySnapshotDto
{
    public string? DistinguishedName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? SamAccountName { get; init; }
    public string? Mail { get; init; }
    public string? DisplayName { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public bool? AccountEnabled { get; init; }
}

public sealed class PersonCurrentGroupDto
{
    public required int DirectoryGroupId { get; init; }
    public required string DisplayName { get; init; }
    public string? SourceSystem { get; init; }
    public required DateTime LastSyncedAt { get; init; }
}

public sealed class PersonGroupAutomationTraceDto
{
    public required string GroupDistinguishedName { get; init; }
    public required DateTime IntendedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required string WorkflowUid { get; init; }
    public required string TaskNodeKey { get; init; }
    public required string JobStatus { get; init; }
    public string? AttemptErrorMessage { get; init; }
}

public sealed class PersonMailboxDto
{
    public required string PrimarySmtpAddress { get; init; }
    public required string LicenseSkuId { get; init; }
    public required DateTime AssignedAtUtc { get; init; }
    public required string WorkflowUid { get; init; }
}

public sealed class PersonWorkflowTraceDto
{
    public required long WorkflowId { get; init; }
    public required string WorkflowUid { get; init; }
    public required string DefinitionKey { get; init; }
    public required string Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required int AutomationJobsTotal { get; init; }
    public required int AutomationJobsSucceeded { get; init; }
    public required int AutomationJobsFailed { get; init; }
}

public sealed class PersonInitialPasswordStatusDto
{
    public required Guid VaultId { get; init; }
    public required string CredentialType { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? FirstReadAt { get; init; }
    public required int ReadCount { get; init; }
    public required bool IsExpired { get; init; }
    public required string WorkflowUid { get; init; }
}
