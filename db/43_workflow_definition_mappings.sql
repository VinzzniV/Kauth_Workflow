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
    'Linearized T6 workflow definition mapped from legacy onboarding task templates.',
    'Linearized Published Mapping',
    'T6 published mapping of the legacy onboarding flow for admin-only runtime validation.',
    'onboarding',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Anforderungen erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"onboarding"}},
      {"node_key":"decide_ad_user","node_type":"decision","title":"AD-User angefordert?","sort_order":20,"config_json":null},
      {"node_key":"task_ad_user_create","node_type":"task","title":"AD-User anlegen","sort_order":30,"config_json":{"legacyTemplateKey":"ad_user_create"}},
      {"node_key":"decide_comparison_user","node_type":"decision","title":"Vergleichsuser vorhanden?","sort_order":40,"config_json":null},
      {"node_key":"task_permissions_from_reference_user","node_type":"task","title":"AD-Berechtigungen uebernehmen","sort_order":50,"config_json":{"legacyTemplateKey":"permissions_from_reference_user"}},
      {"node_key":"decide_mailbox","node_type":"decision","title":"Mailbox angefordert?","sort_order":60,"config_json":null},
      {"node_key":"task_exchange_create","node_type":"task","title":"Mailbox anlegen","sort_order":70,"config_json":{"legacyTemplateKey":"exchange_create"}},
      {"node_key":"decide_habel","node_type":"decision","title":"Habel angefordert?","sort_order":80,"config_json":null},
      {"node_key":"task_habel_user_create","node_type":"task","title":"Habel-User anlegen","sort_order":90,"config_json":{"legacyTemplateKey":"habel_user_create"}},
      {"node_key":"decide_ln","node_type":"decision","title":"InforLN angefordert?","sort_order":100,"config_json":null},
      {"node_key":"task_ln_user_create","node_type":"task","title":"InforLN-User anlegen","sort_order":110,"config_json":{"legacyTemplateKey":"ln_user_create"}},
      {"node_key":"decide_internet","node_type":"decision","title":"Internetzugang angefordert?","sort_order":120,"config_json":null},
      {"node_key":"task_internet_access_enable","node_type":"task","title":"Internetzugang einrichten","sort_order":130,"config_json":{"legacyTemplateKey":"internet_access_enable"}},
      {"node_key":"decide_internal_drive_access","node_type":"decision","title":"Laufwerksrechte angefordert?","sort_order":140,"config_json":null},
      {"node_key":"task_internal_drive_access_grant","node_type":"task","title":"Laufwerksrechte vergeben","sort_order":150,"config_json":{"legacyTemplateKey":"internal_drive_access_grant"}},
      {"node_key":"decide_office","node_type":"decision","title":"Office angefordert?","sort_order":160,"config_json":null},
      {"node_key":"task_office_install","node_type":"task","title":"Office bereitstellen","sort_order":170,"config_json":{"legacyTemplateKey":"office_install"}},
      {"node_key":"decide_hardware_requested","node_type":"decision","title":"Hardware angefordert?","sort_order":180,"config_json":null},
      {"node_key":"decide_hardware_available","node_type":"decision","title":"Hardware bereits verfuegbar?","sort_order":190,"config_json":null},
      {"node_key":"task_hardware_procure","node_type":"task","title":"Hardware beschaffen","sort_order":200,"config_json":{"legacyTemplateKey":"hardware_procure"}},
      {"node_key":"task_hardware_setup","node_type":"task","title":"Hardware einrichten","sort_order":210,"config_json":{"legacyTemplateKey":"hardware_setup"}},
      {"node_key":"task_hardware_handover","node_type":"task","title":"Hardware bereitstellen","sort_order":220,"config_json":{"legacyTemplateKey":"hardware_handover"}},
      {"node_key":"decide_phone","node_type":"decision","title":"Telefon angefordert?","sort_order":230,"config_json":null},
      {"node_key":"task_phone_prepare","node_type":"task","title":"Telefon bereitstellen","sort_order":240,"config_json":{"legacyTemplateKey":"phone_prepare"}},
      {"node_key":"decide_catia","node_type":"decision","title":"Catia angefordert?","sort_order":250,"config_json":null},
      {"node_key":"task_catia_install","node_type":"task","title":"Catia bereitstellen","sort_order":260,"config_json":{"legacyTemplateKey":"catia_install"}},
      {"node_key":"decide_datev","node_type":"decision","title":"DATEV angefordert?","sort_order":270,"config_json":null},
      {"node_key":"task_datev_install","node_type":"task","title":"DATEV bereitstellen","sort_order":280,"config_json":{"legacyTemplateKey":"datev_install"}},
      {"node_key":"decide_tiso","node_type":"decision","title":"Tisoware angefordert?","sort_order":290,"config_json":null},
      {"node_key":"task_tisoware_install","node_type":"task","title":"Tisoware bereitstellen","sort_order":300,"config_json":{"legacyTemplateKey":"tisoware_install"}},
      {"node_key":"decide_babtec","node_type":"decision","title":"Babtec angefordert?","sort_order":310,"config_json":null},
      {"node_key":"task_babtec_user_create","node_type":"task","title":"Babtec-User anlegen","sort_order":320,"config_json":{"legacyTemplateKey":"babtec_user_create"}},
      {"node_key":"decide_gewatec","node_type":"decision","title":"Gewatec angefordert?","sort_order":330,"config_json":null},
      {"node_key":"task_gewatec_user_create","node_type":"task","title":"Gewatec-User anlegen","sort_order":340,"config_json":{"legacyTemplateKey":"gewatec_user_create"}},
      {"node_key":"decide_provis","node_type":"decision","title":"Provis angefordert?","sort_order":350,"config_json":null},
      {"node_key":"task_provis_user_create","node_type":"task","title":"Provis-User anlegen","sort_order":360,"config_json":{"legacyTemplateKey":"provis_user_create"}},
      {"node_key":"decide_consense","node_type":"decision","title":"Consense angefordert?","sort_order":370,"config_json":null},
      {"node_key":"task_consense_setup","node_type":"task","title":"Consense-User anlegen","sort_order":380,"config_json":{"legacyTemplateKey":"consense_setup"}},
      {"node_key":"end","node_type":"end","title":"Abgeschlossen","sort_order":390,"config_json":null}
    ]
    $json$::jsonb,
    $json$
    [
      {"source_node_key":"start","target_node_key":"collect_requirements","priority":0,"condition_expression":null},
      {"source_node_key":"collect_requirements","target_node_key":"decide_ad_user","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ad_user","target_node_key":"task_ad_user_create","priority":0,"condition_expression":"{\"answerKey\":\"ad_user_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ad_user","target_node_key":"decide_mailbox","priority":1,"condition_expression":null},
      {"source_node_key":"task_ad_user_create","target_node_key":"decide_comparison_user","priority":0,"condition_expression":null},
      {"source_node_key":"decide_comparison_user","target_node_key":"task_permissions_from_reference_user","priority":0,"condition_expression":"{\"answerKey\":\"comparison_user_available\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_comparison_user","target_node_key":"decide_mailbox","priority":1,"condition_expression":null},
      {"source_node_key":"task_permissions_from_reference_user","target_node_key":"decide_mailbox","priority":0,"condition_expression":null},
      {"source_node_key":"decide_mailbox","target_node_key":"task_exchange_create","priority":0,"condition_expression":"{\"answerKey\":\"mailbox_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_mailbox","target_node_key":"decide_habel","priority":1,"condition_expression":null},
      {"source_node_key":"task_exchange_create","target_node_key":"decide_habel","priority":0,"condition_expression":null},
      {"source_node_key":"decide_habel","target_node_key":"task_habel_user_create","priority":0,"condition_expression":"{\"answerKey\":\"habel_user_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_habel","target_node_key":"decide_ln","priority":1,"condition_expression":null},
      {"source_node_key":"task_habel_user_create","target_node_key":"decide_ln","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ln","target_node_key":"task_ln_user_create","priority":0,"condition_expression":"{\"answerKey\":\"ln_user_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ln","target_node_key":"decide_internet","priority":1,"condition_expression":null},
      {"source_node_key":"task_ln_user_create","target_node_key":"decide_internet","priority":0,"condition_expression":null},
      {"source_node_key":"decide_internet","target_node_key":"task_internet_access_enable","priority":0,"condition_expression":"{\"answerKey\":\"internet_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_internet","target_node_key":"decide_internal_drive_access","priority":1,"condition_expression":null},
      {"source_node_key":"task_internet_access_enable","target_node_key":"decide_internal_drive_access","priority":0,"condition_expression":null},
      {"source_node_key":"decide_internal_drive_access","target_node_key":"task_internal_drive_access_grant","priority":0,"condition_expression":"{\"answerKey\":\"internal_drive_access_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_internal_drive_access","target_node_key":"decide_office","priority":1,"condition_expression":null},
      {"source_node_key":"task_internal_drive_access_grant","target_node_key":"decide_office","priority":0,"condition_expression":null},
      {"source_node_key":"decide_office","target_node_key":"task_office_install","priority":0,"condition_expression":"{\"answerKey\":\"microsoft_office_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_office","target_node_key":"decide_hardware_requested","priority":1,"condition_expression":null},
      {"source_node_key":"task_office_install","target_node_key":"decide_hardware_requested","priority":0,"condition_expression":null},
      {"source_node_key":"decide_hardware_requested","target_node_key":"decide_hardware_available","priority":0,"condition_expression":"{\"answerKey\":\"hardware_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_hardware_requested","target_node_key":"decide_phone","priority":1,"condition_expression":null},
      {"source_node_key":"decide_hardware_available","target_node_key":"task_hardware_setup","priority":0,"condition_expression":"{\"answerKey\":\"hardware_available\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_hardware_available","target_node_key":"task_hardware_procure","priority":1,"condition_expression":null},
      {"source_node_key":"task_hardware_procure","target_node_key":"task_hardware_setup","priority":0,"condition_expression":null},
      {"source_node_key":"task_hardware_setup","target_node_key":"task_hardware_handover","priority":0,"condition_expression":null},
      {"source_node_key":"task_hardware_handover","target_node_key":"decide_phone","priority":0,"condition_expression":null},
      {"source_node_key":"decide_phone","target_node_key":"task_phone_prepare","priority":0,"condition_expression":"{\"answerKey\":\"phone_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_phone","target_node_key":"decide_catia","priority":1,"condition_expression":null},
      {"source_node_key":"task_phone_prepare","target_node_key":"decide_catia","priority":0,"condition_expression":null},
      {"source_node_key":"decide_catia","target_node_key":"task_catia_install","priority":0,"condition_expression":"{\"answerKey\":\"catia_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_catia","target_node_key":"decide_datev","priority":1,"condition_expression":null},
      {"source_node_key":"task_catia_install","target_node_key":"decide_datev","priority":0,"condition_expression":null},
      {"source_node_key":"decide_datev","target_node_key":"task_datev_install","priority":0,"condition_expression":"{\"answerKey\":\"datev_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_datev","target_node_key":"decide_tiso","priority":1,"condition_expression":null},
      {"source_node_key":"task_datev_install","target_node_key":"decide_tiso","priority":0,"condition_expression":null},
      {"source_node_key":"decide_tiso","target_node_key":"task_tisoware_install","priority":0,"condition_expression":"{\"answerKey\":\"tiso_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_tiso","target_node_key":"decide_babtec","priority":1,"condition_expression":null},
      {"source_node_key":"task_tisoware_install","target_node_key":"decide_babtec","priority":0,"condition_expression":null},
      {"source_node_key":"decide_babtec","target_node_key":"task_babtec_user_create","priority":0,"condition_expression":"{\"answerKey\":\"babtec_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_babtec","target_node_key":"decide_gewatec","priority":1,"condition_expression":null},
      {"source_node_key":"task_babtec_user_create","target_node_key":"decide_gewatec","priority":0,"condition_expression":null},
      {"source_node_key":"decide_gewatec","target_node_key":"task_gewatec_user_create","priority":0,"condition_expression":"{\"answerKey\":\"gewatec_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_gewatec","target_node_key":"decide_provis","priority":1,"condition_expression":null},
      {"source_node_key":"task_gewatec_user_create","target_node_key":"decide_provis","priority":0,"condition_expression":null},
      {"source_node_key":"decide_provis","target_node_key":"task_provis_user_create","priority":0,"condition_expression":"{\"answerKey\":\"provis_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_provis","target_node_key":"decide_consense","priority":1,"condition_expression":null},
      {"source_node_key":"task_provis_user_create","target_node_key":"decide_consense","priority":0,"condition_expression":null},
      {"source_node_key":"decide_consense","target_node_key":"task_consense_setup","priority":0,"condition_expression":"{\"answerKey\":\"consense_requested\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_consense","target_node_key":"end","priority":1,"condition_expression":null},
      {"source_node_key":"task_consense_setup","target_node_key":"end","priority":0,"condition_expression":null}
    ]
    $json$::jsonb
);

