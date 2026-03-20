-- Requirement-Behavior-Regeln fuer bestehende Datenbanken nachziehen.
-- Auf einem frischen Schema laufen die CREATE TABLE/INDEX Statements leer,
-- die anschliessenden Deletes/Seeds stellen aber sicher, dass der aktuelle
-- Regelstand identisch zur 02_seed.sql ist.

CREATE TABLE IF NOT EXISTS workflow_answer_visibility_rules (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    dependency_answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    dependency_kind VARCHAR(40) NOT NULL CHECK (dependency_kind IN ('boolean_true', 'selected_option_value')),
    expected_value_text TEXT,
    missing_result BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (answer_definition_id <> dependency_answer_definition_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_workflow_answer_visibility_rules
    ON workflow_answer_visibility_rules (
        answer_definition_id,
        dependency_answer_definition_id,
        dependency_kind,
        (expected_value_text IS NULL),
        COALESCE(expected_value_text, '')
    );

CREATE TABLE IF NOT EXISTS workflow_answer_validation_rules (
    answer_definition_id INTEGER PRIMARY KEY REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    validation_kind VARCHAR(40) NOT NULL CHECK (validation_kind IN ('text_required', 'single_select_required', 'multi_select_required')),
    message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS workflow_answer_reset_rules (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    trigger_kind VARCHAR(40) NOT NULL CHECK (trigger_kind IN ('when_not_true', 'single_select_mismatch')),
    target_answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    clear_boolean BOOLEAN NOT NULL DEFAULT FALSE,
    clear_text BOOLEAN NOT NULL DEFAULT FALSE,
    clear_number BOOLEAN NOT NULL DEFAULT FALSE,
    clear_selected_option BOOLEAN NOT NULL DEFAULT FALSE,
    clear_selected_options BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (answer_definition_id <> target_answer_definition_id),
    CHECK (clear_boolean OR clear_text OR clear_number OR clear_selected_option OR clear_selected_options)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_workflow_answer_reset_rules
    ON workflow_answer_reset_rules (
        answer_definition_id,
        trigger_kind,
        target_answer_definition_id
    );

CREATE TABLE IF NOT EXISTS workflow_answer_single_select_keep_values (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    option_value VARCHAR(180) NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (answer_definition_id, option_value)
);

CREATE INDEX IF NOT EXISTS idx_workflow_answer_visibility_rules_definition
    ON workflow_answer_visibility_rules(answer_definition_id);

CREATE INDEX IF NOT EXISTS idx_workflow_answer_reset_rules_definition
    ON workflow_answer_reset_rules(answer_definition_id);

DELETE FROM workflow_answer_single_select_keep_values
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'hardware_type'
    )
);

DELETE FROM workflow_answer_reset_rules
WHERE answer_definition_id IN (
        SELECT id
        FROM workflow_answer_definitions
        WHERE answer_key IN (
            'ad_user_requested',
            'comparison_user_available',
            'hardware_requested',
            'hardware_type',
            'internal_drive_access_requested'
        )
    )
   OR target_answer_definition_id IN (
        SELECT id
        FROM workflow_answer_definitions
        WHERE answer_key IN (
            'comparison_user_available',
            'comparison_user_name',
            'hardware_available',
            'hardware_type',
            'laptop_vpn_type',
            'phone_requested',
            'internal_drive_access_roles'
        )
    );

DELETE FROM workflow_answer_validation_rules
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'comparison_user_name',
        'laptop_vpn_type',
        'internal_drive_access_roles'
    )
);

DELETE FROM workflow_answer_visibility_rules
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'comparison_user_available',
        'comparison_user_name',
        'hardware_available',
        'hardware_type',
        'laptop_vpn_type',
        'phone_requested',
        'internal_drive_access_roles'
    )
);

