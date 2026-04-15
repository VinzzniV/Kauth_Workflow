-- Phase C precheck summary:
-- - name_change seeds define dedicated answer definitions, rename-focused templates and effective-date dependencies.
-- - position_change seeds define dedicated answer definitions, change templates, conditions and effective-date dependencies.
-- - role_change seeds define dedicated answer definitions, change templates, conditions and effective-date dependencies.
-- - all three process types currently require no supervisor step and are therefore mapped without approval phase.

ALTER TABLE workflow_nodes
    DROP CONSTRAINT IF EXISTS workflow_nodes_node_type_check;

ALTER TABLE workflow_nodes
    ADD CONSTRAINT workflow_nodes_node_type_check
    CHECK (node_type IN ('start', 'form', 'approval', 'task', 'decision', 'parallel_split', 'parallel_join', 'automation', 'measure_provision', 'measure_deprovision', 'measure_change', 'measure_rename', 'setup', 'end'));

SELECT upsert_linearized_workflow_definition(
    'name_change',
    'Namensaenderung',
    'Business-phase workflow definition mapped to the legacy name change task generator.',
    'Business Phase Mapping',
    'Published name change mapping with a rename measure block and internal task generation.',
    'name_change',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Namensänderung erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"name_change"}},
      {"node_key":"department_setup","node_type":"measure_rename","title":"Umbenennungsmaßnahmen erzeugen","sort_order":20,"config_json":null},
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
    'position_change',
    'Positionswechsel',
    'Business-phase workflow definition mapped to the legacy position change task generator.',
    'Business Phase Mapping',
    'Published position change mapping with a change measure block and internal task generation.',
    'position_change',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Positionswechsel erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"position_change"}},
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

SELECT upsert_linearized_workflow_definition(
    'role_change',
    'Rollenwechsel',
    'Business-phase workflow definition mapped to the legacy role change task generator.',
    'Business Phase Mapping',
    'Published role change mapping with a change measure block and internal task generation.',
    'role_change',
    $json$
    [
      {"node_key":"start","node_type":"start","title":"Start","sort_order":0,"config_json":null},
      {"node_key":"collect_requirements","node_type":"form","title":"Rollenwechsel erfassen","sort_order":10,"config_json":{"legacyProcessTypeKey":"role_change"}},
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
