namespace API;

// Z16-S2 + C: Listenvertrag fuer den kuenftigen Personenbereich (/people, HR + Admin).
// PersonId ist null fuer directory_only-Eintraege (Entra-Identitaeten ohne people-Record).
// DirectoryIdentityId ist nur bei directory_only gesetzt; fuer echte people-Records null.
public sealed class PersonDirectoryItemDto
{
    public long? PersonId { get; init; }
    public long? DirectoryIdentityId { get; init; }
    public required string DisplayName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public string? JobTitle { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public string? EmploymentStatus { get; init; }
    public DateOnly? EntryDate { get; init; }
    public DateOnly? ExitDate { get; init; }
    public string? DirectoryLinkStatus { get; init; }
}

// A1: Directory-Identitaeten ohne people-Record — Basis fuer retroaktiven Import-Flow.
public sealed class UnlinkedDirectoryIdentityDto
{
    public required long DirectoryIdentityId { get; init; }
    public required Guid EntraObjectId { get; init; }
    public required string DisplayName { get; init; }
    public string? Mail { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DepartmentName { get; init; }
    public int? PreviewDepartmentId { get; init; }
    public int? EmployeeNumber { get; init; }
    public string? JobTitle { get; init; }
    public required bool AccountEnabled { get; init; }
    public long? AppUserId { get; init; }
    public required bool HasLinkedAppUser { get; init; }
}

// A1: Anforderung fuer POST /admin/people/import-from-directory.
public sealed class ImportPeopleFromDirectoryRequest
{
    public required List<long> DirectoryIdentityIds { get; init; }
}

// A1: Ergebnis fuer einen einzelnen Import-Eintrag.
// Outcome: "created" | "linked" | "skipped"
public sealed class ImportPeopleResultItemDto
{
    public required long DirectoryIdentityId { get; init; }
    public required string DisplayName { get; init; }
    public required string Outcome { get; init; }
    public long? PersonId { get; init; }
    public string? SkipReason { get; init; }
}

// A1: Gesamtergebnis des Import-Requests.
public sealed class ImportPeopleFromDirectoryResultDto
{
    public int CreatedCount { get; init; }
    public int LinkedCount { get; init; }
    public int SkippedCount { get; init; }
    public required List<ImportPeopleResultItemDto> Results { get; init; }
}

// A3: Inline-Bearbeitung fehlender Stammdaten auf der Mitarbeiterkarte (Admin).
// Aktualisiert Eintrittsdatum und Ausweisnummer; null loescht den Wert.
public sealed class UpdatePersonRequest
{
    public DateOnly? EntryDate { get; init; }
    public int? BadgeNumber { get; init; }
}
