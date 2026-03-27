-- =========================
-- [F7.5.5] Rollenwechsel: Prozesstyp, Requirements, Templates, Conditions, Dependencies
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
    'role_change',
    'Rollenwechsel',
    'Koordinierte Anpassung einer Mitarbeiterrolle mit gezielter Aktualisierung von Rollen- und Berechtigungszuweisungen.',
    FALSE,
    NULL,
    TRUE,
    'identitat',
    FALSE,
    60
)
ON CONFLICT (key) DO UPDATE
SET
    name                       = EXCLUDED.name,
    description                = EXCLUDED.description,
    requires_supervisor_step   = EXCLUDED.requires_supervisor_step,
    approval_task_template_key = EXCLUDED.approval_task_template_key,
    requires_target_person     = EXCLUDED.requires_target_person,
    icon_key                   = EXCLUDED.icon_key,
    is_active                  = EXCLUDED.is_active,
    sort_order                 = EXCLUDED.sort_order;

-- =========================
-- Requirements
-- Alle rc_-Schluessel sind eindeutig und konfliktfrei zu anderen Prozesstypen.
-- HR fuellt diese beim Anlegen des Rollenwechsel-Vorgangs aus.
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order) AS (
    VALUES
        ('rc_new_role',               'Neue Rolle',                          'Rollendetails',        'Neue Rolle oder Berechtigungsfunktion, die die Person kuenftig erhalten soll.',            'identitat',      'text',    TRUE, 1),
        ('rc_effective_date',         'Wirksamkeitsdatum',                   'Rollendetails',        'Datum, ab dem die neue Rolle wirksam wird.',                                                 'identitat',      'text',    TRUE, 2),
        ('rc_role_assignment_change', 'Rollen-Zuweisung anpassen?',          'Berechtigungen',       'Muessen fachliche oder technische Rollen explizit neu zugewiesen oder entzogen werden?',   'ad_user',        'boolean', TRUE, 3),
        ('rc_permission_change',      'Weitere Berechtigungen anpassen?',    'Berechtigungen',       'Muessen zusaetzliche Berechtigungen oder Profile an die neue Rolle angepasst werden?',      'berechtigungen', 'boolean', FALSE,4),
        ('rc_ad_groups_change',       'AD-Gruppen anpassen?',                'Zugaenge',             'Muessen AD-Gruppen und Verzeichnisrollen an die neue Rolle angepasst werden?',              'ad_user',        'boolean', FALSE,5),
        ('rc_mailbox_change',         'Mailbox oder Alias anpassen?',        'Zugaenge',             'Muessen mailboxbezogene Sichtbarkeit oder Aliasrechte geaendert werden?',                   'mailbox',        'boolean', FALSE,6),
        ('rc_has_habel',              'Habel-Zugang anpassen?',              'Programme und Systeme','Muessen Habel-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',       'habel',          'boolean', FALSE,7),
        ('rc_has_ln',                 'InforLN-Zugang anpassen?',            'Programme und Systeme','Muessen InforLN-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',     'inforln',        'boolean', FALSE,8),
        ('rc_has_babtec',             'Babtec-Zugang anpassen?',             'Programme und Systeme','Muessen Babtec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',      'babtec',         'boolean', FALSE,9),
        ('rc_has_gewatec',            'Gewatec-Zugang anpassen?',            'Programme und Systeme','Muessen Gewatec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',     'gewatec',        'boolean', FALSE,10),
        ('rc_has_provis',             'Provis-Zugang anpassen?',             'Programme und Systeme','Muessen Provis-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',      'berechtigungen', 'boolean', FALSE,11),
        ('rc_has_consense',           'Spinfire-Zugang anpassen?',           'Programme und Systeme','Muessen Spinfire-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',    'spinfire',       'boolean', FALSE,12)
)
INSERT INTO workflow_answer_definitions (
    process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'role_change'),
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
-- Task Templates
-- Aufgaben-Schluessel beginnen alle mit rc_ um Konflikte mit anderen Prozesstypen zu vermeiden.
-- is_department_phase_task=TRUE fuer alle (kein Supervisor-Schritt beim Rollenwechsel).
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
        ('rc_effective_date_confirmed',   'Wirksamkeitsdatum bestaetigen',          'HR',              'Bestaetigen, dass das Wirksamkeitsdatum fuer den Rollenwechsel erreicht ist. Schaltet Folgeaufgaben frei.', 'identitat',      'HR', 'hr_onboarding', TRUE, 1, 10),
        ('rc_hr_master_data_update',      'Rolle in HR-Stammdaten aktualisieren',   'HR',              'Neue Rolle in Personalakte und HR-Stammdaten nachfuehren.',                                                    'identitat',      'HR', 'hr_onboarding', TRUE, 2, 20),
        ('rc_role_assignment_update',     'Rollen-Zuweisung aktualisieren',         'Berechtigungen',  'Fachliche und technische Rollen der betroffenen Person auf die neue Rolle umstellen.',                        'ad_user',        'IT', 'it_ad',          TRUE, 2, 100),
        ('rc_permission_profile_update',  'Berechtigungsprofil aktualisieren',      'Berechtigungen',  'Weitere Berechtigungsprofile und Freigaben an die neue Rolle anpassen.',                                       'berechtigungen', 'IT', 'it_ad',          TRUE, 2, 110),
        ('rc_ad_groups_update',           'AD-Gruppen aktualisieren',               'Zugaenge',        'AD-Gruppen und Verzeichnisrollen an die neue Rolle anpassen.',                                                 'ad_user',        'IT', 'it_ad',          TRUE, 2, 120),
        ('rc_mailbox_update',             'Mailbox und Alias anpassen',             'Zugaenge',        'Mailboxbezogene Sichtbarkeit oder Aliasrechte an die neue Rolle anpassen.',                                    'mailbox',        'IT', 'it_mailbox',     TRUE, 3, 130),
        ('rc_habel_access_update',        'Habel-Zugang anpassen',                  'Fachanwendungen', 'Habel-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                                 'habel',          'IT', 'it_habel',       TRUE, 3, 200),
        ('rc_ln_access_update',           'InforLN-Zugang anpassen',                'Fachanwendungen', 'InforLN-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                               'react',          'IT', 'it_ln',          TRUE, 3, 210),
        ('rc_babtec_access_update',       'Babtec-Zugang anpassen',                 'Fachanwendungen', 'Babtec-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                                'babtec',         'QS', 'qs_babtec',      TRUE, 3, 220),
        ('rc_gewatec_access_update',      'Gewatec-Zugang anpassen',                'Fachanwendungen', 'Gewatec-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                               'gewatec',        'AV', 'av_gewatec',     TRUE, 3, 230),
        ('rc_provis_access_update',       'Provis-Zugang anpassen',                 'Fachanwendungen', 'Provis-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                                'berechtigungen', 'AV', 'av_provis',      TRUE, 3, 240),
        ('rc_consense_access_update',     'Spinfire-Zugang anpassen',               'Fachanwendungen', 'Spinfire-Rollen oder Berechtigungen an die neue Rolle anpassen.',                                              'spinfire',       'QMB','qmb_consense',   TRUE, 3, 250)
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
    (SELECT id FROM process_types WHERE key = 'role_change'),
    s.template_key,
    s.title,
    s.category,
    s.description,
    s.icon_key,
    d.id,
    r.id,
    NULL,
    TRUE,
    s.is_required,
    s.due_in_days,
    s.sort_order,
    TRUE
FROM template_seed s
LEFT JOIN departments d ON d.name = s.owning_department_name
LEFT JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
ON CONFLICT (template_key) DO UPDATE
SET
    process_type_id           = EXCLUDED.process_type_id,
    title                     = EXCLUDED.title,
    category                  = EXCLUDED.category,
    description               = EXCLUDED.description,
    icon_key                  = EXCLUDED.icon_key,
    owning_department_id      = EXCLUDED.owning_department_id,
    default_responsibility_id = EXCLUDED.default_responsibility_id,
    process_area_label        = EXCLUDED.process_area_label,
    is_department_phase_task  = EXCLUDED.is_department_phase_task,
    is_required               = EXCLUDED.is_required,
    due_in_days               = EXCLUDED.due_in_days,
    sort_order                = EXCLUDED.sort_order,
    is_active                 = EXCLUDED.is_active;

-- =========================
-- Task generation conditions
-- rc_effective_date_confirmed und rc_hr_master_data_update werden IMMER generiert.
-- Alle weiteren Tasks haengen an den konkret angeforderten Rollen- und Berechtigungsanpassungen.
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
        ('rc_role_assignment_update',    1, 'rc_role_assignment_change', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_permission_profile_update', 1, 'rc_permission_change',      'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_ad_groups_update',          1, 'rc_ad_groups_change',      'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_mailbox_update',            1, 'rc_mailbox_change',        'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_habel_access_update',       1, 'rc_has_habel',             'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_ln_access_update',          1, 'rc_has_ln',                'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_babtec_access_update',      1, 'rc_has_babtec',            'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_gewatec_access_update',     1, 'rc_has_gewatec',           'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_provis_access_update',      1, 'rc_has_provis',            'is_true', NULL::text, TRUE, NULL::numeric),
        ('rc_consense_access_update',    1, 'rc_has_consense',          'is_true', NULL::text, TRUE, NULL::numeric)
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
-- rc_effective_date_confirmed ist der Synchronisationspunkt:
--   Rollen- und Berechtigungsanpassungen starten erst zum Wirksamkeitsdatum.
-- rc_hr_master_data_update kann sofort vorbereitet werden.
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        ('rc_role_assignment_update',    'rc_effective_date_confirmed', 'done'),
        ('rc_permission_profile_update', 'rc_effective_date_confirmed', 'done'),
        ('rc_ad_groups_update',          'rc_effective_date_confirmed', 'done'),
        ('rc_mailbox_update',            'rc_effective_date_confirmed', 'done'),
        ('rc_habel_access_update',       'rc_effective_date_confirmed', 'done'),
        ('rc_ln_access_update',          'rc_effective_date_confirmed', 'done'),
        ('rc_babtec_access_update',      'rc_effective_date_confirmed', 'done'),
        ('rc_gewatec_access_update',     'rc_effective_date_confirmed', 'done'),
        ('rc_provis_access_update',      'rc_effective_date_confirmed', 'done'),
        ('rc_consense_access_update',    'rc_effective_date_confirmed', 'done')
)
INSERT INTO task_template_dependencies (task_template_id, depends_on_task_template_id, required_status)
SELECT
    t.id,
    dep.id,
    s.required_status
FROM dependency_seed s
JOIN task_templates t   ON t.template_key = s.task_key
JOIN task_templates dep ON dep.template_key = s.depends_on_task_key
ON CONFLICT (task_template_id, depends_on_task_template_id) DO UPDATE
SET required_status = EXCLUDED.required_status;
