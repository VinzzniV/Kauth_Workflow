WITH onboarding_process AS (
    SELECT id
    FROM process_types
    WHERE key = 'onboarding'
)
INSERT INTO workflow_answer_definitions (
    process_type_id,
    answer_key,
    title,
    category,
    description,
    icon_key,
    input_type,
    is_required,
    sort_order,
    is_active
)
SELECT
    onboarding_process.id,
    'hardware_takeover_details',
    'Zu übernehmende Hardware',
    'Ausstattung',
    'Welche vorhandene Hardware wird übernommen? Bitte z. B. Rechnernummer, Asset-ID oder kurzen Hinweis angeben.',
    'pc',
    'text',
    FALSE,
    10,
    TRUE
FROM onboarding_process
ON CONFLICT (process_type_id, answer_key) DO UPDATE
SET
    title = EXCLUDED.title,
    category = EXCLUDED.category,
    description = EXCLUDED.description,
    icon_key = EXCLUDED.icon_key,
    input_type = EXCLUDED.input_type,
    is_required = EXCLUDED.is_required,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

DELETE FROM workflow_answer_visibility_rules
WHERE answer_definition_id IN (
    SELECT d.id
    FROM workflow_answer_definitions d
    JOIN process_types pt ON pt.id = d.process_type_id
    WHERE pt.key = 'onboarding'
      AND d.answer_key = 'hardware_takeover_details'
);

WITH visibility_seed AS (
    SELECT
        answer_definition.id AS answer_definition_id,
        dependency_definition.id AS dependency_answer_definition_id,
        'boolean_true'::TEXT AS dependency_kind,
        NULL::TEXT AS expected_value_text,
        FALSE AS missing_result,
        1 AS sort_order
    FROM workflow_answer_definitions answer_definition
    JOIN process_types pt ON pt.id = answer_definition.process_type_id
    JOIN workflow_answer_definitions dependency_definition
        ON dependency_definition.process_type_id = pt.id
       AND dependency_definition.answer_key = 'hardware_available'
    WHERE pt.key = 'onboarding'
      AND answer_definition.answer_key = 'hardware_takeover_details'
)
INSERT INTO workflow_answer_visibility_rules (
    answer_definition_id,
    dependency_answer_definition_id,
    dependency_kind,
    expected_value_text,
    missing_result,
    sort_order
)
SELECT
    answer_definition_id,
    dependency_answer_definition_id,
    dependency_kind,
    expected_value_text,
    missing_result,
    sort_order
FROM visibility_seed;

DELETE FROM workflow_answer_validation_rules
WHERE answer_definition_id IN (
    SELECT d.id
    FROM workflow_answer_definitions d
    JOIN process_types pt ON pt.id = d.process_type_id
    WHERE pt.key = 'onboarding'
      AND d.answer_key = 'hardware_takeover_details'
);

INSERT INTO workflow_answer_validation_rules (
    answer_definition_id,
    validation_kind,
    message
)
SELECT
    d.id,
    'text_required',
    'Bitte angeben, welche Hardware übernommen wird.'
FROM workflow_answer_definitions d
JOIN process_types pt ON pt.id = d.process_type_id
WHERE pt.key = 'onboarding'
  AND d.answer_key = 'hardware_takeover_details';

DELETE FROM workflow_answer_reset_rules
WHERE answer_definition_id IN (
    SELECT d.id
    FROM workflow_answer_definitions d
    JOIN process_types pt ON pt.id = d.process_type_id
    WHERE pt.key = 'onboarding'
      AND d.answer_key = 'hardware_available'
)
AND target_answer_definition_id IN (
    SELECT d.id
    FROM workflow_answer_definitions d
    JOIN process_types pt ON pt.id = d.process_type_id
    WHERE pt.key = 'onboarding'
      AND d.answer_key = 'hardware_takeover_details'
);

WITH reset_seed AS (
    SELECT
        answer_definition.id AS answer_definition_id,
        target_definition.id AS target_answer_definition_id,
        'when_not_true'::TEXT AS trigger_kind,
        FALSE AS clear_boolean,
        TRUE AS clear_text,
        FALSE AS clear_number,
        FALSE AS clear_selected_option,
        FALSE AS clear_selected_options,
        1 AS sort_order
    FROM workflow_answer_definitions answer_definition
    JOIN process_types pt ON pt.id = answer_definition.process_type_id
    JOIN workflow_answer_definitions target_definition
        ON target_definition.process_type_id = pt.id
       AND target_definition.answer_key = 'hardware_takeover_details'
    WHERE pt.key = 'onboarding'
      AND answer_definition.answer_key = 'hardware_available'
)
INSERT INTO workflow_answer_reset_rules (
    answer_definition_id,
    trigger_kind,
    target_answer_definition_id,
    clear_boolean,
    clear_text,
    clear_number,
    clear_selected_option,
    clear_selected_options,
    sort_order
)
SELECT
    answer_definition_id,
    trigger_kind,
    target_answer_definition_id,
    clear_boolean,
    clear_text,
    clear_number,
    clear_selected_option,
    clear_selected_options,
    sort_order
FROM reset_seed;
