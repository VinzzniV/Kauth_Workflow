-- =========================
-- [F7.5.3] Namensaenderung: Prozesstyp, Requirements, Templates, Dependencies
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
    'name_change',
    'Namensaenderung',
    'Koordinierte Aktualisierung eines Mitarbeiternamens in Stammdaten, Verzeichnisdiensten und Kommunikationssystemen.',
    FALSE,
    NULL,
    TRUE,
    'identitat',
    FALSE,
    40
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
-- Alle nc_-Schluessel sind eindeutig und konfliktfrei zu anderen Prozesstypen.
-- HR fuellt diese beim Anlegen des Namensaenderungs-Vorgangs aus.
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order) AS (
    VALUES
        ('nc_new_first_name', 'Neuer Vorname',       'Namensaenderung', 'Neuer gueltiger Vorname der betroffenen Person.',          'identitat', 'text', TRUE, 1),
        ('nc_new_last_name',  'Neuer Nachname',      'Namensaenderung', 'Neuer gueltiger Nachname der betroffenen Person.',         'identitat', 'text', TRUE, 2),
        ('nc_effective_date', 'Wirksamkeitsdatum',   'Namensaenderung', 'Datum, ab dem der neue Name in allen Systemen gelten soll.','identitat', 'text', TRUE, 3)
)
INSERT INTO workflow_answer_definitions (
    process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
)
SELECT
    (SELECT id FROM process_types WHERE key = 'name_change'),
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
-- Aufgaben-Schluessel beginnen alle mit nc_ um Konflikte mit anderen Prozesstypen zu vermeiden.
-- is_department_phase_task=TRUE fuer alle (kein Supervisor-Schritt bei Namensaenderung).
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
        ('nc_effective_date_confirmed',   'Wirksamkeitsdatum bestaetigen',         'HR',       'Bestaetigen, dass das Wirksamkeitsdatum erreicht ist. Schaltet die technischen Umstellungsaufgaben frei.', 'identitat', 'HR', 'hr_onboarding', TRUE, 1, 10),
        ('nc_hr_master_data_update',      'HR-Stammdaten aktualisieren',           'HR',       'Neuen Namen in Personalakte und HR-Stammdaten pflegen.',                                                   'identitat', 'HR', 'hr_onboarding', TRUE, 2, 20),
        ('nc_ad_username_update',         'AD-Benutzername aktualisieren',         'Zugaenge', 'AD-Benutzername, Anzeigename und verzeichnisbezogene Namensfelder auf den neuen Namen umstellen.',      'ad_user',   'IT', 'it_ad',          TRUE, 2, 100),
        ('nc_mailbox_update',             'Mailbox und Alias aktualisieren',       'Zugaenge', 'Mailbox, primäre Adresse und Alias auf den neuen Namen umstellen.',                                        'mailbox',   'IT', 'it_mailbox',     TRUE, 2, 110),
        ('nc_system_display_name_update', 'Anzeigenamen in Systemen aktualisieren','Systeme',  'Anzeigenamen in angeschlossenen Systemen und Verzeichnissen auf den neuen Namen angleichen.',              'berechtigungen', 'IT', 'it_ad', TRUE, 3, 120)
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
    (SELECT id FROM process_types WHERE key = 'name_change'),
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
-- Task template dependencies
-- nc_effective_date_confirmed ist der Synchronisationspunkt:
--   Technische Umstellungen starten erst nach Erreichen des Wirksamkeitsdatums.
-- nc_hr_master_data_update kann sofort vorbereitet werden.
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        ('nc_ad_username_update',         'nc_effective_date_confirmed', 'done'),
        ('nc_mailbox_update',             'nc_effective_date_confirmed', 'done'),
        ('nc_system_display_name_update', 'nc_ad_username_update',       'done')
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
