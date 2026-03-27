INSERT INTO notification_email_settings (
    id,
    enabled,
    frontend_base_url,
    last_test_status
)
VALUES
    (1, FALSE, 'http://localhost:5173', 'never')
ON CONFLICT (id) DO NOTHING;

-- =========================
-- Process types
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
    'onboarding',
    'Onboarding',
    'Start eines neuen Mitarbeiters mit Aufgaben fuer HR, Fuehrungskraft und Fachbereiche.',
    TRUE,
    'supervisor_fills_document',
    FALSE,
    'identitat',
    TRUE,
    10
)
ON CONFLICT (key) DO UPDATE
SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    requires_supervisor_step = EXCLUDED.requires_supervisor_step,
    approval_task_template_key = EXCLUDED.approval_task_template_key,
    requires_target_person = EXCLUDED.requires_target_person,
    icon_key = EXCLUDED.icon_key,
    is_active = EXCLUDED.is_active,
    sort_order = EXCLUDED.sort_order;

-- =========================
-- Departments
-- =========================
INSERT INTO departments (name)
VALUES
    ('IT'),
    ('HR'),
    ('Engineering'),
    ('QS'),
    ('AV'),
    ('QMB'),
    ('Produktion'),
    ('Vertrieb'),
    ('Prototypenbau')
ON CONFLICT (name) DO NOTHING;

-- =========================
-- App roles
-- role_kind:
--   position       -> employee target role for onboarding request
--   system         -> authorization role for app access control
-- =========================
WITH role_seed(department_name, role_key, role_name, role_kind) AS (
    VALUES
        ('IT', 'position_it_administrator', 'IT-Administrator', 'position'),
        ('IT', 'position_it_support', 'IT-Support', 'position'),
        ('IT', 'position_developer', 'Entwickler', 'position'),
        ('AV', 'position_accountant', 'Sachbearbeitung AV', 'position'),
        ('AV', 'position_financial_controller', 'Leitung AV', 'position'),
        ('HR', 'position_hr_manager', 'HR-Manager', 'position'),
        ('HR', 'position_hr_assistant', 'HR-Assistenz', 'position'),
        ('Engineering', 'position_mechanical_engineer', 'Konstrukteur', 'position'),
        ('Engineering', 'position_production_engineer', 'Produktionsingenieur', 'position'),
        ('QS', 'position_quality_engineer', 'Qualitaetsingenieur', 'position'),
        ('QS', 'position_qa_analyst', 'QS-Analyst', 'position'),
        ('Produktion', 'position_production_operator', 'Mitarbeiter Produktion', 'position'),
        ('Vertrieb', 'position_sales_representative', 'Mitarbeiter Vertrieb', 'position'),
        ('Prototypenbau', 'position_prototype_builder', 'Mitarbeiter Prototypenbau', 'position'),

        (NULL, 'auth_hr', 'HR', 'system'),
        (NULL, 'auth_manager', 'Abteilungsleitung', 'system'),
        (NULL, 'auth_worker', 'Bearbeiter', 'system'),
        (NULL, 'auth_admin', 'Admin', 'system'),
        (NULL, 'auth_reader', 'Leser', 'system')
)
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
SELECT d.id, s.role_key, s.role_name, s.role_kind, TRUE
FROM role_seed s
LEFT JOIN departments d ON d.name = s.department_name
ON CONFLICT (role_key) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    name = EXCLUDED.name,
    role_kind = EXCLUDED.role_kind,
    is_active = EXCLUDED.is_active;

-- =========================
-- Fachliche Zustaendigkeiten
-- responsibility_type:
--   process         -> prozessbezogene Verantwortung
--   department_lead -> Fuehrungsverantwortung fuer eine Abteilung
--   application     -> fachliche System-/Themenverantwortung
-- =========================
WITH responsibility_seed(department_name, responsibility_key, system_key, responsibility_name, responsibility_type, description) AS (
    VALUES
        ('HR', 'hr_onboarding', NULL, 'HR-Onboarding', 'process', 'Verantwortung für Start, Abstimmung und Begleitung des Onboardings.'),
        ('IT', 'leadership_it', NULL, 'Abteilungsleitung IT', 'department_lead', 'Führungsverantwortung für Onboardings der IT.'),
        ('AV', 'leadership_av', NULL, 'Abteilungsleitung AV', 'department_lead', 'Führungsverantwortung für Onboardings der AV.'),
        ('HR', 'leadership_hr', NULL, 'Abteilungsleitung HR', 'department_lead', 'Führungsverantwortung für Onboardings der HR.'),
        ('QS', 'leadership_qs', NULL, 'Abteilungsleitung QS', 'department_lead', 'Führungsverantwortung für Onboardings der QS.'),
        ('QMB', 'leadership_qmb', NULL, 'Abteilungsleitung QMB', 'department_lead', 'Führungsverantwortung für Onboardings des QMB.'),
        ('Produktion', 'leadership_production', NULL, 'Abteilungsleitung Produktion', 'department_lead', 'Führungsverantwortung für Onboardings der Produktion.'),
        ('Vertrieb', 'leadership_sales', NULL, 'Abteilungsleitung Vertrieb', 'department_lead', 'Führungsverantwortung für Onboardings des Vertriebs.'),
        ('Prototypenbau', 'leadership_prototype', NULL, 'Abteilungsleitung Prototypenbau', 'department_lead', 'Führungsverantwortung für Onboardings im Prototypenbau.'),
        ('IT', 'it_ad', 'ad', 'IT - AD', 'application', 'Verantwortung für AD-Konto und zentrale Berechtigungen.'),
        ('IT', 'it_mailbox', 'mailbox', 'IT - Mailbox', 'application', 'Verantwortung für Mailbox-Einrichtung.'),
        ('IT', 'it_habel', 'habel', 'IT - Habel', 'application', 'Verantwortung für Habel-Zugänge.'),
        ('IT', 'it_ln', 'ln', 'IT - LN', 'application', 'Verantwortung für LN-Zugänge.'),
        ('IT', 'it_hardware', 'hardware', 'IT - Hardware', 'application', 'Verantwortung für Hardware-Bereitstellung und Einrichtung.'),
        ('QS', 'qs_babtec', 'babtec', 'QS - Babtec', 'application', 'Verantwortung für Babtec in der QS.'),
        ('AV', 'av_gewatec', 'gewatec', 'AV - Gewatec', 'application', 'Verantwortung für Gewatec in der AV.'),
        ('AV', 'av_provis', 'provis', 'AV - Provis', 'application', 'Verantwortung für Provis in der AV.'),
        ('QMB', 'qmb_consense', 'consense', 'QMB - Consense', 'application', 'Verantwortung für Consense im QMB.')
)
INSERT INTO app_responsibilities (department_id, responsibility_key, system_key, name, responsibility_type, description, is_active)
SELECT d.id, s.responsibility_key, s.system_key, s.responsibility_name, s.responsibility_type, s.description, TRUE
FROM responsibility_seed s
LEFT JOIN departments d ON d.name = s.department_name
ON CONFLICT (responsibility_key) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    system_key = EXCLUDED.system_key,
    name = EXCLUDED.name,
    responsibility_type = EXCLUDED.responsibility_type,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

