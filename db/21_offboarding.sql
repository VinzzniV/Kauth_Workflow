-- =========================
-- [F7.5.1] Offboarding: Prozesstyp, Requirements, Templates, Conditions, Dependencies
-- =========================
-- Hinweis: initial mit is_active=FALSE angelegt.
-- Aktivierung erfolgt spaeter bewusst ueber die Prozesstyp-Admin-UI.
-- =========================

-- =========================
-- Prozesstyp
-- =========================
INSERT INTO process_types (
    key,
    name,
    description,
    requires_supervisor_step,
    approval_task_template_key,
    requires_target_person,
    icon_key,
    is_active,
    sort_order
)
VALUES (
    'offboarding',
    'Offboarding',
    'Geordneter Abschluss eines Mitarbeiterverhältnisses mit Rückgabe aller Zugänge und Ausstattung.',
    FALSE,
    NULL,
    TRUE,
    'identitat',
    FALSE,
    20
)
ON CONFLICT (key) DO UPDATE
SET
    name                    = EXCLUDED.name,
    description             = EXCLUDED.description,
    requires_supervisor_step = EXCLUDED.requires_supervisor_step,
    approval_task_template_key = EXCLUDED.approval_task_template_key,
    requires_target_person  = EXCLUDED.requires_target_person,
    icon_key                = EXCLUDED.icon_key,
    is_active               = EXCLUDED.is_active,
    sort_order              = EXCLUDED.sort_order;

-- =========================
-- Requirements (Abmeldungsumfang)
-- Alle ob_-Schlüssel sind eindeutig und konfliktfrei zu Onboarding-Schlüsseln.
-- HR füllt diese beim Anlegen des Offboarding-Vorgangs aus.
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order) AS (
    VALUES
        ('ob_has_ad_account',       'AD-Konto vorhanden?',         'Zugänge',               'Hat die Person ein aktives AD-Konto, das deaktiviert werden muss?',                'ad_user',      'boolean', TRUE,  1),
        ('ob_has_mailbox',          'Mailbox vorhanden?',           'Zugänge',               'Hat die Person eine Mailbox, die deaktiviert werden muss?',                        'mailbox',      'boolean', FALSE, 2),
        ('ob_has_hardware',         'Hardware zurückzugeben?',      'Ausstattung',           'Hat die Person Hardware (Laptop, Workstation, etc.), die eingezogen werden muss?', 'pc',           'boolean', TRUE,  3),
        ('ob_has_phone',            'Telefon zurückzugeben?',       'Ausstattung',           'Hat die Person ein tragbares Telefon, das eingezogen werden muss?',                'phone',        'boolean', FALSE, 4),
        ('ob_has_habel',            'Habel-Zugang vorhanden?',      'Programme und Systeme', 'Hat die Person einen aktiven Habel-User?',                                         'habel',        'boolean', FALSE, 5),
        ('ob_has_ln',               'InforLN-Zugang vorhanden?',    'Programme und Systeme', 'Hat die Person einen aktiven InforLN-User?',                                       'inforln',      'boolean', FALSE, 6),
        ('ob_has_babtec',           'Babtec-Zugang vorhanden?',     'Programme und Systeme', 'Hat die Person einen aktiven Babtec-User?',                                        'babtec',       'boolean', FALSE, 7),
        ('ob_has_gewatec',          'Gewatec-Zugang vorhanden?',    'Programme und Systeme', 'Hat die Person einen aktiven Gewatec-User?',                                       'gewatec',      'boolean', FALSE, 8),
        ('ob_has_provis',           'Provis-Zugang vorhanden?',     'Programme und Systeme', 'Hat die Person einen aktiven Provis-User?',                                        'berechtigungen', 'boolean', FALSE, 9),
        ('ob_has_consense',         'Spinfire-Zugang vorhanden?',   'Programme und Systeme', 'Hat die Person einen aktiven Spinfire-User?',                                      'spinfire',     'boolean', FALSE, 10),
        ('ob_exit_interview',       'Austrittsgespräch führen?',    'Abschluss',             'Soll ein Austrittsgespräch mit der ausscheidenden Person geführt werden?',          'identitat',    'boolean', TRUE,  11),
        ('ob_knowledge_transfer',   'Wissenstransfer notwendig?',   'Abschluss',             'Muss vor dem Austritt ein strukturierter Wissenstransfer stattfinden?',             'identitat',    'boolean', FALSE, 12)
)
INSERT INTO workflow_answer_definitions (
    process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'offboarding'),
    s.answer_key, s.title, s.category, s.description, s.icon_key, s.input_type, s.is_required, s.sort_order, TRUE
