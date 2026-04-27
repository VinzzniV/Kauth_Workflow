-- The supervisor requirement form is the gatekeeper. Do not expose or execute
-- a second approval task before generating onboarding measures.

SELECT upsert_linearized_workflow_definition(
    'onboarding',
    'Onboarding',
    'Business-phase workflow definition mapped to the legacy onboarding task generator.',
    'Onboarding Standard',
    'Published onboarding mapping with a provision measure block and internal task generation.',
    'onboarding',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Anforderungen erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"onboarding"}},
      {"node_key":"department_setup","node_type":"measure_provision","title":"Bereitstellungsmaßnahmen erzeugen","sort_order":20,"config_json":null},
      {"node_key":"end","node_type":"end","title":"Abschluss","sort_order":30,"config_json":null}
    ]
    $json$::jsonb,
    $json$
    [
      {"source_node_key":"start","target_node_key":"collect_requirements","priority":0,"condition_expression":null},
      {"source_node_key":"collect_requirements","target_node_key":"department_setup","priority":0,"condition_expression":null},
      {"source_node_key":"department_setup","target_node_key":"end","priority":0,"condition_expression":null}
    ]
    $json$::jsonb
);

-- Repair development databases where repeated mapping upserts removed runtime
-- node instances for not-yet-completed onboarding gatekeeper workflows.
WITH affected_workflows AS (
    SELECT
        w.id AS workflow_id,
        start_node.id AS start_node_id,
        form_node.id AS form_node_id
    FROM workflows w
    JOIN process_types pt ON pt.id = w.process_type_id
    JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
    JOIN workflow_definitions d ON d.id = v.workflow_definition_id
    JOIN workflow_nodes start_node
        ON start_node.workflow_definition_version_id = v.id
       AND start_node.node_key = 'start'
    JOIN workflow_nodes form_node
        ON form_node.workflow_definition_version_id = v.id
       AND form_node.node_key = 'collect_requirements'
    WHERE pt.key = 'onboarding'
      AND d.definition_key = 'onboarding'
      AND w.status = 'waiting_for_supervisor'
      AND COALESCE(w.current_runtime_status, 'waiting_on_node') = 'waiting_on_node'
      AND NOT EXISTS (
          SELECT 1
          FROM workflow_tasks wt
          WHERE wt.workflow_id = w.id
      )
      AND NOT EXISTS (
          SELECT 1
          FROM workflow_node_instances ni
          WHERE ni.workflow_id = w.id
      )
),
inserted_start AS (
    INSERT INTO workflow_node_instances (
        workflow_id,
        workflow_node_id,
        status,
        started_at,
        completed_at,
        result_json
    )
    SELECT
        workflow_id,
        start_node_id,
        'done',
        NOW(),
        NOW(),
        '{"auto":true,"reason":"mapping_repair"}'::jsonb
    FROM affected_workflows
    ON CONFLICT (workflow_id, workflow_node_id) DO NOTHING
    RETURNING workflow_id
)
INSERT INTO workflow_node_instances (
    workflow_id,
    workflow_node_id,
    status,
    started_at,
    completed_at,
    result_json
)
SELECT
    workflow_id,
    form_node_id,
    'active',
    NOW(),
    NULL,
    NULL
FROM affected_workflows
ON CONFLICT (workflow_id, workflow_node_id) DO NOTHING;