-- =========================
-- App users
-- =========================
WITH user_seed(department_name, external_key, display_name, email, notification_email) AS (
    VALUES
        ('AV', 'michael.beyerle', 'Michael Beyerle', 'michael.beyerle@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('HR', 'ilona.gebhard', 'Ilona Gebhard', 'ilona.gebhard@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('IT', 'tobias.lueck', 'Tobias Lueck', 'tobias.lueck@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('QMB', 'melanie.messinger', 'Melanie Messinger', 'melanie.messinger@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('QS', 'armin.hornig', 'Armin Hornig', 'armin.hornig@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('HR', 'laura.romankewicz', 'Laura Romankewicz', 'laura.romankewicz@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('AV', 'thomas.hauck', 'Thomas Hauck', 'thomas.hauck@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('IT', 'vinzent.niederwieser', 'Vinzent Niederwieser', 'vinzent.niederwieser@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('IT', 'mustafa.avci', 'Mustafa Avci', 'mustafa.avci@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('Produktion', 'sergej.hoffmann', 'Sergej Hoffmann', 'sergej.hoffmann@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('Vertrieb', 'benjamin.thieringer', 'Benjamin Thieringer', 'benjamin.thieringer@demo.local', 'vinzent.niederwieser@kauth.de'),
        ('Prototypenbau', 'mahir.yuceyurt', 'Mahir Yuceyurt', 'mahir.yuceyurt@demo.local', 'vinzent.niederwieser@kauth.de'),
        (NULL, 'admin.demo', 'Admin Demo', 'admin.demo@demo.local', 'vinzent.niederwieser@kauth.de'),
        (NULL, 'superuser.demo', 'Superuser Demo', 'superuser.demo@demo.local', 'vinzent.niederwieser@kauth.de')
)
INSERT INTO app_users (department_id, external_key, display_name, email, notification_email, is_active)
SELECT d.id, s.external_key, s.display_name, s.email, s.notification_email, TRUE
FROM user_seed s
LEFT JOIN departments d ON d.name = s.department_name
ON CONFLICT (email) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    external_key = EXCLUDED.external_key,
    display_name = EXCLUDED.display_name,
    notification_email = EXCLUDED.notification_email,
    is_active = EXCLUDED.is_active;

-- Demo-Sonderkonten werden bei jedem Seed-Lauf explizit auf den gewuenschten Stand zurueckgesetzt.
DELETE FROM app_user_roles
WHERE app_user_id IN (
    SELECT id
    FROM app_users
    WHERE external_key IN ('admin.demo', 'superuser.demo')
);

DELETE FROM app_user_responsibilities
WHERE app_user_id IN (
    SELECT id
    FROM app_users
    WHERE external_key IN ('admin.demo', 'superuser.demo')
);

DELETE FROM app_user_groups
WHERE app_user_id IN (
    SELECT id
    FROM app_users
    WHERE external_key IN ('admin.demo', 'superuser.demo')
);

-- =========================
-- People master data
-- =========================
INSERT INTO people (app_user_id, department_id, updated_at)
SELECT
    u.id,
    u.department_id,
    NOW()
FROM app_users u
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    updated_at = NOW();

-- =========================
-- App user role assignments
-- =========================
WITH user_role_seed(email, role_key) AS (
    VALUES
        ('michael.beyerle@demo.local', 'auth_manager'),
        ('ilona.gebhard@demo.local', 'auth_manager'),
        ('tobias.lueck@demo.local', 'auth_manager'),
        ('melanie.messinger@demo.local', 'auth_manager'),
        ('melanie.messinger@demo.local', 'auth_worker'),
        ('armin.hornig@demo.local', 'auth_manager'),
        ('armin.hornig@demo.local', 'auth_worker'),
        ('laura.romankewicz@demo.local', 'auth_hr'),
        ('thomas.hauck@demo.local', 'auth_worker'),
        ('vinzent.niederwieser@demo.local', 'auth_worker'),
        ('mustafa.avci@demo.local', 'auth_worker'),
        ('sergej.hoffmann@demo.local', 'auth_manager'),
        ('benjamin.thieringer@demo.local', 'auth_manager'),
        ('mahir.yuceyurt@demo.local', 'auth_manager'),

        ('admin.demo@demo.local', 'auth_admin'),
        ('superuser.demo@demo.local', 'auth_admin')
)
INSERT INTO app_user_roles (app_user_id, app_role_id)
SELECT u.id, r.id
FROM user_role_seed s
JOIN app_users u ON u.email = s.email
JOIN app_roles r ON r.role_key = s.role_key
ON CONFLICT (app_user_id, app_role_id) DO NOTHING;

-- =========================
-- App user responsibility assignments
-- =========================
WITH user_responsibility_seed(email, responsibility_key) AS (
    VALUES
        ('michael.beyerle@demo.local', 'leadership_av'),
        ('ilona.gebhard@demo.local', 'leadership_hr'),
        ('tobias.lueck@demo.local', 'leadership_it'),
        ('melanie.messinger@demo.local', 'leadership_qmb'),
        ('melanie.messinger@demo.local', 'qmb_consense'),
        ('armin.hornig@demo.local', 'leadership_qs'),
        ('armin.hornig@demo.local', 'qs_babtec'),
        ('laura.romankewicz@demo.local', 'hr_onboarding'),
        ('thomas.hauck@demo.local', 'av_gewatec'),
        ('thomas.hauck@demo.local', 'av_provis'),
        ('vinzent.niederwieser@demo.local', 'it_ad'),
        ('vinzent.niederwieser@demo.local', 'it_mailbox'),
        ('vinzent.niederwieser@demo.local', 'it_habel'),
        ('vinzent.niederwieser@demo.local', 'it_hardware'),
        ('mustafa.avci@demo.local', 'it_ln'),
        ('sergej.hoffmann@demo.local', 'leadership_production'),
        ('benjamin.thieringer@demo.local', 'leadership_sales'),
        ('mahir.yuceyurt@demo.local', 'leadership_prototype')
)
INSERT INTO app_user_responsibilities (app_user_id, app_responsibility_id)
SELECT u.id, r.id
FROM user_responsibility_seed s
JOIN app_users u ON u.email = s.email
JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
ON CONFLICT (app_user_id, app_responsibility_id) DO NOTHING;

-- =========================
-- Pflegebare Abteilungs-Zuordnungen fuer Onboarding
-- department_settings:
--   requirement_approver_person_id -> zustaendig fuer Auswahl und Rueckmeldung der Anforderungen
--   department_lead_person_id      -> fachliche Fuehrungsverantwortung als Fallback
-- =========================
WITH department_assignment_seed(department_name, lead_email, requirement_owner_email) AS (
    VALUES
        ('IT', 'tobias.lueck@demo.local', 'tobias.lueck@demo.local'),
        ('HR', 'ilona.gebhard@demo.local', 'ilona.gebhard@demo.local'),
        ('QS', 'armin.hornig@demo.local', 'armin.hornig@demo.local'),
        ('AV', 'michael.beyerle@demo.local', 'michael.beyerle@demo.local'),
        ('QMB', 'melanie.messinger@demo.local', 'melanie.messinger@demo.local'),
        ('Produktion', 'sergej.hoffmann@demo.local', 'sergej.hoffmann@demo.local'),
        ('Vertrieb', 'benjamin.thieringer@demo.local', 'benjamin.thieringer@demo.local'),
        ('Prototypenbau', 'mahir.yuceyurt@demo.local', 'mahir.yuceyurt@demo.local')
)
INSERT INTO department_settings (
    department_id,
    department_lead_person_id,
    requirement_approver_person_id,
    updated_at
)
SELECT
    d.id,
    lead_person.id,
    COALESCE(requirement_person.id, lead_person.id),
    NOW()
FROM department_assignment_seed s
JOIN departments d ON d.name = s.department_name
LEFT JOIN app_users lead_user ON lead_user.email = s.lead_email
LEFT JOIN app_users requirement_user ON requirement_user.email = s.requirement_owner_email
LEFT JOIN people lead_person ON lead_person.app_user_id = lead_user.id
LEFT JOIN people requirement_person ON requirement_person.app_user_id = requirement_user.id
ON CONFLICT (department_id) DO UPDATE
SET
    department_lead_person_id = EXCLUDED.department_lead_person_id,
    requirement_approver_person_id = EXCLUDED.requirement_approver_person_id,
    updated_at = NOW();

-- =========================
-- Pflegebare Verantwortliche je System
-- =========================
WITH responsibility_owner_seed(responsibility_key, department_name, email) AS (
    VALUES
        ('it_ad', 'IT', 'vinzent.niederwieser@demo.local'),
        ('it_mailbox', 'IT', 'vinzent.niederwieser@demo.local'),
        ('it_habel', 'IT', 'vinzent.niederwieser@demo.local'),
        ('it_ln', 'IT', 'mustafa.avci@demo.local'),
        ('it_hardware', 'IT', 'vinzent.niederwieser@demo.local'),
        ('qs_babtec', 'QS', 'armin.hornig@demo.local'),
        ('av_gewatec', 'AV', 'thomas.hauck@demo.local'),
        ('av_provis', 'AV', 'thomas.hauck@demo.local'),
        ('qmb_consense', 'QMB', 'melanie.messinger@demo.local')
)
INSERT INTO system_responsibilities (
    system_key,
    app_responsibility_id,
    responsible_person_id,
    responsible_department_id,
    updated_at
)
SELECT
    r.system_key,
    r.id,
    p.id,
    d.id,
    NOW()
FROM responsibility_owner_seed s
JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
JOIN departments d ON d.name = s.department_name
JOIN app_users u ON u.email = s.email
JOIN people p ON p.app_user_id = u.id
WHERE r.system_key IS NOT NULL
ON CONFLICT (system_key) DO UPDATE
SET
    app_responsibility_id = EXCLUDED.app_responsibility_id,
    responsible_person_id = EXCLUDED.responsible_person_id,
    responsible_department_id = EXCLUDED.responsible_department_id,
    updated_at = NOW();

-- =========================
-- App groups
-- =========================
WITH group_seed(group_key, name, description) AS (
    VALUES
        ('hr_team', 'HR-Team', 'HR Onboarding und Personalprozess'),
        ('engineering_leads', 'Engineering-Leads', 'Führungskräfte und Freigaben'),
        ('it_onboarding', 'IT-Onboarding', 'IT Umsetzungsteam'),
        ('qs_team', 'QS-Team', 'QS Prozessbeteiligte'),
        ('av_team', 'AV-Team', 'AV Prozessbeteiligte'),
        ('qmb_team', 'QMB-Team', 'QMB Prozessbeteiligte')
)
INSERT INTO app_groups (group_key, name, description, is_active)
SELECT s.group_key, s.name, s.description, TRUE
FROM group_seed s
ON CONFLICT (group_key) DO UPDATE
SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

-- =========================
-- App group to role assignments (optional but enabled)
-- =========================
WITH group_role_seed(group_key, role_key) AS (
    VALUES
        ('hr_team', 'auth_hr'),

        ('engineering_leads', 'auth_manager'),

        ('it_onboarding', 'auth_worker'),

        ('qs_team', 'auth_worker'),

        ('av_team', 'auth_worker'),

        ('qmb_team', 'auth_worker')
)
INSERT INTO app_group_roles (app_group_id, app_role_id)
SELECT g.id, r.id
FROM group_role_seed s
JOIN app_groups g ON g.group_key = s.group_key
JOIN app_roles r ON r.role_key = s.role_key
ON CONFLICT (app_group_id, app_role_id) DO NOTHING;

-- =========================
-- App group to responsibility assignments
-- =========================
-- Gruppen bleiben fuer Rollen nutzbar, fachliche IT-Responsibilities werden aber nicht mehr breit vererbt.
DELETE FROM app_group_responsibilities gr
USING app_groups g, app_responsibilities r
WHERE gr.app_group_id = g.id
  AND gr.app_responsibility_id = r.id
  AND g.group_key = 'it_onboarding'
  AND r.responsibility_key IN ('it_ad', 'it_mailbox', 'it_habel', 'it_ln', 'it_hardware');

WITH group_responsibility_seed(group_key, responsibility_key) AS (
    VALUES
        ('hr_team', 'hr_onboarding'),
        ('qs_team', 'qs_babtec'),
        ('av_team', 'av_gewatec'),
        ('av_team', 'av_provis'),
        ('qmb_team', 'qmb_consense')
)
INSERT INTO app_group_responsibilities (app_group_id, app_responsibility_id)
SELECT g.id, r.id
FROM group_responsibility_seed s
JOIN app_groups g ON g.group_key = s.group_key
JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
ON CONFLICT (app_group_id, app_responsibility_id) DO NOTHING;

-- =========================
-- App user to group assignments
-- =========================
WITH user_group_seed(external_key, group_key) AS (
    VALUES
        ('laura.romankewicz', 'hr_team'),
        ('vinzent.niederwieser', 'it_onboarding'),
        ('mustafa.avci', 'it_onboarding'),
        ('armin.hornig', 'qs_team'),
        ('thomas.hauck', 'av_team'),
        ('melanie.messinger', 'qmb_team')
)
INSERT INTO app_user_groups (app_user_id, app_group_id)
SELECT u.id, g.id
FROM user_group_seed s
JOIN app_users u ON u.external_key = s.external_key
JOIN app_groups g ON g.group_key = s.group_key
ON CONFLICT (app_user_id, app_group_id) DO NOTHING;

-- =========================
-- Workflow answer definitions (variables/questions)
-- =========================
WITH answer_seed(answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active) AS (
    VALUES
        ('ad_user_requested', 'AD-Konto', 'Zugänge', 'Soll für die neue Person ein AD-Konto eingerichtet werden?', 'ad_user', 'boolean', TRUE, 1, TRUE),
        ('comparison_user_available', 'Vergleichsuser vorhanden?', 'Zugänge', 'Gibt es eine Vergleichsperson für die Übernahme der AD-Berechtigungen?', 'berechtigungen', 'boolean', FALSE, 2, TRUE),
        ('comparison_user_name', 'Referenzuser', 'Zugänge', 'Welcher Referenzuser soll für die Übernahme der AD-Berechtigungen verwendet werden?', 'berechtigungen', 'text', FALSE, 3, TRUE),
        ('mailbox_requested', 'Mailbox', 'Zugänge', 'Soll optional eine Mailbox für die neue Person eingerichtet werden?', 'mailbox', 'boolean', FALSE, 4, TRUE),
        ('internet_requested', 'Internetzugang', 'Zugänge', 'Wird für die neue Person ein Internetzugang benötigt?', 'internetzugang', 'boolean', FALSE, 5, TRUE),
        ('microsoft_office_requested', 'Microsoft Office', 'Programme und Systeme', 'Soll Microsoft Office für die neue Person bereitgestellt werden?', 'microsoft_office', 'boolean', FALSE, 6, TRUE),
        ('habel_user_requested', 'Habel', 'Programme und Systeme', 'Soll ein Habel-User für die neue Person angelegt werden?', 'habel', 'boolean', FALSE, 7, TRUE),
        ('ln_user_requested', 'InforLN', 'Programme und Systeme', 'Soll ein InforLN-User für die neue Person angelegt werden?', 'inforln', 'boolean', FALSE, 8, TRUE),
        ('hardware_requested', 'Hardware benötigt?', 'Ausstattung', 'Wird für die neue Person überhaupt Hardware benötigt?', 'pc', 'boolean', TRUE, 9, TRUE),
        ('hardware_available', 'Hardware vorhanden?', 'Ausstattung', 'Ist für die neue Person bereits passende Hardware vorhanden?', 'pc', 'boolean', FALSE, 10, TRUE),
        ('phone_requested', 'Tragbares Telefon', 'Ausstattung', 'Wird für die neue Person ein tragbares Telefon benötigt?', 'phone', 'boolean', FALSE, 13, TRUE),
        ('hardware_type', 'Hardware', 'Ausstattung', 'Welche Hardware soll bereitgestellt werden?', 'pc', 'select', FALSE, 11, TRUE),
        ('laptop_vpn_type', 'Laptop', 'Ausstattung', 'Soll der Laptop mit VPN oder ohne VPN bereitgestellt werden?', 'vpn', 'select', FALSE, 12, TRUE),
        ('laptop_with_vpn_requested', 'Laptop mit VPN', 'Ausstattung', 'Legacy-Feld für bisherige Laptop-Auswahl mit VPN.', 'vpn', 'boolean', FALSE, 111, FALSE),
        ('laptop_without_vpn_requested', 'Laptop ohne VPN', 'Ausstattung', 'Legacy-Feld für bisherige Laptop-Auswahl ohne VPN.', 'laptop', 'boolean', FALSE, 112, FALSE),
        ('desktop_pc_requested', 'Rechner fest', 'Ausstattung', 'Legacy-Feld für bisherige Auswahl eines festen Rechners.', 'pc', 'boolean', FALSE, 113, FALSE),
        ('babtec_requested', 'Babtec', 'Programme und Systeme', 'Soll ein User in Babtec für die neue Person angelegt werden?', 'babtec', 'boolean', FALSE, 14, TRUE),
        ('catia_requested', 'Catia', 'Programme und Systeme', 'Soll Catia für die neue Person bereitgestellt werden?', 'catia', 'boolean', FALSE, 15, TRUE),
        ('datev_requested', 'DATEV', 'Programme und Systeme', 'Soll DATEV für die neue Person bereitgestellt werden?', 'datev', 'boolean', FALSE, 16, TRUE),
        ('tiso_requested', 'Tisoware', 'Programme und Systeme', 'Soll Tisoware für die neue Person bereitgestellt werden?', 'tiso', 'boolean', FALSE, 17, TRUE),
        ('gewatec_requested', 'Gewatec', 'Programme und Systeme', 'Soll ein Gewatec-User für die neue Person angelegt werden?', 'gewatec', 'boolean', FALSE, 18, TRUE),
        ('provis_requested', 'Provis', 'Programme und Systeme', 'Soll ein Provis-User für die neue Person angelegt werden?', 'provis', 'boolean', FALSE, 19, TRUE),
        ('consense_requested', 'Consense-User anlegen?', 'Programme und Systeme', 'Soll fuer die neue Person ein Consense-User angelegt werden?', 'consense', 'boolean', FALSE, 20, TRUE),
        ('internal_drive_access_requested', 'Zugangsrechte internes Laufwerk', 'Zugangsrechte', 'Sollen Zugangsrechte für ein internes Laufwerk vergeben werden?', 'berechtigungen', 'boolean', FALSE, 21, TRUE),
        ('internal_drive_access_roles', 'Funktion für Laufwerksrechte', 'Zugangsrechte', 'Welche Funktion soll für die Laufwerksrechte berücksichtigt werden?', 'berechtigungen', 'multi_select', FALSE, 22, TRUE),
        ('special_notes', 'Besondere Hinweise', 'Dokumentation', 'Freitext für wichtige Hinweise im Onboarding.', 'identitat', 'text', FALSE, 92, FALSE)
)
INSERT INTO workflow_answer_definitions (process_type_id, answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active)
SELECT (SELECT id FROM process_types WHERE key = 'onboarding'), answer_key, title, category, description, icon_key, input_type, is_required, sort_order, is_active
FROM answer_seed
ON CONFLICT (process_type_id, answer_key) DO UPDATE
SET
    process_type_id = EXCLUDED.process_type_id,
    title = EXCLUDED.title,
    category = EXCLUDED.category,
    description = EXCLUDED.description,
    icon_key = EXCLUDED.icon_key,
    input_type = EXCLUDED.input_type,
    is_required = EXCLUDED.is_required,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

-- =========================
-- Answer options
-- =========================
WITH option_seed(answer_key, option_key, option_value, option_label, sort_order) AS (
    VALUES
        ('hardware_type', 'laptop', 'laptop', 'Laptop', 1),
        ('hardware_type', 'workstation', 'workstation', 'Workstation', 2),
        ('laptop_vpn_type', 'with_vpn', 'with_vpn', 'Mit VPN', 1),
        ('laptop_vpn_type', 'without_vpn', 'without_vpn', 'Ohne VPN', 2),
        ('internal_drive_access_roles', 'leitung', 'leitung', 'Leitung', 1),
        ('internal_drive_access_roles', 'bereichsleitung', 'bereichsleitung', 'Bereichsleitung', 2),
        ('internal_drive_access_roles', 'abteilungsleitung', 'abteilungsleitung', 'Abtlg. Ltg.', 3),
        ('internal_drive_access_roles', 'stellvertretende_abteilungsleitung', 'stellvertretende_abteilungsleitung', 'stv. Abtlg.', 4)
)
INSERT INTO workflow_answer_options (answer_definition_id, option_key, option_value, option_label, sort_order)
SELECT d.id, s.option_key, s.option_value, s.option_label, s.sort_order
FROM option_seed s
JOIN workflow_answer_definitions d ON d.answer_key = s.answer_key
ON CONFLICT (answer_definition_id, option_key) DO UPDATE
SET
    option_value = EXCLUDED.option_value,
    option_label = EXCLUDED.option_label,
    sort_order = EXCLUDED.sort_order;

-- =========================
-- Requirement behavior rules
-- =========================
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

-- =========================
-- Role recommendation/default values for variables
-- =========================
WITH default_seed(role_key, answer_key, is_recommended, is_default, default_value_boolean, default_value_text, default_value_number, sort_order) AS (
    VALUES
        -- Baseline for all position roles (explicit rows for readability and deterministic seeds)
        ('position_it_administrator', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_it_administrator', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_it_administrator', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),

        ('position_it_support', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_it_support', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_it_support', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),

        ('position_developer', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_developer', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_developer', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),

        ('position_accountant', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_accountant', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_accountant', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_accountant', 'habel_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),
        ('position_accountant', 'provis_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 5),

        ('position_financial_controller', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_financial_controller', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_financial_controller', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_financial_controller', 'habel_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),
        ('position_financial_controller', 'provis_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 5),

        ('position_hr_manager', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_hr_manager', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_hr_manager', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),

        ('position_hr_assistant', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_hr_assistant', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_hr_assistant', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),

        ('position_mechanical_engineer', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_mechanical_engineer', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_mechanical_engineer', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_mechanical_engineer', 'gewatec_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),

        ('position_production_engineer', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_production_engineer', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_production_engineer', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_production_engineer', 'ln_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),
        ('position_production_engineer', 'gewatec_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 5),

        ('position_quality_engineer', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_quality_engineer', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_quality_engineer', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_quality_engineer', 'babtec_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),
        ('position_quality_engineer', 'microsoft_office_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 5),
        ('position_quality_engineer', 'tiso_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 6),
        ('position_quality_engineer', 'consense_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 7),

        ('position_qa_analyst', 'ad_user_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 1),
        ('position_qa_analyst', 'mailbox_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 2),
        ('position_qa_analyst', 'hardware_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 3),
        ('position_qa_analyst', 'babtec_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 4),
        ('position_qa_analyst', 'microsoft_office_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 5),
        ('position_qa_analyst', 'tiso_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 6),
        ('position_qa_analyst', 'consense_requested', TRUE, TRUE, TRUE, NULL::text, NULL::numeric, 7)
)
INSERT INTO app_role_answer_defaults (
    process_type_id,
    app_role_id,
    answer_definition_id,
    is_recommended,
    is_default,
    default_value_boolean,
    default_value_text,
    default_value_number,
    sort_order
)
SELECT
    (SELECT id FROM process_types WHERE key = 'onboarding'),
    r.id,
    d.id,
    s.is_recommended,
    s.is_default,
    s.default_value_boolean,
    s.default_value_text,
    s.default_value_number,
    s.sort_order
FROM default_seed s
JOIN app_roles r ON r.role_key = s.role_key
JOIN workflow_answer_definitions d ON d.answer_key = s.answer_key
ON CONFLICT (app_role_id, answer_definition_id) DO UPDATE
SET
    process_type_id = EXCLUDED.process_type_id,
    is_recommended = EXCLUDED.is_recommended,
    is_default = EXCLUDED.is_default,
    default_value_boolean = EXCLUDED.default_value_boolean,
    default_value_text = EXCLUDED.default_value_text,
    default_value_number = EXCLUDED.default_value_number,
    sort_order = EXCLUDED.sort_order;

-- =========================
-- Role default select options
-- =========================
WITH option_default_seed(role_key, answer_key, option_key) AS (
    VALUES
        ('position_it_administrator', 'hardware_type', 'laptop'),
        ('position_it_support', 'hardware_type', 'laptop'),
        ('position_developer', 'hardware_type', 'laptop'),
        ('position_accountant', 'hardware_type', 'laptop'),
        ('position_financial_controller', 'hardware_type', 'laptop'),
        ('position_hr_manager', 'hardware_type', 'laptop'),
        ('position_hr_assistant', 'hardware_type', 'laptop'),
        ('position_mechanical_engineer', 'hardware_type', 'workstation'),
        ('position_production_engineer', 'hardware_type', 'workstation'),
        ('position_quality_engineer', 'hardware_type', 'laptop'),
        ('position_qa_analyst', 'hardware_type', 'laptop')
)
INSERT INTO app_role_answer_default_options (app_role_id, answer_definition_id, answer_option_id, is_default)
SELECT
    r.id,
    d.id,
    o.id,
    TRUE
FROM option_default_seed s
JOIN app_roles r ON r.role_key = s.role_key
JOIN workflow_answer_definitions d ON d.answer_key = s.answer_key
JOIN workflow_answer_options o ON o.answer_definition_id = d.id AND o.option_key = s.option_key
JOIN app_role_answer_defaults ard ON ard.app_role_id = r.id AND ard.answer_definition_id = d.id
ON CONFLICT (app_role_id, answer_definition_id, answer_option_id) DO UPDATE
SET is_default = EXCLUDED.is_default;

-- =========================
-- Aufgaben-Templates
-- =========================
WITH template_seed(
    template_key,
    title,
    category,
    description,
    icon_key,
    owning_department_name,
    responsibility_key,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order
) AS (
    VALUES
        ('supervisor_fills_document', 'Anforderungen auswählen und bestätigen', 'Führungskraft', 'Die Abteilungsleitung wählt die benötigten Anforderungen aus und bestätigt diese.', 'identitat', NULL, NULL, 'Abteilungsleitung', FALSE, TRUE, 2, 40),

        ('ad_user_create', 'AD-User anlegen', 'Zugänge', 'AD-User für die neue Person anlegen.', 'ad_user', 'IT', 'it_ad', NULL, TRUE, TRUE, 3, 100),
        ('permissions_from_reference_user', 'AD-Berechtigungen anhand Vergleichsuser übernehmen', 'Zugänge', 'AD-Berechtigungen anhand einer Vergleichsperson übernehmen.', 'berechtigungen', 'IT', 'it_ad', NULL, TRUE, TRUE, 3, 110),
        ('exchange_create', 'Mailbox anlegen', 'Zugänge', 'Mailbox für die neue Person anlegen.', 'mailbox', 'IT', 'it_mailbox', NULL, TRUE, TRUE, 3, 120),
        ('habel_user_create', 'Habel-User anlegen', 'Fachanwendungen', 'Habel-User für die neue Person anlegen.', 'habel', 'IT', 'it_habel', NULL, TRUE, TRUE, 3, 130),
        ('ln_user_create', 'LN-User anlegen', 'Fachanwendungen', 'LN-User für die neue Person anlegen.', 'react', 'IT', 'it_ln', NULL, TRUE, TRUE, 3, 140),
        ('internet_access_enable', 'Internetzugang einrichten', 'Zugänge', 'Internetzugang für die neue Person freischalten.', 'internetzugang', 'IT', 'it_ad', NULL, TRUE, TRUE, 3, 145),
        ('internal_drive_access_grant', 'Laufwerksrechte vergeben', 'Zugänge', 'Zugriffsrechte für das interne Laufwerk der neuen Person einrichten.', 'berechtigungen', 'IT', 'it_ad', NULL, TRUE, TRUE, 3, 147),
        ('office_install', 'Microsoft Office bereitstellen', 'Fachanwendungen', 'Microsoft Office für die neue Person bereitstellen und konfigurieren.', 'microsoft_office', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 148),

        ('hardware_procure', 'Hardware beschaffen', 'Ausstattung', 'Hardware-Bedarf prüfen und bei Bedarf passende Hardware beschaffen.', 'pc', 'IT', 'it_hardware', NULL, TRUE, TRUE, 5, 150),
        ('hardware_setup', 'Hardware einrichten', 'Ausstattung', 'Hardware installieren und für den Einsatz vorbereiten.', 'pc', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 160),
        ('hardware_handover', 'Hardware bereitstellen', 'Ausstattung', 'Eingerichtete Hardware für die neue Person bereitstellen.', 'pc', 'IT', 'it_hardware', NULL, TRUE, TRUE, 1, 170),
        ('phone_prepare', 'Tragbares Telefon bereitstellen', 'Ausstattung', 'Tragbares Telefon für die neue Person bereitstellen.', 'phone', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 175),
        ('catia_install', 'Catia bereitstellen', 'Fachanwendungen', 'Catia für die neue Person installieren und bereitstellen.', 'catia', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 180),
        ('datev_install', 'DATEV bereitstellen', 'Fachanwendungen', 'DATEV für die neue Person installieren und bereitstellen.', 'datev', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 185),
        ('tisoware_install', 'Tisoware bereitstellen', 'Fachanwendungen', 'Tisoware für die neue Person installieren und bereitstellen.', 'tiso', 'IT', 'it_hardware', NULL, TRUE, TRUE, 3, 190),

        ('babtec_user_create', 'Babtec-User anlegen', 'Fachanwendungen', 'User in Babtec für die neue Person anlegen.', 'babtec', 'QS', 'qs_babtec', NULL, TRUE, TRUE, 3, 200),
        ('gewatec_user_create', 'Gewatec-User anlegen', 'Fachanwendungen', 'Gewatec-User für die neue Person anlegen.', 'berechtigungen', 'AV', 'av_gewatec', NULL, TRUE, TRUE, 3, 210),
        ('provis_user_create', 'Provis-User anlegen', 'Fachanwendungen', 'Provis-User für die neue Person anlegen.', 'berechtigungen', 'AV', 'av_provis', NULL, TRUE, TRUE, 3, 220),
        ('consense_setup', 'Consense User anlegen', 'Fachanwendungen', 'Consense-User fuer die neue Person anlegen.', 'consense', 'QMB', 'qmb_consense', NULL, TRUE, TRUE, 3, 230)
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
    (SELECT id FROM process_types WHERE key = 'onboarding'),
    s.template_key,
    s.title,
    s.category,
    s.description,
    s.icon_key,
    d.id,
    r.id,
    s.process_area_label,
    s.is_department_phase_task,
    s.is_required,
    s.due_in_days,
    s.sort_order,
    TRUE
FROM template_seed s
LEFT JOIN departments d ON d.name = s.owning_department_name
LEFT JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
ON CONFLICT (template_key) DO UPDATE
SET
    process_type_id = EXCLUDED.process_type_id,
    title = EXCLUDED.title,
    category = EXCLUDED.category,
    description = EXCLUDED.description,
    icon_key = EXCLUDED.icon_key,
    owning_department_id = EXCLUDED.owning_department_id,
    default_responsibility_id = EXCLUDED.default_responsibility_id,
    process_area_label = EXCLUDED.process_area_label,
    is_department_phase_task = EXCLUDED.is_department_phase_task,
    is_required = EXCLUDED.is_required,
    due_in_days = EXCLUDED.due_in_days,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

-- =========================
-- Task generation conditions (rule-based)
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
        ('ad_user_create', 1, 'ad_user_requested', 'is_true', NULL::text, TRUE, NULL::numeric),

        ('permissions_from_reference_user', 1, 'ad_user_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('permissions_from_reference_user', 1, 'comparison_user_available', 'is_true', NULL::text, TRUE, NULL::numeric),

        ('exchange_create', 1, 'mailbox_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('habel_user_create', 1, 'habel_user_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('ln_user_create', 1, 'ln_user_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('internet_access_enable', 1, 'internet_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('internal_drive_access_grant', 1, 'internal_drive_access_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('office_install', 1, 'microsoft_office_requested', 'is_true', NULL::text, TRUE, NULL::numeric),

        ('hardware_procure', 1, 'hardware_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('hardware_procure', 1, 'hardware_available', 'is_false', NULL::text, FALSE, NULL::numeric),
        ('hardware_setup', 1, 'hardware_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('hardware_handover', 1, 'hardware_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('phone_prepare', 1, 'phone_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('catia_install', 1, 'catia_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('datev_install', 1, 'datev_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('tisoware_install', 1, 'tiso_requested', 'is_true', NULL::text, TRUE, NULL::numeric),

        ('babtec_user_create', 1, 'babtec_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('gewatec_user_create', 1, 'gewatec_requested', 'is_true', NULL::text, TRUE, NULL::numeric),
        ('provis_user_create', 1, 'provis_requested', 'is_true', NULL::text, TRUE, NULL::numeric),

        ('consense_setup', 1, 'consense_requested', 'is_true', NULL::text, TRUE, NULL::numeric)
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
    expected_value_text = EXCLUDED.expected_value_text,
    expected_value_boolean = EXCLUDED.expected_value_boolean,
    expected_value_number = EXCLUDED.expected_value_number;

-- =========================
-- Task template dependencies
-- =========================
WITH dependency_seed(task_key, depends_on_task_key, required_status) AS (
    VALUES
        ('ad_user_create', 'supervisor_fills_document', 'done'),
        ('permissions_from_reference_user', 'ad_user_create', 'done'),
        ('exchange_create', 'ad_user_create', 'done'),
        ('habel_user_create', 'supervisor_fills_document', 'done'),
        ('ln_user_create', 'supervisor_fills_document', 'done'),
        ('internet_access_enable', 'supervisor_fills_document', 'done'),
        ('internal_drive_access_grant', 'supervisor_fills_document', 'done'),
        ('office_install', 'supervisor_fills_document', 'done'),

        ('hardware_procure', 'supervisor_fills_document', 'done'),
        ('hardware_setup', 'supervisor_fills_document', 'done'),
        ('hardware_setup', 'hardware_procure', 'done'),
        ('hardware_handover', 'hardware_setup', 'done'),
        ('phone_prepare', 'supervisor_fills_document', 'done'),
        ('catia_install', 'supervisor_fills_document', 'done'),
        ('datev_install', 'supervisor_fills_document', 'done'),
        ('tisoware_install', 'supervisor_fills_document', 'done'),

        ('babtec_user_create', 'supervisor_fills_document', 'done'),
        ('gewatec_user_create', 'supervisor_fills_document', 'done'),
        ('provis_user_create', 'supervisor_fills_document', 'done'),
        ('consense_setup', 'supervisor_fills_document', 'done')
)
INSERT INTO task_template_dependencies (task_template_id, depends_on_task_template_id, required_status)
SELECT
    t.id,
    dep.id,
    s.required_status
FROM dependency_seed s
JOIN task_templates t ON t.template_key = s.task_key
JOIN task_templates dep ON dep.template_key = s.depends_on_task_key
ON CONFLICT (task_template_id, depends_on_task_template_id) DO UPDATE
SET required_status = EXCLUDED.required_status;
