-- Demo-/Dev-Ergaenzungen auf Basis des produktiven Bootstraps.
-- Diese Datei enthaelt bewusst nur Demo-Daten und lokale Defaults.

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