SELECT upsert_linearized_workflow_definition(
    'offboarding',
    'Offboarding',
    'Linearized T6 workflow definition mapped from legacy offboarding task templates.',
    'Linearized Published Mapping',
    'T6 published mapping of the legacy offboarding flow for admin-only runtime validation.',
    'offboarding',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Offboarding-Umfang erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"offboarding"}},
      {"node_key":"task_last_day_confirmed","node_type":"task","title":"Letzten Arbeitstag bestaetigen","sort_order":20,"config_json":{"legacyTemplateKey":"ob_last_day_confirmed"}},
      {"node_key":"decide_exit_interview","node_type":"decision","title":"Austrittsgespraech noetig?","sort_order":30,"config_json":null},
      {"node_key":"task_exit_interview","node_type":"task","title":"Austrittsgespraech fuehren","sort_order":40,"config_json":{"legacyTemplateKey":"ob_exit_interview"}},
      {"node_key":"decide_knowledge_transfer","node_type":"decision","title":"Wissenstransfer noetig?","sort_order":50,"config_json":null},
      {"node_key":"task_knowledge_transfer","node_type":"task","title":"Wissenstransfer organisieren","sort_order":60,"config_json":{"legacyTemplateKey":"ob_knowledge_transfer"}},
      {"node_key":"task_badge_key_return","node_type":"task","title":"Schluessel und Badge zurueckgeben","sort_order":70,"config_json":{"legacyTemplateKey":"ob_badge_key_return"}},
      {"node_key":"decide_ad_account","node_type":"decision","title":"AD-Konto vorhanden?","sort_order":80,"config_json":null},
      {"node_key":"task_ad_account_disable","node_type":"task","title":"AD-Konto deaktivieren","sort_order":90,"config_json":{"legacyTemplateKey":"ob_ad_account_disable"}},
      {"node_key":"decide_mailbox","node_type":"decision","title":"Mailbox vorhanden?","sort_order":100,"config_json":null},
      {"node_key":"task_mailbox_disable","node_type":"task","title":"Mailbox deaktivieren","sort_order":110,"config_json":{"legacyTemplateKey":"ob_mailbox_disable"}},
      {"node_key":"decide_habel","node_type":"decision","title":"Habel-Zugang vorhanden?","sort_order":120,"config_json":null},
      {"node_key":"task_habel_user_disable","node_type":"task","title":"Habel-User deaktivieren","sort_order":130,"config_json":{"legacyTemplateKey":"ob_habel_user_disable"}},
      {"node_key":"decide_ln","node_type":"decision","title":"InforLN-Zugang vorhanden?","sort_order":140,"config_json":null},
      {"node_key":"task_ln_user_disable","node_type":"task","title":"InforLN-User deaktivieren","sort_order":150,"config_json":{"legacyTemplateKey":"ob_ln_user_disable"}},
      {"node_key":"decide_hardware","node_type":"decision","title":"Hardware zurueckzugeben?","sort_order":160,"config_json":null},
      {"node_key":"task_hardware_return","node_type":"task","title":"Hardware einziehen","sort_order":170,"config_json":{"legacyTemplateKey":"ob_hardware_return"}},
      {"node_key":"decide_phone","node_type":"decision","title":"Telefon zurueckzugeben?","sort_order":180,"config_json":null},
      {"node_key":"task_phone_return","node_type":"task","title":"Telefon einziehen","sort_order":190,"config_json":{"legacyTemplateKey":"ob_phone_return"}},
      {"node_key":"decide_babtec","node_type":"decision","title":"Babtec-Zugang vorhanden?","sort_order":200,"config_json":null},
      {"node_key":"task_babtec_user_disable","node_type":"task","title":"Babtec-User deaktivieren","sort_order":210,"config_json":{"legacyTemplateKey":"ob_babtec_user_disable"}},
      {"node_key":"decide_gewatec","node_type":"decision","title":"Gewatec-Zugang vorhanden?","sort_order":220,"config_json":null},
      {"node_key":"task_gewatec_user_disable","node_type":"task","title":"Gewatec-User deaktivieren","sort_order":230,"config_json":{"legacyTemplateKey":"ob_gewatec_user_disable"}},
      {"node_key":"decide_provis","node_type":"decision","title":"Provis-Zugang vorhanden?","sort_order":240,"config_json":null},
      {"node_key":"task_provis_user_disable","node_type":"task","title":"Provis-User deaktivieren","sort_order":250,"config_json":{"legacyTemplateKey":"ob_provis_user_disable"}},
      {"node_key":"decide_consense","node_type":"decision","title":"Consense-Zugang vorhanden?","sort_order":260,"config_json":null},
      {"node_key":"task_consense_user_disable","node_type":"task","title":"Consense-User deaktivieren","sort_order":270,"config_json":{"legacyTemplateKey":"ob_consense_user_disable"}},
      {"node_key":"end","node_type":"end","title":"Abgeschlossen","sort_order":280,"config_json":null}
    ]
    $json$::jsonb,
    $json$
    [
      {"source_node_key":"start","target_node_key":"collect_requirements","priority":0,"condition_expression":null},
      {"source_node_key":"collect_requirements","target_node_key":"task_last_day_confirmed","priority":0,"condition_expression":null},
      {"source_node_key":"task_last_day_confirmed","target_node_key":"decide_exit_interview","priority":0,"condition_expression":null},
      {"source_node_key":"decide_exit_interview","target_node_key":"task_exit_interview","priority":0,"condition_expression":"{\"answerKey\":\"ob_exit_interview\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_exit_interview","target_node_key":"decide_knowledge_transfer","priority":1,"condition_expression":null},
      {"source_node_key":"task_exit_interview","target_node_key":"decide_knowledge_transfer","priority":0,"condition_expression":null},
      {"source_node_key":"decide_knowledge_transfer","target_node_key":"task_knowledge_transfer","priority":0,"condition_expression":"{\"answerKey\":\"ob_knowledge_transfer\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_knowledge_transfer","target_node_key":"task_badge_key_return","priority":1,"condition_expression":null},
      {"source_node_key":"task_knowledge_transfer","target_node_key":"task_badge_key_return","priority":0,"condition_expression":null},
      {"source_node_key":"task_badge_key_return","target_node_key":"decide_ad_account","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ad_account","target_node_key":"task_ad_account_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_ad_account\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ad_account","target_node_key":"decide_habel","priority":1,"condition_expression":null},
      {"source_node_key":"task_ad_account_disable","target_node_key":"decide_mailbox","priority":0,"condition_expression":null},
      {"source_node_key":"decide_mailbox","target_node_key":"task_mailbox_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_mailbox\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_mailbox","target_node_key":"decide_habel","priority":1,"condition_expression":null},
      {"source_node_key":"task_mailbox_disable","target_node_key":"decide_habel","priority":0,"condition_expression":null},
      {"source_node_key":"decide_habel","target_node_key":"task_habel_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_habel\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_habel","target_node_key":"decide_ln","priority":1,"condition_expression":null},
      {"source_node_key":"task_habel_user_disable","target_node_key":"decide_ln","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ln","target_node_key":"task_ln_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_ln\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ln","target_node_key":"decide_hardware","priority":1,"condition_expression":null},
      {"source_node_key":"task_ln_user_disable","target_node_key":"decide_hardware","priority":0,"condition_expression":null},
      {"source_node_key":"decide_hardware","target_node_key":"task_hardware_return","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_hardware\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_hardware","target_node_key":"decide_phone","priority":1,"condition_expression":null},
      {"source_node_key":"task_hardware_return","target_node_key":"decide_phone","priority":0,"condition_expression":null},
      {"source_node_key":"decide_phone","target_node_key":"task_phone_return","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_phone\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_phone","target_node_key":"decide_babtec","priority":1,"condition_expression":null},
      {"source_node_key":"task_phone_return","target_node_key":"decide_babtec","priority":0,"condition_expression":null},
      {"source_node_key":"decide_babtec","target_node_key":"task_babtec_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_babtec\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_babtec","target_node_key":"decide_gewatec","priority":1,"condition_expression":null},
      {"source_node_key":"task_babtec_user_disable","target_node_key":"decide_gewatec","priority":0,"condition_expression":null},
      {"source_node_key":"decide_gewatec","target_node_key":"task_gewatec_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_gewatec\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_gewatec","target_node_key":"decide_provis","priority":1,"condition_expression":null},
      {"source_node_key":"task_gewatec_user_disable","target_node_key":"decide_provis","priority":0,"condition_expression":null},
      {"source_node_key":"decide_provis","target_node_key":"task_provis_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_provis\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_provis","target_node_key":"decide_consense","priority":1,"condition_expression":null},
      {"source_node_key":"task_provis_user_disable","target_node_key":"decide_consense","priority":0,"condition_expression":null},
      {"source_node_key":"decide_consense","target_node_key":"task_consense_user_disable","priority":0,"condition_expression":"{\"answerKey\":\"ob_has_consense\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_consense","target_node_key":"end","priority":1,"condition_expression":null},
      {"source_node_key":"task_consense_user_disable","target_node_key":"end","priority":0,"condition_expression":null}
    ]
    $json$::jsonb
);

