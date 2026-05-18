using System.Text.Json;
using Npgsql;

namespace API;

// Slice 4 (Admin-Gated-Automation, 360°-Karte): Liefert die person-zentrierte
// Aggregator-Sicht fuer GET /people/{id}/360-view. Auth-Vertrag spiegelt
// /people/{id}/workflows (CanAccessWorkflowOverview + Abteilungs-Scope), damit
// kein partieller 403-Regressionsweg auf der Person-Detail-Seite entsteht.
internal sealed class PersonDetailsReadService
{
    private readonly LifecycleRuntimeSettings runtimeSettings;
    private readonly IWorkflowVisibilityService workflowVisibilityService;

    public PersonDetailsReadService(
        LifecycleRuntimeSettings runtimeSettings,
        IWorkflowVisibilityService workflowVisibilityService)
    {
        this.runtimeSettings = runtimeSettings;
        this.workflowVisibilityService = workflowVisibilityService;
    }

    public async Task<Person360ViewDto?> GetAsync(long personId, CurrentUser currentUser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);

        // 1. Stamm + Auth-Check via Abteilungs-Scope (analog WorkflowRuntimeService.GetPersonWorkflowHistoryAsync).
        var stamm = await LoadPersonStammAsync(connection, personId, ct);
        if (stamm is null) return null;

        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (observableDepartmentIds is not null
            && (!stamm.DepartmentId.HasValue || !observableDepartmentIds.Contains(stamm.DepartmentId.Value)))
        {
            throw new UnauthorizedAccessException("Die Person liegt außerhalb Ihrer freigegebenen Abteilungen.");
        }

        // 2-7. Sub-Reads, parallel-vorbereiten via getrennte SQLs auf derselben Connection (sequentiell, weil ein NpgsqlConnection nicht concurrent-safe ist).
        var identity = await LoadIdentitySnapshotAsync(connection, stamm.DirectoryIdentityId, ct);
        var currentGroups = await LoadCurrentGroupMembershipsAsync(connection, stamm.DirectoryIdentityId, ct);
        var groupTrace = await LoadGroupAutomationTraceAsync(connection, personId, ct);
        var mailbox = await LoadLatestMailboxAsync(connection, personId, ct);
        var workflowTrace = await LoadWorkflowTraceAsync(connection, personId, ct);
        var initialPasswords = await LoadInitialPasswordStatusAsync(connection, personId, ct);

