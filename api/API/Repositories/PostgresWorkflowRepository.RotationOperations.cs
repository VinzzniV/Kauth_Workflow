using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<WorkflowTargetPersonSourceDto?> GetCompletedOnboardingSource(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    w.uid,
    matched_person.person_id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(matched_person.first_name, w.first_name), COALESCE(matched_person.last_name, w.last_name))), ''),
        matched_person.display_name,
        'Person #' || matched_person.person_id::text
    ) AS display_name,
    COALESCE(matched_person.first_name, w.first_name) AS first_name,
    COALESCE(matched_person.last_name, w.last_name) AS last_name,
    COALESCE(w.department_id, matched_person.department_id, matched_person.user_department_id) AS department_id,
    d.name AS department_name,
    w.position_role_id,
    r.name AS role_name,
    w.employee_number,
    w.badge_number,
    COALESCE(w.completed_at, w.created_at) AS completed_at,
    w.archived_at
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id
JOIN LATERAL (
    SELECT
        p.id AS person_id,
        p.first_name,
        p.last_name,
        p.department_id,
        u.display_name,
        u.department_id AS user_department_id
    FROM people p
    LEFT JOIN app_users u ON u.id = p.app_user_id
    WHERE w.target_person_id = p.id
       OR (p.employee_number IS NOT NULL AND w.employee_number = p.employee_number)
       OR (
            w.employee_number > 0
            AND TRIM(COALESCE(w.first_name, '') || ' ' || COALESCE(w.last_name, '')) =
                COALESCE(
                    NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                    u.display_name,
                    'Person #' || p.id::text
                )
       )
    ORDER BY
        CASE
            WHEN w.target_person_id = p.id THEN 0
            WHEN p.employee_number IS NOT NULL AND w.employee_number = p.employee_number THEN 1
            ELSE 2
        END,
        p.id
    LIMIT 1
) matched_person ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(w.department_id, matched_person.department_id, matched_person.user_department_id)
LEFT JOIN app_roles r ON r.id = w.position_role_id
WHERE w.uid = @workflowUid
  AND pt.key = 'onboarding'
  AND (w.workflow_definition_version_id IS NULL OR vpt.key = 'onboarding')
  AND w.status = 'completed'
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowUid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapWorkflowTargetPersonSource(reader);
    }

    public async Task<RotationPlanConflictState> GetRotationPlanConflictState(long personId, Guid sourceWorkflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    EXISTS(
        SELECT 1
        FROM rotation_plans
        WHERE person_id = @personId
          AND status = 'active'
    ),
    EXISTS(
        SELECT 1
        FROM rotation_plans rp
        JOIN workflows w ON w.id = rp.source_workflow_id
        WHERE w.uid = @sourceWorkflowUid
          AND rp.status IN ('draft', 'active')
    );";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("personId", personId);
        command.Parameters.AddWithValue("sourceWorkflowUid", sourceWorkflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        await reader.ReadAsync();
        return new RotationPlanConflictState
        {
            HasActivePlanForPerson = reader.GetBoolean(0),
            HasOpenPlanForSourceWorkflow = reader.GetBoolean(1)
        };
    }

    public async Task<List<RotationPlanListItemDto>> GetRotationPlans(
        long? personId,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    rp.id,
    rp.person_id,
    w.uid,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.first_name, w.first_name) AS first_name,
    COALESCE(p.last_name, w.last_name) AS last_name,
    COALESCE(w.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    rp.title,
    rp.status,
    rp.created_by_user_id,
    rp.created_at,
    rp.updated_at,
    COUNT(rs.id) AS station_count
FROM rotation_plans rp
JOIN people p ON p.id = rp.person_id
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments d ON d.id = COALESCE(w.department_id, p.department_id, u.department_id)
LEFT JOIN rotation_stations rs ON rs.rotation_plan_id = rp.id
WHERE (@personId IS NULL OR rp.person_id = @personId)
  AND (@departmentIds IS NULL OR COALESCE(w.department_id, p.department_id, u.department_id) = ANY(@departmentIds))
GROUP BY
    rp.id,
    rp.person_id,
    w.uid,
    p.id,
    p.first_name,
    p.last_name,
    w.first_name,
    w.last_name,
    u.display_name,
    w.department_id,
    p.department_id,
    u.department_id,
    d.name,
    rp.title,
    rp.status,
    rp.created_by_user_id,
    rp.created_at,
    rp.updated_at
ORDER BY rp.created_at DESC, rp.id DESC;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("personId", NpgsqlDbType.Bigint).Value = (object?)personId ?? DBNull.Value;
        command.Parameters.Add("departmentIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
            observableDepartmentIds is null ? DBNull.Value : observableDepartmentIds.ToArray();

        await using var reader = await command.ExecuteReaderAsync();
        var plans = new List<RotationPlanListItemDto>();
        while (await reader.ReadAsync())
        {
            plans.Add(MapRotationPlanListItem(reader));
        }

        return plans;
    }

    public async Task<RotationPlanDetailDto?> GetRotationPlan(long planId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    rp.id,
    rp.person_id,
    w.uid,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.first_name, w.first_name) AS first_name,
    COALESCE(p.last_name, w.last_name) AS last_name,
    COALESCE(w.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    rp.title,
    rp.status,
    rp.created_by_user_id,
    rp.created_at,
    rp.updated_at
FROM rotation_plans rp
JOIN people p ON p.id = rp.person_id
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments d ON d.id = COALESCE(w.department_id, p.department_id, u.department_id)
WHERE rp.id = @planId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("planId", planId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var plan = MapRotationPlanDetail(reader);
        await reader.CloseAsync();
        plan.Stations = await LoadRotationStations(connection, null, plan.Id);
        return plan;
    }

    public async Task<RotationPlanDetailDto> CreateRotationPlan(CreateRotationPlanRequest request, long createdByUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string sql = @"
WITH source_workflow AS (
    SELECT w.id
    FROM workflows w
    JOIN process_types pt ON pt.id = w.process_type_id
    LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
    LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id
    WHERE w.uid = @sourceWorkflowUid
      AND pt.key = 'onboarding'
      AND (w.workflow_definition_version_id IS NULL OR vpt.key = 'onboarding')
      AND w.status = 'completed'
      AND (
            w.target_person_id = @personId
            OR EXISTS (
                SELECT 1
                FROM people p
                WHERE p.id = @personId
                  AND p.employee_number IS NOT NULL
                  AND w.employee_number = p.employee_number
            )
            OR EXISTS (
                SELECT 1
                FROM people p
                LEFT JOIN app_users u ON u.id = p.app_user_id
                WHERE p.id = @personId
                  AND w.employee_number > 0
                  AND TRIM(COALESCE(w.first_name, '') || ' ' || COALESCE(w.last_name, '')) =
                        COALESCE(
                            NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                            u.display_name,
                            'Person #' || p.id::text
                        )
            )
      )
    LIMIT 1
)
INSERT INTO rotation_plans (
    person_id,
    source_workflow_id,
    title,
    status,
    created_by_user_id
)
SELECT
    @personId,
    source_workflow.id,
    @title,
    @status,
    @createdByUserId
FROM source_workflow
RETURNING id;";

        long createdPlanId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("personId", request.PersonId);
            command.Parameters.AddWithValue("sourceWorkflowUid", request.SourceWorkflowUid);
            command.Parameters.AddWithValue("title", request.Title!);
            command.Parameters.AddWithValue("status", request.Status!);
            command.Parameters.AddWithValue("createdByUserId", createdByUserId);

            var scalar = await command.ExecuteScalarAsync();
            if (scalar is not long planId)
            {
                throw new InvalidOperationException(
                    "Der Durchlaufplan konnte nicht erstellt werden, weil das abgeschlossene Onboarding nicht valide ist.");
            }

            createdPlanId = planId;
        }

        await InsertRotationAuditEntry(
            connection,
            transaction,
            createdPlanId,
            null,
            null,
            createdByUserId,
            "rotation_plan_created",
            null,
            CreateRotationPlanAuditSnapshot(createdPlanId, request),
            request.Title);

        await transaction.CommitAsync();
        var createdPlan = await GetRotationPlan(createdPlanId);
        return createdPlan ?? throw new InvalidOperationException("Created rotation plan could not be loaded afterwards.");
    }

    public async Task<List<RotationStationDto>> GetRotationStations(long planId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await LoadRotationStations(connection, null, planId);
    }

    public async Task<RotationStationDto?> GetRotationStation(long stationId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    rs.id,
    rs.rotation_plan_id,
    rs.department_id,
    d.name,
    rs.start_date,
    rs.end_date,
    rs.order_index,
    rs.location,
    rs.notes,
    rs.status,
    rs.created_at,
    rs.updated_at
FROM rotation_stations rs
JOIN departments d ON d.id = rs.department_id
WHERE rs.id = @stationId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("stationId", stationId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRotationStation(reader) : null;
    }

    public async Task<RotationStationDto?> CreateRotationStation(long planId, RotationStationUpsertRequest request, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string sql = @"
INSERT INTO rotation_stations (
    rotation_plan_id,
    department_id,
    start_date,
    end_date,
    order_index,
    location,
    notes,
    status
)
VALUES (
    @planId,
    @departmentId,
    @startDate,
    @endDate,
    @orderIndex,
    @location,
    @notes,
    @status
)
RETURNING id;";

        long createdStationId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("planId", planId);
            command.Parameters.AddWithValue("departmentId", request.DepartmentId);
            command.Parameters.AddWithValue("startDate", request.StartDate);
            command.Parameters.AddWithValue("endDate", request.EndDate);
            command.Parameters.AddWithValue("orderIndex", request.OrderIndex);
            command.Parameters.AddWithValue("location", (object?)request.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("status", request.Status!);
            var scalar = await command.ExecuteScalarAsync();
            if (scalar is not long stationId)
            {
                return null;
            }

            createdStationId = stationId;
        }

        var createdStation = await LoadRotationStationById(connection, transaction, createdStationId);
        if (createdStation is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        await InsertRotationAuditEntry(
            connection,
            transaction,
            planId,
            createdStationId,
            null,
            actorUserId,
            "rotation_station_created",
            null,
            CreateRotationStationAuditSnapshot(createdStation),
            createdStation.DepartmentName);

        await transaction.CommitAsync();
        return createdStation;
    }

    public async Task<RotationStationDto?> UpdateRotationStation(long stationId, RotationStationUpsertRequest request, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var currentStation = await LoadRotationStationById(connection, transaction, stationId);
        if (currentStation is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        const string sql = @"
UPDATE rotation_stations
SET
    department_id = @departmentId,
    start_date = @startDate,
    end_date = @endDate,
    order_index = @orderIndex,
    location = @location,
    notes = @notes,
    status = @status,
    updated_at = NOW()
WHERE id = @stationId
RETURNING id;";

        var updated = false;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("stationId", stationId);
            command.Parameters.AddWithValue("departmentId", request.DepartmentId);
            command.Parameters.AddWithValue("startDate", request.StartDate);
            command.Parameters.AddWithValue("endDate", request.EndDate);
            command.Parameters.AddWithValue("orderIndex", request.OrderIndex);
            command.Parameters.AddWithValue("location", (object?)request.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("status", request.Status!);
            updated = await command.ExecuteScalarAsync() is long;
        }

        if (!updated)
        {
            await transaction.RollbackAsync();
            return null;
        }

        var updatedStation = await LoadRotationStationById(connection, transaction, stationId);
        if (updatedStation is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        await InsertRotationAuditEntry(
            connection,
            transaction,
            updatedStation.RotationPlanId,
            stationId,
            null,
            actorUserId,
            "rotation_station_updated",
            CreateRotationStationAuditSnapshot(currentStation),
            CreateRotationStationAuditSnapshot(updatedStation),
            updatedStation.DepartmentName);

        await transaction.CommitAsync();
        return updatedStation;
    }

    public async Task<bool> DeleteRotationStation(long stationId, long actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var currentStation = await LoadRotationStationById(connection, transaction, stationId);
        if (currentStation is null)
        {
            await transaction.RollbackAsync();
            return false;
        }

        const string sql = @"
DELETE FROM rotation_stations
WHERE id = @stationId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("stationId", stationId);
        var deleted = await command.ExecuteNonQueryAsync() > 0;
        if (!deleted)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await InsertRotationAuditEntry(
            connection,
            transaction,
            currentStation.RotationPlanId,
            stationId,
            null,
            actorUserId,
            "rotation_station_deleted",
            CreateRotationStationAuditSnapshot(currentStation),
            null,
            currentStation.DepartmentName);

        await transaction.CommitAsync();
        return true;
    }

    private static object CreateRotationPlanAuditSnapshot(long planId, CreateRotationPlanRequest request)
    {
        return new
        {
            id = planId,
            request.PersonId,
            request.SourceWorkflowUid,
            request.Title,
            request.Status
        };
    }

    private static object CreateRotationStationAuditSnapshot(RotationStationDto station)
    {
        return new
        {
            station.Id,
            station.RotationPlanId,
            station.DepartmentId,
            station.DepartmentName,
            station.StartDate,
            station.EndDate,
            station.OrderIndex,
            station.Location,
            station.Notes,
            station.Status
        };
    }

    private static async Task<RotationStationDto?> LoadRotationStationById(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long stationId)
    {
        const string sql = @"
SELECT
    rs.id,
    rs.rotation_plan_id,
    rs.department_id,
    d.name,
    rs.start_date,
    rs.end_date,
    rs.order_index,
    rs.location,
    rs.notes,
    rs.status,
    rs.created_at,
    rs.updated_at
FROM rotation_stations rs
JOIN departments d ON d.id = rs.department_id
WHERE rs.id = @stationId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("stationId", stationId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRotationStation(reader) : null;
    }

    private static async Task<List<RotationStationDto>> LoadRotationStations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long planId)
    {
        const string sql = @"
SELECT
    rs.id,
    rs.rotation_plan_id,
    rs.department_id,
    d.name,
    rs.start_date,
    rs.end_date,
    rs.order_index,
    rs.location,
    rs.notes,
    rs.status,
    rs.created_at,
    rs.updated_at
FROM rotation_stations rs
JOIN departments d ON d.id = rs.department_id
WHERE rs.rotation_plan_id = @planId
ORDER BY rs.order_index, rs.start_date, rs.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("planId", planId);
        await using var reader = await command.ExecuteReaderAsync();

        var stations = new List<RotationStationDto>();
        while (await reader.ReadAsync())
        {
            stations.Add(MapRotationStation(reader));
        }

        return stations;
    }

    private static RotationPlanListItemDto MapRotationPlanListItem(NpgsqlDataReader reader)
    {
        return new RotationPlanListItemDto
        {
            Id = reader.GetInt64(0),
            PersonId = reader.GetInt64(1),
            SourceWorkflowUid = reader.GetGuid(2),
            DisplayName = reader.GetString(3),
            FirstName = reader.IsDBNull(4) ? null : reader.GetString(4),
            LastName = reader.IsDBNull(5) ? null : reader.GetString(5),
            DepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            DepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
            Title = reader.GetString(8),
            Status = reader.GetString(9),
            CreatedByUserId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            CreatedAt = reader.GetDateTime(11),
            UpdatedAt = reader.GetDateTime(12),
            StationCount = reader.GetInt64(13)
        };
    }

    private static RotationPlanDetailDto MapRotationPlanDetail(NpgsqlDataReader reader)
    {
        return new RotationPlanDetailDto
        {
            Id = reader.GetInt64(0),
            PersonId = reader.GetInt64(1),
            SourceWorkflowUid = reader.GetGuid(2),
            DisplayName = reader.GetString(3),
            FirstName = reader.IsDBNull(4) ? null : reader.GetString(4),
            LastName = reader.IsDBNull(5) ? null : reader.GetString(5),
            DepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            DepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
            Title = reader.GetString(8),
            Status = reader.GetString(9),
            CreatedByUserId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            CreatedAt = reader.GetDateTime(11),
            UpdatedAt = reader.GetDateTime(12),
            Stations = []
        };
    }

    private static RotationStationDto MapRotationStation(NpgsqlDataReader reader)
    {
        return new RotationStationDto
        {
            Id = reader.GetInt64(0),
            RotationPlanId = reader.GetInt64(1),
            DepartmentId = reader.GetInt32(2),
            DepartmentName = reader.GetString(3),
            StartDate = reader.GetFieldValue<DateOnly>(4),
            EndDate = reader.GetFieldValue<DateOnly>(5),
            OrderIndex = reader.GetInt32(6),
            Location = reader.IsDBNull(7) ? null : reader.GetString(7),
            Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
            Status = reader.GetString(9),
            CreatedAt = reader.GetDateTime(10),
            UpdatedAt = reader.GetDateTime(11)
        };
    }
}
