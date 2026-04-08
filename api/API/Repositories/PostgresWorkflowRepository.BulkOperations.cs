using Npgsql;

namespace API;

// Bulk-Operationen: Massenhafte Workflow-Erstellung, z.B. Abteilungswechsel
// fuer alle Mitarbeiter bei Abteilungsauflösung.
internal sealed partial class PostgresWorkflowRepository
{
    public async Task<BulkOperationResultDto> BulkCreateDepartmentChangeWorkflows(
        BulkDepartmentChangeRequest request,
        long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        // Alle Personen in der Quell-Abteilung laden, mit letztem Workflow fuer Personalnummer/Badge.
        const string employeeSql = @"
SELECT
    p.id              AS person_id,
    u.display_name,
    u.id              AS app_user_id,
    -- Personalnummer und Badge aus dem letzten Workflow der Person ableiten
    latest.employee_number,
    latest.badge_number,
    latest.first_name,
    latest.last_name,
    -- Prüfen ob bereits ein aktiver department_change-Workflow existiert
    CASE WHEN active_dc.id IS NOT NULL THEN TRUE ELSE FALSE END AS has_active_dc
FROM people p
JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN LATERAL (
    SELECT w.employee_number, w.badge_number, w.first_name, w.last_name
    FROM workflows w
    WHERE (w.target_person_id = p.id
           OR (w.first_name || ' ' || w.last_name = u.display_name AND w.employee_number > 0))
    ORDER BY w.created_at DESC
    LIMIT 1
) latest ON TRUE
LEFT JOIN LATERAL (
    SELECT w.id
    FROM workflows w
    JOIN process_types pt ON pt.id = w.process_type_id AND pt.key = 'department_change'
    WHERE w.target_person_id = p.id
      AND w.status NOT IN ('completed')
    LIMIT 1
) active_dc ON TRUE
WHERE p.department_id = @sourceDeptId
  AND u.is_active = TRUE
ORDER BY u.display_name;";

        await using var cmd = new NpgsqlCommand(employeeSql, connection);
        cmd.Parameters.AddWithValue("@sourceDeptId", request.SourceDepartmentId);
        await using var reader = await cmd.ExecuteReaderAsync();

        var employees = new List<BulkEmployeeRecord>();
        while (await reader.ReadAsync())
        {
            employees.Add(new BulkEmployeeRecord
            {
                PersonId = reader.GetInt64(0),
                DisplayName = reader.GetString(1),
                AppUserId = reader.GetInt64(2),
                EmployeeNumber = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                BadgeNumber = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                FirstName = reader.IsDBNull(5) ? null : reader.GetString(5),
                LastName = reader.IsDBNull(6) ? null : reader.GetString(6),
                HasActiveDepartmentChange = reader.GetBoolean(7),
            });
        }

        await reader.CloseAsync();

        var items = new List<BulkOperationItemDto>();
        var createdCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var emp in employees)
        {
            // Überspringen: bereits aktiver Abteilungswechsel
            if (emp.HasActiveDepartmentChange)
            {
                items.Add(new BulkOperationItemDto
                {
                    PersonId = emp.PersonId,
                    DisplayName = emp.DisplayName,
                    Status = "skipped",
                    ErrorMessage = "Aktiver Abteilungswechsel-Vorgang vorhanden."
                });
                skippedCount++;
                continue;
            }

            // Überspringen: keine Personalnummer/Badge aus vorherigem Workflow
            if (emp.EmployeeNumber is null || emp.BadgeNumber is null)
            {
                items.Add(new BulkOperationItemDto
                {
                    PersonId = emp.PersonId,
                    DisplayName = emp.DisplayName,
                    Status = "skipped",
                    ErrorMessage = "Keine Personalnummer/Badge aus vorherigem Vorgang ableitbar."
                });
                skippedCount++;
                continue;
            }

            if (request.DryRun)
            {
                items.Add(new BulkOperationItemDto
                {
                    PersonId = emp.PersonId,
                    DisplayName = emp.DisplayName,
                    Status = "would_create",
                });
                createdCount++;
                continue;
            }

            // Name ableiten: entweder aus letztem Workflow oder Display-Name splitten
            var firstName = emp.FirstName ?? emp.DisplayName.Split(' ').FirstOrDefault() ?? emp.DisplayName;
            var lastName = emp.LastName ?? emp.DisplayName.Split(' ').Skip(1).FirstOrDefault() ?? "";

            var createRequest = new CreateWorkflowDefinitionInstanceRequest
            {
                WorkflowDefinitionKey = "department_change",
                DepartmentId = request.TargetDepartmentId,
                RoleId = request.TargetRoleId,
                TargetPersonId = emp.PersonId,
                FirstName = firstName,
                LastName = lastName,
                EmployeeNumber = emp.EmployeeNumber.Value,
                BadgeNumber = emp.BadgeNumber.Value,
                DeadlineDate = request.DeadlineDate,
            };

            try
            {
                var result = await CreateWorkflowDefinitionInstance(createRequest, actorUserId);
                items.Add(new BulkOperationItemDto
                {
                    PersonId = emp.PersonId,
                    DisplayName = emp.DisplayName,
                    Status = "created",
                    WorkflowUid = result.WorkflowUid,
                });
                createdCount++;
            }
            catch (Exception ex)
            {
                items.Add(new BulkOperationItemDto
                {
                    PersonId = emp.PersonId,
                    DisplayName = emp.DisplayName,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                });
                failedCount++;
            }
        }

        return new BulkOperationResultDto
        {
            TotalEmployees = employees.Count,
            CreatedWorkflows = createdCount,
            SkippedEmployees = skippedCount,
            FailedEmployees = failedCount,
            IsDryRun = request.DryRun,
            Items = items,
        };
    }

    private sealed class BulkEmployeeRecord
    {
        public required long PersonId { get; init; }
        public required string DisplayName { get; init; }
        public required long AppUserId { get; init; }
        public int? EmployeeNumber { get; init; }
        public int? BadgeNumber { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public required bool HasActiveDepartmentChange { get; init; }
    }
}
