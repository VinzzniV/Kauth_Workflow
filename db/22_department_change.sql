-- =========================
-- [F7.5.2] Abteilungswechsel: Prozesstyp, Requirements, Templates, Conditions, Dependencies
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
    'department_change',
    'Abteilungswechsel',
    'Koordinierter Wechsel eines Mitarbeiters in eine andere Abteilung mit Anpassung aller Zugänge und Ausstattung.',
    FALSE,
    NULL,
    TRUE,
    'identitat',
    FALSE,
    30
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
-- Requirements (Wechselumfang)
-- Alle dc_-Schlüssel sind eindeutig und konfliktfrei zu anderen Prozesstypen.
-- HR füllt diese beim Anlegen des Abteilungswechsel-Vorgangs aus.
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order) AS (
    VALUES
        ('dc_new_department',       'Neue Abteilung',                  'Wechseldetails',        'Name der Zielabteilung, in die der Mitarbeiter wechselt.',                             'identitat',      'text',    TRUE,  1),
        ('dc_change_date',          'Wechseldatum',                    'Wechseldetails',        'Geplanter Termin des Abteilungswechsels (z. B. 2025-07-01).',                          'identitat',      'text',    TRUE,  2),
        ('dc_ad_group_change',      'AD-Gruppen anpassen?',            'Zugänge',               'Müssen AD-Gruppen und Berechtigungen an die neue Abteilung angepasst werden?',         'ad_user',        'boolean', TRUE,  3),
        ('dc_drive_access_change',  'Laufwerk-Zugänge anpassen?',      'Zugänge',               'Müssen Netzlaufwerk-Zugriffsrechte für die neue Abteilung geändert werden?',           'pc',             'boolean', TRUE,  4),
        ('dc_email_alias_change',   'E-Mail Alias anpassen?',          'Zugänge',               'Muss der E-Mail Alias wegen Abteilungsbezug im Mailnamen geändert werden?',            'mailbox',        'boolean', FALSE, 5),
        ('dc_hardware_change',      'Hardware-Tausch notwendig?',      'Ausstattung',           'Muss die Hardware (z. B. stationär ↔ mobil) aufgrund der neuen Abteilung getauscht werden?', 'pc',      'boolean', FALSE, 6),
        ('dc_has_habel',            'Habel-Zugang anpassen?',          'Programme und Systeme', 'Muss der Habel-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'habel',          'boolean', FALSE, 7),
        ('dc_has_ln',               'InforLN-Zugang anpassen?',        'Programme und Systeme', 'Muss der InforLN-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?','inforln',       'boolean', FALSE, 8),
        ('dc_has_babtec',           'Babtec-Zugang anpassen?',         'Programme und Systeme', 'Muss der Babtec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?','babtec',         'boolean', FALSE, 9),
        ('dc_has_gewatec',          'Gewatec-Zugang anpassen?',        'Programme und Systeme', 'Muss der Gewatec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?','gewatec',       'boolean', FALSE, 10),
        ('dc_has_provis',           'Provis-Zugang anpassen?',         'Programme und Systeme', 'Muss der Provis-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?','berechtigungen', 'boolean', FALSE, 11),
        ('dc_has_consense',         'Spinfire-Zugang anpassen?',       'Programme und Systeme', 'Muss der Spinfire-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?','spinfire',     'boolean', FALSE, 12)
)
INSERT INTO workflow_answer_definitions (
    process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'department_change'),
    s.answer_key, s.title, s.category, s.description, s.icon_key, s.input_type, s.is_required, s.sort_order, TRUE
FROM answer_seed s
ON CONFLICT (answer_key) DO UPDATE
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
-- Aufgaben-Schlüssel beginnen alle mit dc_ um Konflikte mit anderen Prozesstypen zu vermeiden.
-- is_department_phase_task=TRUE für alle (kein Supervisor-Schritt beim Abteilungswechsel).
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
        ('dc_change_date_confirmed',    'Wechseldatum bestätigen',               'HR',              'Bestätigen, dass das Wechseldatum eingetroffen ist. Schaltet alle Zugangs- und Ausstattungsaufgaben frei.',   'identitat',      'HR', 'hr_onboarding', TRUE,  1,  10),
        ('dc_hr_system_update',         'Abteilung im HR-System aktualisieren',  'HR',              'Abteilung des Mitarbeiters in der Personalakte und im HR-System auf die neue Abteilung umstellen.',           'identitat',      'HR', 'hr_onboarding', TRUE,  3,  20),

        -- IT: Zugänge (abhängig von dc_change_date_confirmed)
        ('dc_ad_group_update',          'AD-Gruppen aktualisieren',              'Zugänge',         'AD-Gruppen und Berechtigungen auf die neue Abteilung umstellen, alte abteilungsspezifische Gruppen entfernen.','ad_user',        'IT', 'it_ad',          TRUE,  2,  100),
        ('dc_drive_access_update',      'Laufwerk-Zugänge anpassen',             'Zugänge',         'Netzlaufwerk-Zugriffsrechte anpassen: Zugriff auf neue Abteilungs-Laufwerke gewähren, alte entziehen.',       'pc',             'IT', 'it_ad',          TRUE,  2,  110),
        ('dc_email_alias_update',       'E-Mail Alias anpassen',                 'Zugänge',         'E-Mail Alias des Mitarbeiters aktualisieren, falls die neue Abteilung einen anderen Kürzel erfordert.',       'mailbox',        'IT', 'it_mailbox',     TRUE,  3,  120),
        ('dc_habel_access_update',      'Habel-Zugang anpassen',                 'Fachanwendungen', 'Habel-Berechtigungen auf die neue Abteilung umstellen.',                                                       'habel',          'IT', 'it_habel',       TRUE,  3,  130),
        ('dc_ln_access_update',         'InforLN-Zugang anpassen',               'Fachanwendungen', 'InforLN-Berechtigungen auf die neue Abteilung umstellen.',                                                     'react',          'IT', 'it_ln',          TRUE,  3,  140),

        -- IT: Hardware (abhängig von dc_change_date_confirmed)
        ('dc_hardware_swap',            'Hardware tauschen',                     'Ausstattung',     'Hardware der neuen Arbeitsanforderungen entsprechend tauschen (z. B. stationär durch Laptop ersetzen).',      'pc',             'IT', 'it_hardware',    TRUE,  2,  150),

        -- Fachabteilungen: Zugänge (abhängig von dc_change_date_confirmed)
        ('dc_babtec_access_update',     'Babtec-Zugang anpassen',                'Fachanwendungen', 'Babtec-Berechtigungen auf die neue Abteilung umstellen.',                                                      'babtec',         'QS', 'qs_babtec',      TRUE,  3,  200),
        ('dc_gewatec_access_update',    'Gewatec-Zugang anpassen',               'Fachanwendungen', 'Gewatec-Berechtigungen auf die neue Abteilung umstellen.',                                                     'gewatec',        'AV', 'av_gewatec',     TRUE,  3,  210),
        ('dc_provis_access_update',     'Provis-Zugang anpassen',                'Fachanwendungen', 'Provis-Berechtigungen auf die neue Abteilung umstellen.',                                                      'berechtigungen', 'AV', 'av_provis',      TRUE,  3,  220),
        ('dc_consense_access_update',   'Spinfire-Zugang anpassen',              'Fachanwendungen', 'Spinfire-Berechtigungen auf die neue Abteilung umstellen.',                                                    'spinfire',       'QMB','qmb_consense',   TRUE,  3,  230)
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
    (SELECT id FROM process_types WHERE key = 'department_change'),
    s.template_key,
    s.title,
    s.category,
    s.description,
    s.icon_key,
    d.id,
    r.id,
    NULL,   -- process_area_label: kein separater Bereich beim Abteilungswechsel
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
-- dc_change_date_confirmed und dc_hr_system_update werden IMMER generiert (keine Conditions).
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
        ('dc_ad_group_update',       1, 'dc_ad_group_change',     'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_drive_access_update',   1, 'dc_drive_access_change', 'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_email_alias_update',    1, 'dc_email_alias_change',  'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_hardware_swap',         1, 'dc_hardware_change',     'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_habel_access_update',   1, 'dc_has_habel',           'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_ln_access_update',      1, 'dc_has_ln',              'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_babtec_access_update',  1, 'dc_has_babtec',          'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_gewatec_access_update', 1, 'dc_has_gewatec',         'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_provis_access_update',  1, 'dc_has_provis',          'is_true', NULL::text, TRUE,  NULL::numeric),
        ('dc_consense_access_update',1, 'dc_has_consense',        'is_true', NULL::text, TRUE,  NULL::numeric)
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
-- dc_change_date_confirmed ist der Synchronisationspunkt:
--   Alle Zugangs-Anpassungs- und Ausstattungsaufgaben starten erst wenn
--   HR das Wechseldatum bestätigt hat.
-- dc_hr_system_update startet sofort (kein Blocker) — Personalakte kann vorab gepflegt werden.
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        -- Zugänge und Hardware erst ab Wechseldatum
        ('dc_ad_group_update',       'dc_change_date_confirmed', 'done'),
        ('dc_drive_access_update',   'dc_change_date_confirmed', 'done'),
        ('dc_email_alias_update',    'dc_change_date_confirmed', 'done'),
        ('dc_hardware_swap',         'dc_change_date_confirmed', 'done'),
        ('dc_habel_access_update',   'dc_change_date_confirmed', 'done'),
        ('dc_ln_access_update',      'dc_change_date_confirmed', 'done'),
        ('dc_babtec_access_update',  'dc_change_date_confirmed', 'done'),
        ('dc_gewatec_access_update', 'dc_change_date_confirmed', 'done'),
        ('dc_provis_access_update',  'dc_change_date_confirmed', 'done'),
        ('dc_consense_access_update','dc_change_date_confirmed', 'done')
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