FROM answer_seed s
ON CONFLICT (process_type_id, answer_key) DO UPDATE
SET
    process_type_id = EXCLUDED.process_type_id,
    title           = EXCLUDED.title,
    category        = EXCLUDED.category,
    description     = EXCLUDED.description,
    icon_key        = EXCLUDED.icon_key,
    input_type      = EXCLUDED.input_type,
    is_required     = EXCLUDED.is_required,
    sort_order      = EXCLUDED.sort_order,
    is_active       = EXCLUDED.is_active;

-- =========================
-- Visibility rules
-- ob_has_mailbox nur anzeigen wenn ob_has_ad_account=true
-- ob_has_phone nur anzeigen wenn ob_has_hardware=true
-- =========================
DELETE FROM workflow_answer_visibility_rules
WHERE answer_definition_id IN (
    SELECT id FROM workflow_answer_definitions
    WHERE answer_key IN ('ob_has_mailbox', 'ob_has_phone')
);

WITH visibility_seed(answer_key, dependency_answer_key, dependency_kind, expected_value_text, missing_result, sort_order) AS (
    VALUES
        ('ob_has_mailbox', 'ob_has_ad_account', 'boolean_true', NULL::text, FALSE, 1),
        ('ob_has_phone',   'ob_has_hardware',   'boolean_true', NULL::text, FALSE, 1)
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
    a.id,
    d.id,
    s.dependency_kind,
    s.expected_value_text,
    s.missing_result,
    s.sort_order
FROM visibility_seed s
JOIN workflow_answer_definitions a ON a.answer_key = s.answer_key
JOIN workflow_answer_definitions d ON d.answer_key = s.dependency_answer_key;

-- Reset rules: Mailbox-Frage zurücksetzen wenn AD-Konto auf false; Telefon-Frage zurücksetzen wenn Hardware auf false
DELETE FROM workflow_answer_reset_rules
WHERE answer_definition_id IN (
    SELECT id FROM workflow_answer_definitions
    WHERE answer_key IN ('ob_has_ad_account', 'ob_has_hardware')
);

WITH reset_seed(answer_key, trigger_kind, target_answer_key, clear_boolean, sort_order) AS (
    VALUES
        ('ob_has_ad_account', 'when_not_true', 'ob_has_mailbox', TRUE, 1),
        ('ob_has_hardware',   'when_not_true', 'ob_has_phone',   TRUE, 1)
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
    a.id,
    s.trigger_kind,
    t.id,
    s.clear_boolean,
    FALSE, FALSE, FALSE, FALSE,
    s.sort_order
FROM reset_seed s
JOIN workflow_answer_definitions a ON a.answer_key = s.answer_key
JOIN workflow_answer_definitions t ON t.answer_key = s.target_answer_key;

-- =========================
-- Task Templates
-- Aufgaben-Schlüssel beginnen alle mit ob_ um Konflikte mit Onboarding-Templates zu vermeiden.
-- process_area_label ist NULL für alle Tasks (analog zu Onboarding-Fachabteilungs-Tasks).
-- is_department_phase_task=TRUE für alle (kein Supervisor-Schritt beim Offboarding).
-- =========================
WITH template_seed(
    template_key,
    title,
    category,
    description,
    icon_key,
    owning_department_name,
    responsibility_key,
    is_required,
    due_in_days,
    sort_order
) AS (
    VALUES
        -- HR-Aufgaben (starten sofort als ready)
        ('ob_last_day_confirmed',   'Letzten Arbeitstag bestätigen',        'HR',              'Letzten Arbeitstag der ausscheidenden Person im System bestätigen. Schaltet alle Zugangs-Entzug-Aufgaben frei.',        'identitat',      'HR', 'hr_onboarding', TRUE,  1, 10),
        ('ob_exit_interview',       'Austrittsgespräch führen',              'HR',              'Strukturiertes Abschlussgespräch mit der ausscheidenden Person führen und dokumentieren.',                             'identitat',      'HR', 'hr_onboarding', TRUE,  5, 20),
        ('ob_knowledge_transfer',   'Wissenstransfer organisieren',          'HR',              'Sicherstellen, dass kritisches Wissen und laufende Aufgaben an Nachfolger oder Team übergeben werden.',                'identitat',      'HR', 'hr_onboarding', TRUE,  5, 30),
        ('ob_badge_key_return',     'Schlüssel und Badge zurückgeben',       'HR',              'Ausweis, Schlüssel und sonstige Zugangsmittel von der ausscheidenden Person einziehen.',                               'identitat',      'HR', 'hr_onboarding', TRUE,  1, 40),

        -- IT: Zugänge (abhängig von ob_last_day_confirmed)
        ('ob_ad_account_disable',   'AD-Konto deaktivieren',                 'Zugänge',         'AD-Konto der ausscheidenden Person deaktivieren und Berechtigungen entziehen.',                                       'ad_user',        'IT', 'it_ad',          TRUE,  1, 100),
        ('ob_mailbox_disable',      'Mailbox deaktivieren',                  'Zugänge',         'Mailbox der ausscheidenden Person deaktivieren.',                                                                      'mailbox',        'IT', 'it_mailbox',     TRUE,  1, 110),
        ('ob_habel_user_disable',   'Habel-User deaktivieren',               'Fachanwendungen', 'Habel-Zugang der ausscheidenden Person sperren.',                                                                      'habel',          'IT', 'it_habel',       TRUE,  2, 120),
        ('ob_ln_user_disable',      'LN-User deaktivieren',                  'Fachanwendungen', 'InforLN-Zugang der ausscheidenden Person sperren.',                                                                    'react',          'IT', 'it_ln',          TRUE,  2, 130),

        -- IT: Hardware
        ('ob_hardware_return',      'Hardware einziehen',                    'Ausstattung',     'Hardware (Laptop, Workstation, Zubehör) der ausscheidenden Person einziehen und auf Vollständigkeit prüfen.',         'pc',             'IT', 'it_hardware',    TRUE,  1, 140),
        ('ob_phone_return',         'Telefon einziehen',                     'Ausstattung',     'Tragbares Telefon der ausscheidenden Person einziehen.',                                                               'phone',          'IT', 'it_hardware',    TRUE,  1, 150),

        -- Fachabteilungen: Zugänge (abhängig von ob_last_day_confirmed)
        ('ob_babtec_user_disable',  'Babtec-User deaktivieren',              'Fachanwendungen', 'Babtec-Zugang der ausscheidenden Person deaktivieren.',                                                                'babtec',         'QS', 'qs_babtec',      TRUE,  2, 200),
        ('ob_gewatec_user_disable', 'Gewatec-User deaktivieren',             'Fachanwendungen', 'Gewatec-Zugang der ausscheidenden Person deaktivieren.',                                                               'gewatec',        'AV', 'av_gewatec',     TRUE,  2, 210),
        ('ob_provis_user_disable',  'Provis-User deaktivieren',              'Fachanwendungen', 'Provis-Zugang der ausscheidenden Person deaktivieren.',                                                                'berechtigungen', 'AV', 'av_provis',      TRUE,  2, 220),
        ('ob_consense_user_disable','Spinfire-User deaktivieren',            'Fachanwendungen', 'Spinfire-Zugang der ausscheidenden Person deaktivieren.',                                                              'spinfire',       'QMB','qmb_consense',   TRUE,  2, 230)
)
INSERT INTO task_templates (
    process_type_id,
    template_key,
    title,
    category,
    description,
    icon_key,
    owning_department_id,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order,
    is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'offboarding'),
    s.template_key,
    s.title,
    s.category,
    s.description,
    s.icon_key,
    d.id,
    r.id,
    NULL,   -- process_area_label: kein separater Bereich beim Offboarding
    TRUE,   -- is_department_phase_task: alle Tasks sind Fachabteilungs-Tasks (kein Supervisor-Schritt)
    s.is_required,
    s.due_in_days,
    s.sort_order,
    TRUE
FROM template_seed s
LEFT JOIN departments d ON d.name = s.owning_department_name
LEFT JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
ON CONFLICT (template_key) DO UPDATE
SET
    process_type_id         = EXCLUDED.process_type_id,
    title                   = EXCLUDED.title,
    category                = EXCLUDED.category,
    description             = EXCLUDED.description,
    icon_key                = EXCLUDED.icon_key,
    owning_department_id    = EXCLUDED.owning_department_id,
    default_responsibility_id = EXCLUDED.default_responsibility_id,
    process_area_label      = EXCLUDED.process_area_label,
    is_department_phase_task = EXCLUDED.is_department_phase_task,
    is_required             = EXCLUDED.is_required,
    due_in_days             = EXCLUDED.due_in_days,
    sort_order              = EXCLUDED.sort_order,
    is_active               = EXCLUDED.is_active;

-- =========================
-- Task generation conditions
-- ob_last_day_confirmed und ob_badge_key_return werden IMMER generiert (keine Conditions).
-- Alle anderen Tasks haben Bedingungen basierend auf den Requirements.
-- =========================
WITH condition_seed(
    template_key,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
) AS (
    VALUES
        ('ob_exit_interview',       1, 'ob_exit_interview',     'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_knowledge_transfer',   1, 'ob_knowledge_transfer', 'is_true', NULL::text, TRUE,  NULL::numeric),

        ('ob_ad_account_disable',   1, 'ob_has_ad_account',     'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_mailbox_disable',      1, 'ob_has_mailbox',        'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_habel_user_disable',   1, 'ob_has_habel',          'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_ln_user_disable',      1, 'ob_has_ln',             'is_true', NULL::text, TRUE,  NULL::numeric),

        ('ob_hardware_return',      1, 'ob_has_hardware',       'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_phone_return',         1, 'ob_has_phone',          'is_true', NULL::text, TRUE,  NULL::numeric),

        ('ob_babtec_user_disable',  1, 'ob_has_babtec',         'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_gewatec_user_disable', 1, 'ob_has_gewatec',        'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_provis_user_disable',  1, 'ob_has_provis',         'is_true', NULL::text, TRUE,  NULL::numeric),
        ('ob_consense_user_disable',1, 'ob_has_consense',       'is_true', NULL::text, TRUE,  NULL::numeric)
)
INSERT INTO task_template_conditions (
    task_template_id,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
)
SELECT
    t.id,
    s.condition_group,
    s.answer_key,
    s.operator,
    s.expected_value_text,
    s.expected_value_boolean,
    s.expected_value_number
FROM condition_seed s
JOIN task_templates t ON t.template_key = s.template_key
ON CONFLICT (
    task_template_id,
    condition_group,
    answer_key,
    operator,
    (expected_value_text IS NULL),
    (COALESCE(expected_value_text, '')),
    (expected_value_boolean IS NULL),
    (COALESCE(expected_value_boolean::text, '')),
    (expected_value_number IS NULL),
    (COALESCE(expected_value_number::text, ''))
) DO UPDATE
SET
    expected_value_text    = EXCLUDED.expected_value_text,
    expected_value_boolean = EXCLUDED.expected_value_boolean,
    expected_value_number  = EXCLUDED.expected_value_number;

-- =========================
-- Task template dependencies
-- ob_last_day_confirmed ist der Synchronisationspunkt:
--   Alle Zugangs-Entzug- und Hardware-Aufgaben starten erst wenn
--   HR den letzten Arbeitstag bestätigt hat.
-- ob_mailbox_disable folgt erst nach ob_ad_account_disable.
-- ob_exit_interview, ob_knowledge_transfer starten sofort (kein Blocker).
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        -- Zugangs-Entzug erst nach letztem Arbeitstag
        ('ob_badge_key_return',     'ob_last_day_confirmed', 'done'),
        ('ob_ad_account_disable',   'ob_last_day_confirmed', 'done'),
        ('ob_habel_user_disable',   'ob_last_day_confirmed', 'done'),
        ('ob_ln_user_disable',      'ob_last_day_confirmed', 'done'),
        ('ob_hardware_return',      'ob_last_day_confirmed', 'done'),
        ('ob_phone_return',         'ob_last_day_confirmed', 'done'),
        ('ob_babtec_user_disable',  'ob_last_day_confirmed', 'done'),
        ('ob_gewatec_user_disable', 'ob_last_day_confirmed', 'done'),
        ('ob_provis_user_disable',  'ob_last_day_confirmed', 'done'),
        ('ob_consense_user_disable','ob_last_day_confirmed', 'done'),

        -- Mailbox erst nach AD-Deaktivierung
        ('ob_mailbox_disable',      'ob_ad_account_disable', 'done')
)
INSERT INTO task_template_dependencies (task_template_id, depends_on_task_template_id, required_status)
SELECT
    t.id,
    dep.id,
    s.required_status
FROM dependency_seed s
JOIN task_templates t   ON t.template_key   = s.task_key
JOIN task_templates dep ON dep.template_key = s.depends_on_task_key
ON CONFLICT (task_template_id, depends_on_task_template_id) DO UPDATE
SET required_status = EXCLUDED.required_status;