SELECT upsert_linearized_workflow_definition(
    'department_change',
    'Abteilungswechsel',
    'Linearized T6 workflow definition mapped from legacy department change task templates.',
    'Linearized Published Mapping',
    'T6 published mapping of the legacy department change flow for admin-only runtime validation.',
    'department_change',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Wechselumfang erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"department_change"}},
      {"node_key":"task_hr_system_update","node_type":"task","title":"HR-System aktualisieren","sort_order":20,"config_json":{"legacyTemplateKey":"dc_hr_system_update"}},
      {"node_key":"task_change_date_confirmed","node_type":"task","title":"Wechseldatum bestaetigen","sort_order":30,"config_json":{"legacyTemplateKey":"dc_change_date_confirmed"}},
      {"node_key":"decide_ad_group_update","node_type":"decision","title":"AD-Gruppen anpassen?","sort_order":40,"config_json":null},
      {"node_key":"task_ad_group_update","node_type":"task","title":"AD-Gruppen aktualisieren","sort_order":50,"config_json":{"legacyTemplateKey":"dc_ad_group_update"}},
      {"node_key":"decide_drive_access_update","node_type":"decision","title":"Laufwerksrechte anpassen?","sort_order":60,"config_json":null},
      {"node_key":"task_drive_access_update","node_type":"task","title":"Laufwerk-Zugaenge anpassen","sort_order":70,"config_json":{"legacyTemplateKey":"dc_drive_access_update"}},
      {"node_key":"decide_email_alias_update","node_type":"decision","title":"E-Mail Alias anpassen?","sort_order":80,"config_json":null},
      {"node_key":"task_email_alias_update","node_type":"task","title":"E-Mail Alias anpassen","sort_order":90,"config_json":{"legacyTemplateKey":"dc_email_alias_update"}},
      {"node_key":"decide_hardware_swap","node_type":"decision","title":"Hardware-Tausch noetig?","sort_order":100,"config_json":null},
      {"node_key":"task_hardware_swap","node_type":"task","title":"Hardware tauschen","sort_order":110,"config_json":{"legacyTemplateKey":"dc_hardware_swap"}},
      {"node_key":"decide_habel_access_update","node_type":"decision","title":"Habel-Zugang anpassen?","sort_order":120,"config_json":null},
      {"node_key":"task_habel_access_update","node_type":"task","title":"Habel-Zugang anpassen","sort_order":130,"config_json":{"legacyTemplateKey":"dc_habel_access_update"}},
      {"node_key":"decide_ln_access_update","node_type":"decision","title":"InforLN-Zugang anpassen?","sort_order":140,"config_json":null},
      {"node_key":"task_ln_access_update","node_type":"task","title":"InforLN-Zugang anpassen","sort_order":150,"config_json":{"legacyTemplateKey":"dc_ln_access_update"}},
      {"node_key":"decide_babtec_access_update","node_type":"decision","title":"Babtec-Zugang anpassen?","sort_order":160,"config_json":null},
      {"node_key":"task_babtec_access_update","node_type":"task","title":"Babtec-Zugang anpassen","sort_order":170,"config_json":{"legacyTemplateKey":"dc_babtec_access_update"}},
      {"node_key":"decide_gewatec_access_update","node_type":"decision","title":"Gewatec-Zugang anpassen?","sort_order":180,"config_json":null},
      {"node_key":"task_gewatec_access_update","node_type":"task","title":"Gewatec-Zugang anpassen","sort_order":190,"config_json":{"legacyTemplateKey":"dc_gewatec_access_update"}},
      {"node_key":"decide_provis_access_update","node_type":"decision","title":"Provis-Zugang anpassen?","sort_order":200,"config_json":null},
      {"node_key":"task_provis_access_update","node_type":"task","title":"Provis-Zugang anpassen","sort_order":210,"config_json":{"legacyTemplateKey":"dc_provis_access_update"}},
      {"node_key":"decide_consense_access_update","node_type":"decision","title":"Consense-Zugang anpassen?","sort_order":220,"config_json":null},
      {"node_key":"task_consense_access_update","node_type":"task","title":"Consense-Zugang anpassen","sort_order":230,"config_json":{"legacyTemplateKey":"dc_consense_access_update"}},
      {"node_key":"end","node_type":"end","title":"Abgeschlossen","sort_order":240,"config_json":null}
    ]
    $json$::jsonb,
    $json$
    [
      {"source_node_key":"start","target_node_key":"collect_requirements","priority":0,"condition_expression":null},
      {"source_node_key":"collect_requirements","target_node_key":"task_hr_system_update","priority":0,"condition_expression":null},
      {"source_node_key":"task_hr_system_update","target_node_key":"task_change_date_confirmed","priority":0,"condition_expression":null},
      {"source_node_key":"task_change_date_confirmed","target_node_key":"decide_ad_group_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ad_group_update","target_node_key":"task_ad_group_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_ad_group_change\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ad_group_update","target_node_key":"decide_drive_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_ad_group_update","target_node_key":"decide_drive_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_drive_access_update","target_node_key":"task_drive_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_drive_access_change\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_drive_access_update","target_node_key":"decide_email_alias_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_drive_access_update","target_node_key":"decide_email_alias_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_email_alias_update","target_node_key":"task_email_alias_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_email_alias_change\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_email_alias_update","target_node_key":"decide_hardware_swap","priority":1,"condition_expression":null},
      {"source_node_key":"task_email_alias_update","target_node_key":"decide_hardware_swap","priority":0,"condition_expression":null},
      {"source_node_key":"decide_hardware_swap","target_node_key":"task_hardware_swap","priority":0,"condition_expression":"{\"answerKey\":\"dc_hardware_change\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_hardware_swap","target_node_key":"decide_habel_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_hardware_swap","target_node_key":"decide_habel_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_habel_access_update","target_node_key":"task_habel_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_habel\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_habel_access_update","target_node_key":"decide_ln_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_habel_access_update","target_node_key":"decide_ln_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_ln_access_update","target_node_key":"task_ln_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_ln\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_ln_access_update","target_node_key":"decide_babtec_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_ln_access_update","target_node_key":"decide_babtec_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_babtec_access_update","target_node_key":"task_babtec_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_babtec\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_babtec_access_update","target_node_key":"decide_gewatec_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_babtec_access_update","target_node_key":"decide_gewatec_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_gewatec_access_update","target_node_key":"task_gewatec_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_gewatec\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_gewatec_access_update","target_node_key":"decide_provis_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_gewatec_access_update","target_node_key":"decide_provis_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_provis_access_update","target_node_key":"task_provis_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_provis\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_provis_access_update","target_node_key":"decide_consense_access_update","priority":1,"condition_expression":null},
      {"source_node_key":"task_provis_access_update","target_node_key":"decide_consense_access_update","priority":0,"condition_expression":null},
      {"source_node_key":"decide_consense_access_update","target_node_key":"task_consense_access_update","priority":0,"condition_expression":"{\"answerKey\":\"dc_has_consense\",\"operator\":\"is_true\"}"},
      {"source_node_key":"decide_consense_access_update","target_node_key":"end","priority":1,"condition_expression":null},
      {"source_node_key":"task_consense_access_update","target_node_key":"end","priority":0,"condition_expression":null}
    ]
    $json$::jsonb
);

DROP FUNCTION upsert_linearized_workflow_definition(TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, JSONB, JSONB);
