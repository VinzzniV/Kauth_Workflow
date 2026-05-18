using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace API;

// Admin-Gated-Automation Slice 1: berechnet eine WhatIf-Vorschau fuer alle Actions eines
// Workflow-Automation-Nodes. Fuehrt keinen Schreibvorgang aus — reines Read + Handler-PlanAsync.
//
// Linux-side Actions: PlanAsync direkt auf dem registrierten IWorkflowAutomationActionHandler.
// Windows-Worker-Actions: schreibt einen automation_plan_requests-Eintrag, pollt bis completed/failed
// (max 30 s Timeout).
internal sealed class WorkflowAutomationPlanService
{
    private readonly LifecycleRuntimeSettings runtimeSettings;
    private readonly IWorkflowAutomationHandlerRegistry handlerRegistry;
    private readonly IReferenceUserDirectoryReader referenceUserReader;

    private static readonly TimeSpan WorkerPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WorkerPollTimeout = TimeSpan.FromSeconds(30);

    public WorkflowAutomationPlanService(
        LifecycleRuntimeSettings runtimeSettings,
        IWorkflowAutomationHandlerRegistry handlerRegistry,
        IReferenceUserDirectoryReader referenceUserReader)
    {
        this.runtimeSettings = runtimeSettings;
        this.handlerRegistry = handlerRegistry;
        this.referenceUserReader = referenceUserReader;
    }

    public async Task<NodePlanResult?> ComputeNodePlanAsync(
        string workflowInstanceUid, string nodeKey, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Workflow-ID und Versions-ID aus UID laden
        var workflowInfo = await LoadWorkflowInfoAsync(connection, tx, workflowInstanceUid, ct);
        if (workflowInfo is null) return null;
        var (workflowId, _) = workflowInfo.Value;

        // Node für diesen Workflow laden
        var nodeInfo = await LoadWorkflowNodeAsync(connection, tx, workflowInfo.Value.VersionId, nodeKey, ct);
        if (nodeInfo is null) return null;
        var nodeId = nodeInfo.Value;

        // Actions des Nodes laden (mit target_runtime)
        var actions = await LoadNodeActionsWithRuntimeAsync(connection, tx, nodeId, ct);
        if (actions.Count == 0) return null;

        // Echte succeeded Outputs laden
        var realAdUser = await PostgresWorkflowAutomationOperations
            .LoadCreatedAdUserOutputsForWorkflowInScope(connection, tx, workflowId, ct);
        var realMailbox = await PostgresWorkflowAutomationOperations
            .LoadCreatedMailboxOutputsForWorkflowInScope(connection, tx, workflowId, ct);

        // Action-Keys aller Automation-Nodes dieses Workflows laden (für Sentinel-Seeding)
        var nodeActionMap = await LoadAutomationNodeActionKeysAsync(connection, tx, workflowId, ct);

        // Formular-Antworten laden
        var answersByKey = await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, tx, workflowId);

        // Sentinel-Dicts vorbelegen (nur typengerecht)
        var adUserDict = new Dictionary<string, JsonElement>(realAdUser, StringComparer.Ordinal);
        var mailboxDict = new Dictionary<string, JsonElement>(realMailbox, StringComparer.Ordinal);
        foreach (var (nk, actionKeys) in nodeActionMap)
        {
            if (actionKeys.Contains("CreateAdUserLdaps") && !adUserDict.ContainsKey(nk))
                adUserDict[nk] = PlanSentinels.BuildAdUserSentinelOutput(PlanSentinels.CredentialStatusUnknown);
            if (actionKeys.Contains("CreateMailboxGraph") && !mailboxDict.ContainsKey(nk))
                mailboxDict[nk] = PlanSentinels.BuildMailboxSentinelOutput();
        }

