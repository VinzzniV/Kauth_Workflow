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
UPDATE workflow_answers
SET answer_key = 'mailbox_requested'
WHERE answer_key = 'exchange_requested';

UPDATE workflow_answers
SET answer_key = 'hardware_requested'
WHERE answer_key = 'hardware_available';

UPDATE task_template_conditions
SET answer_key = 'mailbox_requested'
WHERE answer_key = 'exchange_requested';

UPDATE task_template_conditions
SET answer_key = 'hardware_requested'
WHERE answer_key = 'hardware_available';

UPDATE workflow_answer_definitions
SET answer_key = 'mailbox_requested'
WHERE answer_key = 'exchange_requested';

UPDATE workflow_answer_definitions
SET answer_key = 'hardware_requested'
WHERE answer_key = 'hardware_available';

UPDATE workflow_answer_definitions
SET is_active = FALSE
WHERE answer_key = 'hardware_available';

-- Falls ein Bestand noch ohne Creator-Spalte existiert, wird die Struktur hier nachgezogen.
ALTER TABLE workflows
ADD COLUMN IF NOT EXISTS created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL;

-- Veraltete Templates nur noch deaktivieren, nicht mehr im Seed entfernen.
UPDATE task_templates
SET is_active = FALSE
WHERE template_key IN ('contract_archived', 'kaba_user_created', 'document_sent_to_distribution', 'onboarding_doc_sent_to_supervisor');

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