        return new Person360ViewDto
        {
            PersonId = personId,
            IdentitySnapshot = identity,
            CurrentGroupMemberships = currentGroups,
            GroupAutomationTrace = groupTrace,
            Mailbox = mailbox,
            WorkflowTrace = workflowTrace,
            InitialPasswords = initialPasswords,
        };
    }

    private static async Task<PersonStammRecord?> LoadPersonStammAsync(
        NpgsqlConnection connection, long personId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id, p.department_id, p.directory_identity_id
            FROM public.people p
            WHERE p.id = @personId
            LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new PersonStammRecord(
            PersonId: reader.GetInt64(0),
            DepartmentId: reader.IsDBNull(1) ? null : reader.GetInt32(1),
            DirectoryIdentityId: reader.IsDBNull(2) ? null : reader.GetInt64(2));
    }

    private static async Task<PersonIdentitySnapshotDto> LoadIdentitySnapshotAsync(
        NpgsqlConnection connection, long? directoryIdentityId, CancellationToken ct)
    {
        if (directoryIdentityId is null)
        {
            return new PersonIdentitySnapshotDto();
        }

        const string sql = """
            SELECT user_principal_name, mail, display_name, department_name, job_title, account_enabled
            FROM public.directory_identities
            WHERE id = @id
            LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", directoryIdentityId.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new PersonIdentitySnapshotDto();

        return new PersonIdentitySnapshotDto
        {
            UserPrincipalName = reader.IsDBNull(0) ? null : reader.GetString(0),
            Mail = reader.IsDBNull(1) ? null : reader.GetString(1),
            DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
            Department = reader.IsDBNull(3) ? null : reader.GetString(3),
            JobTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
            AccountEnabled = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
            // DN/SAM kommen aus dem letzten created_ad_user-Output (separate Aggregation moeglich,
            // hier konservativ leer — Folge-Slice kann das nachziehen, sobald der Worker einen
            // strukturierten Success-Output schreibt).
            DistinguishedName = null,
            SamAccountName = null,
        };
    }

    private static async Task<IReadOnlyList<PersonCurrentGroupDto>> LoadCurrentGroupMembershipsAsync(
        NpgsqlConnection connection, long? directoryIdentityId, CancellationToken ct)
    {
        if (directoryIdentityId is null) return Array.Empty<PersonCurrentGroupDto>();

        const string sql = """
            SELECT dg.id, dg.display_name, dg.source_system, dgm.synced_at
            FROM public.directory_group_members dgm
            INNER JOIN public.directory_groups dg ON dg.id = dgm.directory_group_id
            WHERE dgm.directory_identity_id = @id
            ORDER BY dg.display_name
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", directoryIdentityId.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<PersonCurrentGroupDto>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new PersonCurrentGroupDto
            {
                DirectoryGroupId = reader.GetInt32(0),
                DisplayName = reader.GetString(1),
                SourceSystem = reader.IsDBNull(2) ? null : reader.GetString(2),
                LastSyncedAt = reader.GetDateTime(3),
            });
        }
        return result;
    }

    // 4b: Payload-basierter Read — kein output_json-Vertrag fuer AssignGroupsLdaps
    // verfuegbar (Worker schreibt heute keinen strukturierten Success-Output;
    // siehe Plan-File Begruendung). Daher aus automation_jobs.payload_json
    // + letztem Attempt-Status. Best-Effort; ohne Match zu directory_groups
    // bleibt der Eintrag ohne Aktuell-Markierung im UI.
    private static async Task<IReadOnlyList<PersonGroupAutomationTraceDto>> LoadGroupAutomationTraceAsync(
        NpgsqlConnection connection, long personId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                j.payload_json::text,
                j.created_at,
                j.status,
                n.node_key,
                w.uid,
                latest_attempt.completed_at,
                latest_attempt.error_message
            FROM public.automation_jobs j
            INNER JOIN public.workflows w ON w.id = j.workflow_id
            INNER JOIN public.action_definitions ad ON ad.id = j.action_definition_id
            INNER JOIN public.workflow_node_instances ni ON ni.id = j.workflow_node_instance_id
            INNER JOIN public.workflow_nodes n ON n.id = ni.workflow_node_id
            LEFT JOIN LATERAL (
                SELECT completed_at, error_message
                FROM public.automation_job_attempts a
                WHERE a.automation_job_id = j.id
                ORDER BY a.attempt_number DESC, a.id DESC
                LIMIT 1
            ) latest_attempt ON TRUE
            WHERE w.target_person_id = @personId
              AND ad.action_key = 'AssignGroupsLdaps'
            ORDER BY j.created_at DESC, j.id DESC
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new List<PersonGroupAutomationTraceDto>();
        while (await reader.ReadAsync(ct))
        {
            var payloadText = reader.IsDBNull(0) ? null : reader.GetString(0);
            var jobCreatedAt = reader.GetDateTime(1);
            var jobStatus = reader.GetString(2);
            var nodeKey = reader.GetString(3);
            var workflowUid = reader.GetGuid(4).ToString();
            var completedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
            var errorMessage = reader.IsDBNull(6) ? null : reader.GetString(6);

            // Payload kann eine groupDistinguishedNames-Liste tragen; bei Drift einfach leere
            // Liste, damit der Endpoint nicht crasht.
            var groupDns = ExtractGroupDistinguishedNamesFromPayload(payloadText);
            foreach (var dn in groupDns)
            {
                result.Add(new PersonGroupAutomationTraceDto
                {
                    GroupDistinguishedName = dn,
                    IntendedAt = jobCreatedAt,
                    CompletedAt = completedAt,
                    WorkflowUid = workflowUid,
                    TaskNodeKey = nodeKey,
                    JobStatus = jobStatus,
                    AttemptErrorMessage = errorMessage,
                });
            }
        }
        return result;
    }

    private static IReadOnlyList<string> ExtractGroupDistinguishedNamesFromPayload(string? payloadText)
    {
        if (string.IsNullOrWhiteSpace(payloadText)) return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(payloadText);
            if (!doc.RootElement.TryGetProperty("groupDistinguishedNames", out var arr)) return Array.Empty<string>();
            if (arr.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
            var list = new List<string>(arr.GetArrayLength());
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static async Task<PersonMailboxDto?> LoadLatestMailboxAsync(
        NpgsqlConnection connection, long personId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                a.output_json::text,
                a.completed_at,
                w.uid
            FROM public.automation_jobs j
            INNER JOIN public.workflows w ON w.id = j.workflow_id
            INNER JOIN public.action_definitions ad ON ad.id = j.action_definition_id
            INNER JOIN public.automation_job_attempts a ON a.automation_job_id = j.id
            WHERE w.target_person_id = @personId
              AND ad.action_key = 'CreateMailboxGraph'
              AND a.status = 'succeeded'
            ORDER BY a.completed_at DESC NULLS LAST, a.id DESC
            LIMIT 1
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var outputText = reader.IsDBNull(0) ? null : reader.GetString(0);
        var completedAt = reader.GetDateTime(1);
        var workflowUid = reader.GetGuid(2).ToString();

        if (string.IsNullOrWhiteSpace(outputText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(outputText);
            var root = doc.RootElement;
            var smtp = root.TryGetProperty("primarySmtpAddress", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            var skuId = root.TryGetProperty("licenseSkuId", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            if (string.IsNullOrWhiteSpace(smtp) || string.IsNullOrWhiteSpace(skuId)) return null;
            return new PersonMailboxDto
            {
                PrimarySmtpAddress = smtp!,
                LicenseSkuId = skuId!,
                AssignedAtUtc = completedAt,
                WorkflowUid = workflowUid,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<PersonWorkflowTraceDto>> LoadWorkflowTraceAsync(
        NpgsqlConnection connection, long personId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                w.id, w.uid, wd.definition_key, w.status, w.created_at, w.completed_at,
                COALESCE(j_stats.total, 0)     AS jobs_total,
                COALESCE(j_stats.succeeded, 0) AS jobs_succeeded,
                COALESCE(j_stats.failed, 0)    AS jobs_failed
            FROM public.workflows w
            INNER JOIN public.workflow_definition_versions wdv ON wdv.id = w.workflow_definition_version_id
            INNER JOIN public.workflow_definitions wd ON wd.id = wdv.workflow_definition_id
            LEFT JOIN LATERAL (
                SELECT
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE status = 'succeeded') AS succeeded,
                    COUNT(*) FILTER (WHERE status = 'failed')    AS failed
                FROM public.automation_jobs
                WHERE workflow_id = w.id
            ) j_stats ON TRUE
            WHERE w.target_person_id = @personId
            ORDER BY w.created_at DESC, w.id DESC
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<PersonWorkflowTraceDto>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new PersonWorkflowTraceDto
            {
                WorkflowId = reader.GetInt64(0),
                WorkflowUid = reader.GetGuid(1).ToString(),
                DefinitionKey = reader.GetString(2),
                Status = reader.GetString(3),
                StartedAt = reader.GetDateTime(4),
                CompletedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                AutomationJobsTotal = (int)reader.GetInt64(6),
                AutomationJobsSucceeded = (int)reader.GetInt64(7),
                AutomationJobsFailed = (int)reader.GetInt64(8),
            });
        }
        return result;
    }

    private static async Task<IReadOnlyList<PersonInitialPasswordStatusDto>> LoadInitialPasswordStatusAsync(
        NpgsqlConnection connection, long personId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                tc.id, tc.credential_type, tc.created_at, tc.expires_at, tc.first_read_at, tc.read_count,
                w.uid
            FROM public.temporary_credentials tc
            INNER JOIN public.workflow_node_instances ni ON ni.id = tc.workflow_node_instance_id
            INNER JOIN public.workflows w ON w.id = ni.workflow_id
            WHERE w.target_person_id = @personId
              AND tc.credential_type = 'ad_initial_password'
            ORDER BY tc.created_at DESC
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var now = DateTime.UtcNow;
        var result = new List<PersonInitialPasswordStatusDto>();
        while (await reader.ReadAsync(ct))
        {
            var expiresAt = reader.GetDateTime(3);
            result.Add(new PersonInitialPasswordStatusDto
            {
                VaultId = reader.GetGuid(0),
                CredentialType = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                ExpiresAt = expiresAt,
                FirstReadAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                ReadCount = reader.GetInt32(5),
                IsExpired = expiresAt <= now,
                WorkflowUid = reader.GetGuid(6).ToString(),
            });
        }
        return result;
    }

    private sealed record PersonStammRecord(long PersonId, int? DepartmentId, long? DirectoryIdentityId);
}
