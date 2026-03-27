-- =========================
-- [F7.5.4] Positionswechsel: Prozesstyp, Requirements, Templates, Conditions, Dependencies
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
    'position_change',
    'Positionswechsel',
    'Koordinierter Wechsel eines Mitarbeiters in eine neue Position mit Anpassung von Berechtigungen, Systemzugaengen und Schulungen.',
    FALSE,
    NULL,
    TRUE,
    'identitat',
    FALSE,
    50
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
-- Alle pc_-Schluessel sind eindeutig und konfliktfrei zu anderen Prozesstypen.
-- HR fuellt diese beim Anlegen des Positionswechsel-Vorgangs aus.
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order) AS (
    VALUES
        ('pc_new_position',         'Neue Position / Rolle',               'Wechseldetails',        'Neue Position oder Rolle, die die Person kuenftig ausueben soll.',                            'identitat',      'text',    TRUE,  1),
        ('pc_change_date',          'Wechseldatum',                        'Wechseldetails',        'Datum, ab dem die neue Position wirksam wird.',                                                 'identitat',      'text',    TRUE,  2),
        ('pc_permission_change',    'Berechtigungen anpassen?',            'Berechtigungen',        'Muessen allgemeine Berechtigungen und Zugriffsprofile wegen der neuen Position angepasst werden?', 'ad_user',    'boolean', TRUE,  3),
        ('pc_training_required',    'Neue Schulungen erforderlich?',       'Qualifizierung',        'Sind fuer die neue Position neue Schulungen oder Einweisungen notwendig?',                     'identitat',      'boolean', FALSE, 4),
        ('pc_ad_groups_change',     'AD-Gruppen anpassen?',                'Zugaenge',              'Muessen AD-Gruppen und Rollen fuer die neue Position geaendert werden?',                       'ad_user',        'boolean', FALSE, 5),
        ('pc_drive_access_change',  'Laufwerk-Zugaenge anpassen?',         'Zugaenge',              'Muessen Laufwerks- und Datei-Zugriffe an die neue Position angepasst werden?',                 'pc',             'boolean', FALSE, 6),
        ('pc_mail_alias_change',    'Mailbox oder Alias anpassen?',        'Zugaenge',              'Muessen Mailbox-bezogene Sichtbarkeit oder Aliasdaten geaendert werden?',                      'mailbox',        'boolean', FALSE, 7),
        ('pc_has_habel',            'Habel-Zugang anpassen?',              'Programme und Systeme', 'Muessen Habel-Berechtigungen wegen der neuen Position angepasst werden?',                      'habel',          'boolean', FALSE, 8),
        ('pc_has_ln',               'InforLN-Zugang anpassen?',            'Programme und Systeme', 'Muessen InforLN-Berechtigungen wegen der neuen Position angepasst werden?',                    'inforln',        'boolean', FALSE, 9),
        ('pc_has_babtec',           'Babtec-Zugang anpassen?',             'Programme und Systeme', 'Muessen Babtec-Berechtigungen wegen der neuen Position angepasst werden?',                     'babtec',         'boolean', FALSE, 10),
        ('pc_has_gewatec',          'Gewatec-Zugang anpassen?',            'Programme und Systeme', 'Muessen Gewatec-Berechtigungen wegen der neuen Position angepasst werden?',                    'gewatec',        'boolean', FALSE, 11),
        ('pc_has_provis',           'Provis-Zugang anpassen?',             'Programme und Systeme', 'Muessen Provis-Berechtigungen wegen der neuen Position angepasst werden?',                     'berechtigungen', 'boolean', FALSE, 12),
        ('pc_has_consense',         'Spinfire-Zugang anpassen?',           'Programme und Systeme', 'Muessen Spinfire-Berechtigungen wegen der neuen Position angepasst werden?',                   'spinfire',       'boolean', FALSE, 13)
)
INSERT INTO workflow_answer_definitions (
    process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'position_change'),
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
-- Aufgaben-Schluessel beginnen alle mit pc_ um Konflikte mit anderen Prozesstypen zu vermeiden.
-- is_department_phase_task=TRUE fuer alle (kein Supervisor-Schritt beim Positionswechsel).
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
        ('pc_change_date_confirmed',     'Wechseldatum bestaetigen',            'HR',              'Bestaetigen, dass das Wechseldatum fuer die neue Position erreicht ist. Schaltet Folgeaufgaben frei.', 'identitat',      'HR', 'hr_onboarding', TRUE, 1,  10),
        ('pc_hr_master_data_update',     'Position in HR-Stammdaten aktualisieren', 'HR',          'Neue Position in Personalakte und HR-Stammdaten nachfuehren.',                                           'identitat',      'HR', 'hr_onboarding', TRUE, 2,  20),
        ('pc_permission_profile_update', 'Berechtigungsprofil aktualisieren',   'Berechtigungen', 'Allgemeine Berechtigungsprofile und Freigaben an die neue Position anpassen.',                            'ad_user',        'IT', 'it_ad',          TRUE, 2, 100),
        ('pc_training_assign',           'Schulungen einplanen',                'Qualifizierung',  'Noetige Schulungen und Einweisungen fuer die neue Position planen und dokumentieren.',                    'identitat',      'HR', 'hr_onboarding', TRUE, 5, 110),
        ('pc_ad_groups_update',          'AD-Gruppen aktualisieren',            'Zugaenge',        'AD-Gruppen und Rollen entsprechend der neuen Position anpassen.',                                          'ad_user',        'IT', 'it_ad',          TRUE, 2, 120),
        ('pc_drive_access_update',       'Laufwerk-Zugaenge anpassen',          'Zugaenge',        'Datei- und Laufwerksberechtigungen auf die Anforderungen der neuen Position umstellen.',                  'pc',             'IT', 'it_ad',          TRUE, 2, 130),
        ('pc_mailbox_update',            'Mailbox und Alias anpassen',          'Zugaenge',        'Mailbox-bezogene Sichtbarkeit oder Aliasdaten an die neue Position anpassen.',                             'mailbox',        'IT', 'it_mailbox',     TRUE, 3, 140),
        ('pc_habel_access_update',       'Habel-Zugang anpassen',               'Fachanwendungen', 'Habel-Berechtigungen auf die neue Position umstellen.',                                                     'habel',          'IT', 'it_habel',       TRUE, 3, 200),
        ('pc_ln_access_update',          'InforLN-Zugang anpassen',             'Fachanwendungen', 'InforLN-Berechtigungen auf die neue Position umstellen.',                                                   'react',          'IT', 'it_ln',          TRUE, 3, 210),
        ('pc_babtec_access_update',      'Babtec-Zugang anpassen',              'Fachanwendungen', 'Babtec-Berechtigungen auf die neue Position umstellen.',                                                    'babtec',         'QS', 'qs_babtec',      TRUE, 3, 220),
        ('pc_gewatec_access_update',     'Gewatec-Zugang anpassen',             'Fachanwendungen', 'Gewatec-Berechtigungen auf die neue Position umstellen.',                                                   'gewatec',        'AV', 'av_gewatec',     TRUE, 3, 230),
        ('pc_provis_access_update',      'Provis-Zugang anpassen',              'Fachanwendungen', 'Provis-Berechtigungen auf die neue Position umstellen.',                                                    'berechtigungen', 'AV', 'av_provis',      TRUE, 3, 240),
        ('pc_consense_access_update',    'Spinfire-Zugang anpassen',            'Fachanwendungen', 'Spinfire-Berechtigungen auf die neue Position umstellen.',                                                  'spinfire',       'QMB','qmb_consense',   TRUE, 3, 250)
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
    (SELECT id FROM process_types WHERE key = 'position_change'),
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
-- pc_change_date_confirmed und pc_hr_master_data_update werden IMMER generiert.
-- Alle weiteren Tasks haengen an den konkret angeforderten Aenderungen.
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
        ('pc_permission_profile_update', 1, 'pc_permission_change',   'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_training_assign',           1, 'pc_training_required',   'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_ad_groups_update',          1, 'pc_ad_groups_change',    'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_drive_access_update',       1, 'pc_drive_access_change', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_mailbox_update',            1, 'pc_mail_alias_change',   'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_habel_access_update',       1, 'pc_has_habel',           'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_ln_access_update',          1, 'pc_has_ln',              'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_babtec_access_update',      1, 'pc_has_babtec',          'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_gewatec_access_update',     1, 'pc_has_gewatec',         'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_provis_access_update',      1, 'pc_has_provis',          'is_true', NULL::text, TRUE, NULL::numeric),
        ('pc_consense_access_update',    1, 'pc_has_consense',        'is_true', NULL::text, TRUE, NULL::numeric)
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
-- pc_change_date_confirmed ist der Synchronisationspunkt:
--   Technische und fachliche Aenderungen starten erst zum Wirksamkeitsdatum.
-- pc_hr_master_data_update kann sofort vorbereitet werden.
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        ('pc_permission_profile_update', 'pc_change_date_confirmed', 'done'),
        ('pc_training_assign',           'pc_change_date_confirmed', 'done'),
        ('pc_ad_groups_update',          'pc_change_date_confirmed', 'done'),
        ('pc_drive_access_update',       'pc_change_date_confirmed', 'done'),
        ('pc_mailbox_update',            'pc_change_date_confirmed', 'done'),
        ('pc_habel_access_update',       'pc_change_date_confirmed', 'done'),
        ('pc_ln_access_update',          'pc_change_date_confirmed', 'done'),
        ('pc_babtec_access_update',      'pc_change_date_confirmed', 'done'),
        ('pc_gewatec_access_update',     'pc_change_date_confirmed', 'done'),
        ('pc_provis_access_update',      'pc_change_date_confirmed', 'done'),
        ('pc_consense_access_update',    'pc_change_date_confirmed', 'done')
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
