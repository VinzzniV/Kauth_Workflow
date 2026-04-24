using Npgsql;

namespace API;

// Hilfsmethoden, die zwischen PostgresWorkflowRepository und PostgresRotationRepository geteilt werden.
// Diese sind bewusst als internal static gehalten, damit beide Repositories sie ohne gegenseitige
// Klassenkopplung nutzen koennen.
internal static class PostgresRepositorySharedHelpers
{
    public static async Task EnsureAssignableResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE id = @responsibilityId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee responsibility is invalid or inactive.");
        }
    }

    public static async Task EnsureAssignableUserExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT id
FROM app_users
WHERE id = @userId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee user is invalid or inactive.");
        }
    }

    public static async Task EnsureUserHasResponsibility(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        int responsibilityId)
    {
        const string sql = @"
SELECT 1
FROM app_user_responsibilities
WHERE app_user_id = @userId
  AND app_responsibility_id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("The selected user is not directly assigned to the selected responsibility.");
        }
    }

    public static async Task<long?> LoadExplicitResponsibilityOwnerUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT u.id
FROM system_responsibilities sr
JOIN people p ON p.id = sr.responsible_person_id
JOIN app_users u ON u.id = p.app_user_id
WHERE sr.app_responsibility_id = @responsibilityId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    public static async Task<int?> LoadResponsibilityDepartmentOverrideId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT COALESCE(sr.responsible_department_id, r.department_id)
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    public static async Task<long?> ResolvePrimaryAssigneeUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        int departmentId)
    {
        var explicitOwnerUserId = await LoadExplicitResponsibilityOwnerUserId(connection, transaction, responsibilityId);
        if (explicitOwnerUserId.HasValue)
        {
            return explicitOwnerUserId.Value;
        }

        var effectiveDepartmentId = await LoadResponsibilityDepartmentOverrideId(connection, transaction, responsibilityId)
            ?? departmentId;

        const string sql = @"
SELECT u.id
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
JOIN app_user_responsibilities ur ON ur.app_user_id = u.id
LEFT JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
WHERE u.is_active = TRUE
  AND ur.app_responsibility_id = @responsibilityId
  AND (
      r.department_id = @effectiveDepartmentId
      OR r.department_id IS NULL
      OR COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId
      OR COALESCE(p.department_id, u.department_id) IS NULL
  )
ORDER BY CASE WHEN COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId THEN 0 ELSE 1 END, u.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        command.Parameters.AddWithValue("effectiveDepartmentId", effectiveDepartmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    public static async Task<string> LoadAssigneeUserAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT display_name
FROM app_users
WHERE id = @userId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is not string displayName)
        {
            throw new InvalidOperationException("Assignee user label could not be resolved.");
        }

        return displayName;
    }

    public static async Task<string> LoadAssigneeResponsibilityAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT CASE
    WHEN d.name IS NULL OR d.name = '' THEN r.name
    ELSE d.name || ' - ' || r.name
END
FROM app_responsibilities r
LEFT JOIN departments d ON d.id = r.department_id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is not string responsibilityName)
        {
            throw new InvalidOperationException("Assignee responsibility label could not be resolved.");
        }

        return responsibilityName;
    }

    public static WorkflowTargetPersonSourceDto MapWorkflowTargetPersonSource(NpgsqlDataReader reader)
    {
        return new WorkflowTargetPersonSourceDto
        {
            WorkflowUid = reader.GetGuid(0),
            PersonId = reader.GetInt64(1),
            DisplayName = reader.GetString(2),
            FirstName = reader.GetString(3),
            LastName = reader.GetString(4),
            DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
            RoleId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            RoleName = reader.IsDBNull(8) ? null : reader.GetString(8),
            EmployeeNumber = reader.GetInt32(9),
            BadgeNumber = reader.GetInt32(10),
            CompletedAt = reader.GetDateTime(11),
            ArchivedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
        };
    }

    public static async Task<(long UserId, string DisplayName, string Email, string IdentityKey, string PreferredPath)?> LoadActiveUserNotificationRecipient(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
WITH effective_roles AS (
    SELECT r.role_key
    FROM app_user_roles ur
    JOIN app_roles r ON r.id = ur.app_role_id
    WHERE ur.app_user_id = @userId
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
    UNION
    SELECT r.role_key
    FROM app_user_groups ug
    JOIN app_groups g ON g.id = ug.app_group_id
    JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
    JOIN app_roles r ON r.id = gr.app_role_id
    WHERE ug.app_user_id = @userId
      AND g.is_active = TRUE
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
)
SELECT
    u.id,
    u.display_name,
    COALESCE(NULLIF(BTRIM(u.notification_email), ''), u.email) AS target_email,
    COALESCE(NULLIF(BTRIM(u.external_key), ''), u.email) AS identity_key,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key IN ('auth_hr', 'auth_admin', 'auth_reader')
    ) AS can_access_workflow_overview,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key = 'auth_manager'
    ) AS can_access_supervisor
FROM app_users u
WHERE u.id = @userId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var canAccessWorkflowOverview = reader.GetBoolean(4);
        var canAccessSupervisor = reader.GetBoolean(5);

        var preferredPath = canAccessWorkflowOverview
            ? "/workflows"
            : canAccessSupervisor
                ? "/supervisor"
                : "/tasks/my";

        return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), preferredPath);
    }
}
