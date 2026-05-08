namespace API;

// Z16-S2: Listenvertrag fuer den kuenftigen Personenbereich (/people, HR + Admin).
// Kompaktes DTO ohne Workflow-History — nur Identifikation und Statusfelder fuer die Liste.
public sealed class PersonDirectoryItemDto
{
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public string? EmploymentStatus { get; init; }
    public DateOnly? EntryDate { get; init; }
    public DateOnly? ExitDate { get; init; }
    public string? DirectoryLinkStatus { get; init; }
}
