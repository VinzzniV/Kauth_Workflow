CREATE OR REPLACE FUNCTION upsert_linearized_workflow_definition(
    p_definition_key TEXT,
    p_definition_name TEXT,
    p_definition_description TEXT,
    p_version_name TEXT,
    p_version_description TEXT,
    p_primary_legacy_process_type_key TEXT,
    p_nodes JSONB,
    p_edges JSONB
) RETURNS VOID AS $$
DECLARE
    definition_id INTEGER;
    definition_version_id BIGINT;
    primary_process_type_id INTEGER;
    node_item RECORD;
    edge_item RECORD;
    created_node_id BIGINT;
    node_ids JSONB := '{}'::jsonb;
BEGIN
    SELECT id
    INTO primary_process_type_id
    FROM process_types
    WHERE key = LOWER(TRIM(p_primary_legacy_process_type_key))
      AND is_active = TRUE
    LIMIT 1;

    IF primary_process_type_id IS NULL THEN
        RAISE EXCEPTION 'Active process_type "%" is required for workflow definition mapping.', p_primary_legacy_process_type_key;
    END IF;

    INSERT INTO workflow_definitions (
        definition_key,
        name,
        description,
        updated_at
    )
    VALUES (
        LOWER(TRIM(p_definition_key)),
        p_definition_name,
        p_definition_description,
        NOW()
    )
    ON CONFLICT (definition_key) DO UPDATE
    SET
        name = EXCLUDED.name,
        description = EXCLUDED.description,
        updated_at = NOW()
    RETURNING id
    INTO definition_id;

    SELECT id
    INTO definition_version_id
    FROM workflow_definition_versions
    WHERE workflow_definition_id = definition_id
      AND status = 'published'
    ORDER BY version_number DESC, id DESC
    LIMIT 1;

    IF definition_version_id IS NULL THEN
        SELECT id
        INTO definition_version_id
        FROM workflow_definition_versions
        WHERE workflow_definition_id = definition_id
        ORDER BY version_number DESC, id DESC
        LIMIT 1;
    END IF;

    IF definition_version_id IS NULL THEN
        INSERT INTO workflow_definition_versions (
            workflow_definition_id,
            version_number,
            status,
            name,
            description,
            primary_legacy_process_type_id,
            updated_at,
            published_at
        )
        VALUES (
            definition_id,
            1,
            'published',
            p_version_name,
            p_version_description,
            primary_process_type_id,
            NOW(),
            NOW()
        )
        RETURNING id
        INTO definition_version_id;
    END IF;

    UPDATE workflow_definition_versions
    SET
        status = CASE WHEN id = definition_version_id THEN 'published' ELSE 'retired' END,
        name = CASE WHEN id = definition_version_id THEN p_version_name ELSE name END,
        description = CASE WHEN id = definition_version_id THEN p_version_description ELSE description END,
        primary_legacy_process_type_id = CASE WHEN id = definition_version_id THEN primary_process_type_id ELSE primary_legacy_process_type_id END,
        updated_at = NOW(),
        published_at = CASE WHEN id = definition_version_id THEN COALESCE(published_at, NOW()) ELSE published_at END
    WHERE workflow_definition_id = definition_id;

    DELETE FROM workflow_edges
    WHERE workflow_definition_version_id = definition_version_id;

    DELETE FROM workflow_node_configs
    WHERE workflow_node_id IN (
        SELECT id
        FROM workflow_nodes
        WHERE workflow_definition_version_id = definition_version_id
    );

    DELETE FROM workflow_nodes
    WHERE workflow_definition_version_id = definition_version_id;

    FOR node_item IN
        SELECT *
        FROM jsonb_to_recordset(p_nodes) AS x(
            node_key TEXT,
            node_type TEXT,
            title TEXT,
            sort_order INTEGER,
            config_json JSONB
        )
    LOOP
        INSERT INTO workflow_nodes (
            workflow_definition_version_id,
            node_key,
            node_type,
            title,
            sort_order
        )
        VALUES (
            definition_version_id,
            LOWER(TRIM(node_item.node_key)),
            LOWER(TRIM(node_item.node_type)),
            node_item.title,
            node_item.sort_order
        )
        RETURNING id
        INTO created_node_id;

        node_ids := jsonb_set(
            node_ids,
            ARRAY[LOWER(TRIM(node_item.node_key))],
            to_jsonb(created_node_id),
            TRUE);

        IF node_item.config_json IS NOT NULL AND node_item.config_json <> 'null'::jsonb THEN
            INSERT INTO workflow_node_configs (
                workflow_node_id,
                config_json
            )
            VALUES (
                created_node_id,
                node_item.config_json
            );
        END IF;
    END LOOP;

    FOR edge_item IN
        SELECT *
        FROM jsonb_to_recordset(p_edges) AS x(
            source_node_key TEXT,
            target_node_key TEXT,
            priority INTEGER,
            condition_expression TEXT
        )
    LOOP
        INSERT INTO workflow_edges (
            workflow_definition_version_id,
            source_workflow_node_id,
            target_workflow_node_id,
            priority,
            condition_expression
        )
        VALUES (
            definition_version_id,
            (node_ids ->> LOWER(TRIM(edge_item.source_node_key)))::BIGINT,
            (node_ids ->> LOWER(TRIM(edge_item.target_node_key)))::BIGINT,
            edge_item.priority,
            edge_item.condition_expression
        );
    END LOOP;
END;
$$ LANGUAGE plpgsql;

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
      {"node_key":"supervisor_approval","node_type":"approval","title":"Supervisor / Freigabe","sort_order":20,"config_json":{"legacyTemplateKey":"supervisor_fills_document"}},
      {"node_key":"department_setup","node_type":"measure_provision","title":"Bereitstellungsmaßnahmen erzeugen","sort_order":30,"config_json":null},
      {"node_key":"end","node_type":"end","title":"Abschluss","sort_order":40,"config_json":null}
    ]
    $json$::jsonb,
    $json$
    [
      {"source_node_key":"start","target_node_key":"collect_requirements","priority":0,"condition_expression":null},
      {"source_node_key":"collect_requirements","target_node_key":"supervisor_approval","priority":0,"condition_expression":null},
      {"source_node_key":"supervisor_approval","target_node_key":"department_setup","priority":0,"condition_expression":null},
      {"source_node_key":"department_setup","target_node_key":"end","priority":0,"condition_expression":null}
    ]
    $json$::jsonb
);

SELECT upsert_linearized_workflow_definition(
    'offboarding',
    'Offboarding',
    'Business-phase workflow definition mapped to the legacy offboarding task generator.',
    'Offboarding Standard',
    'Published offboarding mapping with a deprovision measure block and internal task generation.',
    'offboarding',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Offboarding-Umfang erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"offboarding"}},
      {"node_key":"department_setup","node_type":"measure_deprovision","title":"Entzugsmaßnahmen erzeugen","sort_order":20,"config_json":null},
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

SELECT upsert_linearized_workflow_definition(
    'department_change',
    'Abteilungswechsel',
    'Business-phase workflow definition mapped to the legacy department change task generator.',
    'Abteilungswechsel Standard',
    'Published department change mapping with a change measure block and internal task generation.',
    'department_change',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Wechselumfang erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"department_change"}},
      {"node_key":"department_setup","node_type":"measure_change","title":"Änderungsmaßnahmen erzeugen","sort_order":20,"config_json":null},
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

-- Keep the helper for follow-up lifecycle mapping migrations.