WITH visibility_seed(answer_key, dependency_answer_key, dependency_kind, expected_value_text, missing_result, sort_order) AS (
    VALUES
        ('comparison_user_available', 'ad_user_requested', 'boolean_true', NULL::text, TRUE, 1),
        ('comparison_user_name', 'ad_user_requested', 'boolean_true', NULL::text, FALSE, 1),
        ('comparison_user_name', 'comparison_user_available', 'boolean_true', NULL::text, FALSE, 2),
        ('hardware_available', 'hardware_requested', 'boolean_true', NULL::text, FALSE, 1),
        ('hardware_type', 'hardware_requested', 'boolean_true', NULL::text, TRUE, 1),
        ('phone_requested', 'hardware_requested', 'boolean_true', NULL::text, TRUE, 1),
        ('internal_drive_access_roles', 'internal_drive_access_requested', 'boolean_true', NULL::text, FALSE, 1),
        ('laptop_vpn_type', 'hardware_requested', 'boolean_true', NULL::text, TRUE, 1),
        ('laptop_vpn_type', 'hardware_type', 'selected_option_value', 'laptop', FALSE, 2)
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
    answer_definition.id,
    dependency_definition.id,
    s.dependency_kind,
    s.expected_value_text,
    s.missing_result,
    s.sort_order
FROM visibility_seed s
JOIN workflow_answer_definitions answer_definition ON answer_definition.answer_key = s.answer_key
JOIN workflow_answer_definitions dependency_definition ON dependency_definition.answer_key = s.dependency_answer_key;

WITH validation_seed(answer_key, validation_kind, message) AS (
    VALUES
        ('comparison_user_name', 'text_required', 'Bitte den Referenzuser angeben.'),
        ('internal_drive_access_roles', 'multi_select_required', 'Bitte mindestens eine Funktion für die Laufwerksrechte auswählen.'),
        ('laptop_vpn_type', 'single_select_required', 'Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.')
)
INSERT INTO workflow_answer_validation_rules (
    answer_definition_id,
    validation_kind,
    message
)
SELECT
    definition.id,
    s.validation_kind,
    s.message
FROM validation_seed s
JOIN workflow_answer_definitions definition ON definition.answer_key = s.answer_key;

WITH reset_seed(
    answer_key,
    trigger_kind,
    target_answer_key,
    clear_boolean,
    clear_text,
    clear_number,
    clear_selected_option,
    clear_selected_options,
    sort_order
) AS (
    VALUES
        ('ad_user_requested', 'when_not_true', 'comparison_user_available', TRUE, FALSE, FALSE, FALSE, FALSE, 1),
        ('ad_user_requested', 'when_not_true', 'comparison_user_name', FALSE, TRUE, FALSE, FALSE, FALSE, 2),
        ('comparison_user_available', 'when_not_true', 'comparison_user_name', FALSE, TRUE, FALSE, FALSE, FALSE, 1),
        ('hardware_requested', 'when_not_true', 'hardware_available', TRUE, FALSE, FALSE, FALSE, FALSE, 1),
        ('hardware_requested', 'when_not_true', 'phone_requested', TRUE, FALSE, FALSE, FALSE, FALSE, 2),
        ('hardware_requested', 'when_not_true', 'hardware_type', FALSE, FALSE, FALSE, TRUE, TRUE, 3),
        ('hardware_requested', 'when_not_true', 'laptop_vpn_type', FALSE, FALSE, FALSE, TRUE, TRUE, 4),
        ('internal_drive_access_requested', 'when_not_true', 'internal_drive_access_roles', FALSE, FALSE, FALSE, FALSE, TRUE, 1),
        ('hardware_type', 'single_select_mismatch', 'laptop_vpn_type', FALSE, FALSE, FALSE, TRUE, TRUE, 1)
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
    answer_definition.id,
    s.trigger_kind,
    target_definition.id,
    s.clear_boolean,
    s.clear_text,
    s.clear_number,
    s.clear_selected_option,
    s.clear_selected_options,
    s.sort_order
FROM reset_seed s
JOIN workflow_answer_definitions answer_definition ON answer_definition.answer_key = s.answer_key
JOIN workflow_answer_definitions target_definition ON target_definition.answer_key = s.target_answer_key;

WITH keep_value_seed(answer_key, option_value, sort_order) AS (
    VALUES
        ('hardware_type', 'laptop', 1)
)
INSERT INTO workflow_answer_single_select_keep_values (
    answer_definition_id,
    option_value,
    sort_order
)
SELECT
    definition.id,
    s.option_value,
    s.sort_order
FROM keep_value_seed s
JOIN workflow_answer_definitions definition ON definition.answer_key = s.answer_key;
