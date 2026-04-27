-- Legacy-Bereinigung und Modellanpassungen fuer bestehende Datenbestaende.
-- Diese Datei enthaelt bewusst keine Demo-/Seed-Daten.

-- Veraltete Benutzer-, Rollen- und Gruppenreste aus dem frueheren Berechtigungsmodell entfernen.
DELETE FROM app_user_groups
WHERE app_user_id IN (
    SELECT id FROM app_users WHERE external_key = 'kerstin.fricker'
);

DELETE FROM app_user_roles
WHERE app_role_id IN (
    SELECT id
    FROM app_roles
    WHERE role_key IN (
        'special_cases_support',
        'finance_system_admin',
        'qa_system_admin',
        'department_coordinator_finance',
        'department_coordinator_quality'
    )
);

DELETE FROM app_group_roles
WHERE app_role_id IN (
    SELECT id
    FROM app_roles
    WHERE role_key IN (
        'special_cases_support',
        'finance_system_admin',
        'qa_system_admin',
        'department_coordinator_finance',
        'department_coordinator_quality'
    )
);

DELETE FROM app_users
WHERE external_key = 'kerstin.fricker';

DELETE FROM app_roles
WHERE role_key IN (
    'special_cases_support',
    'finance_system_admin',
    'qa_system_admin',
    'department_coordinator_finance',
    'department_coordinator_quality'
);

DELETE FROM app_user_groups
WHERE app_group_id IN (
    SELECT id FROM app_groups WHERE group_key IN ('finance', 'qs')
);

DELETE FROM app_group_roles
WHERE app_group_id IN (
    SELECT id FROM app_groups WHERE group_key IN ('finance', 'qs')
);

DELETE FROM app_groups
WHERE group_key IN ('finance', 'qs');

-- Legacy-Antwortschluessel auf das aktuelle Modell umziehen.
DO $$
DECLARE
    legacy_definition_id INTEGER;
    current_definition_id INTEGER;
BEGIN
    SELECT id
    INTO legacy_definition_id
    FROM workflow_answer_definitions
    WHERE answer_key = 'exchange_requested';

    IF legacy_definition_id IS NULL THEN
        RETURN;
    END IF;

    SELECT id
    INTO current_definition_id
    FROM workflow_answer_definitions
    WHERE answer_key = 'mailbox_requested';

    IF current_definition_id IS NULL THEN
        UPDATE workflow_answers
        SET answer_key = 'mailbox_requested'
        WHERE answer_key = 'exchange_requested';

        UPDATE task_template_conditions
        SET answer_key = 'mailbox_requested'
        WHERE answer_key = 'exchange_requested';

        UPDATE workflow_answer_definitions
        SET answer_key = 'mailbox_requested'
        WHERE id = legacy_definition_id;

        RETURN;
    END IF;

    UPDATE workflow_answers current_answer
    SET
        value_boolean = CASE
            WHEN current_answer.value_boolean IS TRUE OR legacy_answer.value_boolean IS TRUE THEN TRUE
            WHEN current_answer.value_boolean IS FALSE OR legacy_answer.value_boolean IS FALSE THEN FALSE
            ELSE NULL
        END,
        value_text = COALESCE(current_answer.value_text, legacy_answer.value_text),
        value_number = COALESCE(current_answer.value_number, legacy_answer.value_number),
        selected_option_id = COALESCE(current_answer.selected_option_id, legacy_answer.selected_option_id)
    FROM workflow_answers legacy_answer
    WHERE legacy_answer.answer_definition_id = legacy_definition_id
      AND current_answer.answer_definition_id = current_definition_id
      AND current_answer.workflow_id = legacy_answer.workflow_id;

    DELETE FROM workflow_answers legacy_answer
    WHERE legacy_answer.answer_definition_id = legacy_definition_id
      AND EXISTS (
          SELECT 1
          FROM workflow_answers current_answer
          WHERE current_answer.answer_definition_id = current_definition_id
            AND current_answer.workflow_id = legacy_answer.workflow_id
      );

    UPDATE workflow_answers
    SET
        answer_definition_id = current_definition_id,
        answer_key = 'mailbox_requested'
    WHERE answer_definition_id = legacy_definition_id;

    DELETE FROM task_template_conditions legacy_condition
    WHERE legacy_condition.answer_key = 'exchange_requested'
      AND EXISTS (
          SELECT 1
          FROM task_template_conditions current_condition
          WHERE current_condition.task_template_id = legacy_condition.task_template_id
            AND current_condition.condition_group = legacy_condition.condition_group
            AND current_condition.answer_key = 'mailbox_requested'
            AND current_condition.operator = legacy_condition.operator
            AND current_condition.expected_value_text IS NOT DISTINCT FROM legacy_condition.expected_value_text
            AND current_condition.expected_value_boolean IS NOT DISTINCT FROM legacy_condition.expected_value_boolean
            AND current_condition.expected_value_number IS NOT DISTINCT FROM legacy_condition.expected_value_number
      );

    UPDATE task_template_conditions
    SET answer_key = 'mailbox_requested'
    WHERE answer_key = 'exchange_requested';

    UPDATE workflow_answer_definitions
    SET is_active = FALSE
    WHERE id = legacy_definition_id;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM workflow_answer_definitions
        WHERE answer_key = 'hardware_available'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM workflow_answer_definitions
        WHERE answer_key = 'hardware_requested'
    ) THEN
        UPDATE workflow_answers
        SET answer_key = 'hardware_requested'
        WHERE answer_key = 'hardware_available';

        UPDATE task_template_conditions
        SET answer_key = 'hardware_requested'
        WHERE answer_key = 'hardware_available';

        UPDATE workflow_answer_definitions
        SET answer_key = 'hardware_requested'
        WHERE answer_key = 'hardware_available';
    END IF;