        // Pro Action: Payload bauen + Plan berechnen
        var steps = new List<ActionPlanStep>();
        foreach (var action in actions)
        {
            // Slice 5: Referenzuser-Gruppen pro Action vorab via Graph-Reader laden.
            // answersByKey wird hier aus dem laufenden Workflow-Snapshot uebergeben.
            var refUserCache = await PostgresWorkflowAutomationOperations.LoadReferenceUserGroupsForMappingAsync(
                connection, tx, referenceUserReader, action.InputMapping, answersByKey, ct);

            JsonElement payload;
            try
            {
                payload = await PostgresWorkflowAutomationOperations.BuildAutomationJobPayloadAsync(
                    connection,
                    tx,
                    workflowId,
                    action.InputMapping,
                    answersByKey,
                    ct,
                    adUserDict,
                    mailboxDict,
                    refUserCache);
            }
            catch (InvalidOperationException ex)
            {
                steps.Add(new ActionPlanStep(action.ActionKey, false, null, $"Config-Fehler im Input-Mapping: {ex.Message}"));
                continue;
            }

            AutomationLinuxPlanResult planResult;
            if (string.IsNullOrEmpty(action.TargetRuntime))
            {
                // Linux-side Handler: direkt aufrufen
                planResult = await PlanLinuxActionAsync(action.ActionKey, workflowInstanceUid, payload, ct);
            }
            else
            {
                // Windows-Worker: Plan-Request einstellen und auf Ergebnis warten
                planResult = await PlanWindowsActionAsync(action.ActionKey, workflowInstanceUid, nodeKey, payload, action.TargetRuntime, ct);
            }

            steps.Add(new ActionPlanStep(action.ActionKey, planResult.IsSuccess, planResult.Plan, planResult.ErrorMessage));

            // Within-Node Sentinel-Update nach CreateAdUserLdaps
            if (action.ActionKey == "CreateAdUserLdaps" && planResult.IsSuccess && planResult.Plan is not null)
            {
                var planJson = planResult.Plan.ToJsonString();
                using var doc = JsonDocument.Parse(planJson);
                var root = doc.RootElement;
                var alreadyExists = root.TryGetProperty("alreadyExists", out var ae) && ae.ValueKind == JsonValueKind.True;
                var credVaultId = alreadyExists
                    ? null
                    : PlanSentinels.CredentialWillBeGenerated;
                adUserDict[nodeKey] = PlanSentinels.BuildAdUserSentinelOutput(credVaultId);
            }
        }

        await tx.CommitAsync(ct);
        return new NodePlanResult(nodeKey, steps);
    }

    private async Task<AutomationLinuxPlanResult> PlanLinuxActionAsync(
        string actionKey, string workflowInstanceUid, JsonElement payload, CancellationToken ct)
    {
        IWorkflowAutomationActionHandler handler;
        try
        {
            handler = handlerRegistry.Resolve(actionKey);
        }
        catch
        {
            return AutomationLinuxPlanResult.Failure($"Kein Linux-Handler fuer action_key '{actionKey}' registriert.");
        }

        var ctx = new AutomationPlanContext(
            workflowInstanceUid,
            actionKey,
            JsonDocument.Parse(payload.GetRawText()));

        try
        {
            return await handler.PlanAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            return AutomationLinuxPlanResult.Failure($"PlanAsync Exception: {ex.Message}");
        }
    }

    private async Task<AutomationLinuxPlanResult> PlanWindowsActionAsync(
        string actionKey, string workflowInstanceUid, string nodeKey,
        JsonElement payload, string targetRuntime, CancellationToken ct)
    {
        // Plan-Request in automation_plan_requests einstellen
        long requestId;
        try
        {
            requestId = await InsertPlanRequestAsync(workflowInstanceUid, nodeKey, actionKey, payload, targetRuntime, ct);
        }
        catch (Exception ex)
        {
            return AutomationLinuxPlanResult.Failure($"Plan-Request konnte nicht erstellt werden: {ex.Message}");
        }

        // Auf Worker-Ergebnis warten (Polling mit Timeout)
        var deadline = DateTimeOffset.UtcNow + WorkerPollTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (ct.IsCancellationRequested)
                return AutomationLinuxPlanResult.Failure("Abgebrochen.");

            await Task.Delay(WorkerPollInterval, ct);

            var (status, planJson, errorMessage) = await PollPlanRequestAsync(requestId, ct);
            if (status == "completed" && planJson is not null)
            {
                try
                {
                    var node = JsonNode.Parse(planJson);
                    return node is not null
                        ? AutomationLinuxPlanResult.Success(node)
                        : AutomationLinuxPlanResult.Failure("Worker returned empty plan.");
                }
                catch
                {
                    return AutomationLinuxPlanResult.Failure("Plan-JSON des Workers konnte nicht geparst werden.");
                }
            }
            if (status == "failed")
                return AutomationLinuxPlanResult.Failure(errorMessage ?? "Worker Plan-Request fehlgeschlagen.");
        }

        return AutomationLinuxPlanResult.Failure($"Timeout: Worker hat Plan-Request für '{actionKey}' nicht innerhalb von {WorkerPollTimeout.TotalSeconds}s beantwortet.");
    }

    private async Task<long> InsertPlanRequestAsync(
        string workflowInstanceUid, string nodeKey, string actionKey,
        JsonElement payload, string targetRuntime, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            INSERT INTO public.automation_plan_requests
                (workflow_instance_uid, node_key, action_key, payload_json, target_runtime, status)
            VALUES
                (@workflowInstanceUid::uuid, @nodeKey, @actionKey, @payloadJson::jsonb, @targetRuntime, 'pending')
            RETURNING id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("workflowInstanceUid", workflowInstanceUid);
        cmd.Parameters.AddWithValue("nodeKey", nodeKey);
        cmd.Parameters.AddWithValue("actionKey", actionKey);
        cmd.Parameters.AddWithValue("payloadJson", payload.GetRawText());
        cmd.Parameters.AddWithValue("targetRuntime", targetRuntime);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private async Task<(string Status, string? PlanJson, string? ErrorMessage)> PollPlanRequestAsync(
        long requestId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT status, plan_json::text, error_message
            FROM public.automation_plan_requests
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", requestId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ("unknown", null, "Plan-Request nicht gefunden.");
        return (
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    // Lädt workflowId (long) + workflowDefinitionVersionId für eine Workflow-Instanz-UID.
    private static async Task<(long WorkflowId, long VersionId)?> LoadWorkflowInfoAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, string workflowInstanceUid, CancellationToken ct)
    {
        const string sql = """
            SELECT w.id, w.workflow_definition_version_id
            FROM public.workflows w
            WHERE w.uid = @uid::uuid
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("uid", workflowInstanceUid);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<long?> LoadWorkflowNodeAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, long versionId, string nodeKey, CancellationToken ct)
    {
        const string sql = """
            SELECT id
            FROM public.workflow_nodes
            WHERE workflow_definition_version_id = @versionId
              AND node_key = @nodeKey
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("versionId", versionId);
        cmd.Parameters.AddWithValue("nodeKey", nodeKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.GetInt64(0);
    }

    // Lädt Actions für einen Node mit target_runtime aus action_definitions.
    private static async Task<List<PlanNodeAction>> LoadNodeActionsWithRuntimeAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, long nodeId, CancellationToken ct)
    {
        const string sql = """
            SELECT ad.action_key, ad.target_runtime, wna.input_mapping_json::text
            FROM public.workflow_node_actions wna
            INNER JOIN public.action_definitions ad ON ad.id = wna.action_definition_id
            WHERE wna.workflow_node_id = @nodeId
            ORDER BY wna.execution_order, wna.id
            """;

        var result = new List<PlanNodeAction>();
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("nodeId", nodeId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var actionKey = reader.GetString(0);
            var targetRuntime = reader.IsDBNull(1) ? null : reader.GetString(1);
            var inputMappingRaw = reader.IsDBNull(2) ? null : reader.GetString(2);
            JsonElement? inputMapping = inputMappingRaw is not null
                ? JsonSerializer.Deserialize<JsonElement>(inputMappingRaw)
                : null;
            result.Add(new PlanNodeAction(actionKey, targetRuntime, inputMapping));
        }
        return result;
    }

    // Lädt pro Automation-Node alle Action-Keys (für Sentinel-Seeding).
    private static async Task<Dictionary<string, HashSet<string>>> LoadAutomationNodeActionKeysAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, long workflowId, CancellationToken ct)
    {
        const string sql = """
            SELECT DISTINCT wn.node_key, ad.action_key
            FROM public.automation_jobs j
            INNER JOIN public.workflow_node_instances wni ON wni.id = j.workflow_node_instance_id
            INNER JOIN public.workflow_nodes wn ON wn.id = wni.workflow_node_id
            INNER JOIN public.action_definitions ad ON ad.id = j.action_definition_id
            WHERE j.workflow_id = @workflowId
            UNION
            SELECT DISTINCT wn.node_key, ad.action_key
            FROM public.workflow_nodes wn
            INNER JOIN public.workflow_node_actions wna ON wna.workflow_node_id = wn.id
            INNER JOIN public.action_definitions ad ON ad.id = wna.action_definition_id
            INNER JOIN public.workflows w ON w.workflow_definition_version_id = wn.workflow_definition_version_id
            WHERE w.id = @workflowId
            """;

        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var nk = reader.GetString(0);
            var ak = reader.GetString(1);
            if (!result.TryGetValue(nk, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                result[nk] = set;
            }
            set.Add(ak);
        }
        return result;
    }

    private sealed record PlanNodeAction(string ActionKey, string? TargetRuntime, JsonElement? InputMapping);
}