END $$;

-- Falls ein Bestand noch ohne Creator-Spalte existiert, wird die Struktur hier nachgezogen.
ALTER TABLE workflows
ADD COLUMN IF NOT EXISTS created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL;

-- Veraltete Templates nur noch deaktivieren, nicht mehr im Seed entfernen.
UPDATE task_templates
SET is_active = FALSE
WHERE template_key IN ('contract_archived', 'kaba_user_created', 'document_sent_to_distribution', 'onboarding_doc_sent_to_supervisor');

-- Neue Spalten fuer datengetriebene Prozessbereich- und Phase-Klassifizierung nachrüsten.
ALTER TABLE task_templates ADD COLUMN IF NOT EXISTS process_area_label VARCHAR(80);
ALTER TABLE task_templates ADD COLUMN IF NOT EXISTS is_department_phase_task BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE workflow_tasks ADD COLUMN IF NOT EXISTS process_area_label VARCHAR(80);
ALTER TABLE workflow_tasks ADD COLUMN IF NOT EXISTS is_department_phase_task BOOLEAN NOT NULL DEFAULT TRUE;

-- Bereichslabel und Phase-Flag fuer bekannte Templates setzen.
UPDATE task_templates
SET process_area_label = 'Abteilungsleitung', is_department_phase_task = FALSE
WHERE template_key = 'supervisor_fills_document';

UPDATE task_templates
SET process_area_label = 'HR', is_department_phase_task = FALSE
WHERE template_key IN ('contract_archived', 'kaba_user_created', 'document_sent_to_distribution', 'onboarding_doc_sent_to_supervisor');

-- Bestehende workflow_tasks aus ihren Templates befüllen (Backfill fuer vorhandene Datenbestaende).
UPDATE workflow_tasks wt
SET
    process_area_label = tt.process_area_label,
    is_department_phase_task = tt.is_department_phase_task
FROM task_templates tt
WHERE wt.task_template_id = tt.id
   OR (wt.task_template_id IS NULL AND wt.task_key = tt.template_key);

-- Historische Responsibility-Keys auf das aktuelle Modell mappen.
WITH task_responsibility_mapping(template_key, responsibility_key) AS (
    VALUES
        ('ad_user_create', 'it_ad'),
        ('permissions_from_reference_user', 'it_ad'),
        ('exchange_create', 'it_mailbox'),
        ('habel_user_create', 'it_habel'),
        ('ln_user_create', 'it_ln'),
        ('internet_access_enable', 'it_ad'),
        ('internal_drive_access_grant', 'it_ad'),
        ('office_install', 'it_hardware'),
        ('hardware_procure', 'it_hardware'),
        ('hardware_setup', 'it_hardware'),
        ('hardware_handover', 'it_hardware'),
        ('phone_prepare', 'it_hardware'),
        ('catia_install', 'it_hardware'),
        ('datev_install', 'it_hardware'),
        ('tisoware_install', 'it_hardware'),
        ('babtec_user_create', 'qs_babtec'),
        ('gewatec_user_create', 'av_gewatec'),
        ('provis_user_create', 'av_provis'),
        ('consense_setup', 'qmb_consense'),
        ('consense_training', 'qmb_consense')
)
UPDATE task_assignments ta
SET
    assignee_responsibility_id = r.id,
    assignment_type = CASE
        WHEN ta.assignee_user_id IS NULL THEN 'responsibility'
        ELSE ta.assignment_type
    END
FROM workflow_tasks wt
JOIN task_responsibility_mapping m ON m.template_key = wt.task_key
JOIN app_responsibilities r ON r.responsibility_key = m.responsibility_key
WHERE wt.id = ta.workflow_task_id
  AND ta.assignee_responsibility_id IN (
      SELECT id
      FROM app_responsibilities
      WHERE responsibility_key IN ('it_accounts', 'it_workplace', 'qs_systems', 'av_systems', 'qmb_systems')
  );

DELETE FROM app_responsibilities
WHERE responsibility_key IN ('it_accounts', 'it_workplace', 'qs_systems', 'av_systems', 'qmb_systems');
