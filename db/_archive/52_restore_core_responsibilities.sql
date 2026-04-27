-- Restore core responsibilities without creating departments. Department links
-- are resolved only when a matching Entra-created department already exists.

WITH responsibility_seed(department_name, responsibility_key, system_key, responsibility_name, responsibility_type, description) AS (
    VALUES
        ('HR', 'hr_onboarding', NULL, 'HR-Onboarding', 'process', 'Verantwortung fuer Start, Abstimmung und Begleitung des Onboardings.'),
        ('IT', 'leadership_it', NULL, 'Abteilungsleitung IT', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der IT.'),
        ('AV', 'leadership_av', NULL, 'Abteilungsleitung AV', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der AV.'),
        ('HR', 'leadership_hr', NULL, 'Abteilungsleitung HR', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der HR.'),
        ('QS', 'leadership_qs', NULL, 'Abteilungsleitung QS', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der QS.'),
        ('QMB', 'leadership_qmb', NULL, 'Abteilungsleitung QMB', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des QMB.'),
        ('Produktion', 'leadership_production', NULL, 'Abteilungsleitung Produktion', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der Produktion.'),
        ('Vertrieb', 'leadership_sales', NULL, 'Abteilungsleitung Vertrieb', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des Vertriebs.'),
        ('Prototypenbau', 'leadership_prototype', NULL, 'Abteilungsleitung Prototypenbau', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings im Prototypenbau.'),
        ('IT', 'it_ad', 'ad', 'AD', 'application', 'Verantwortung fuer AD-Konto und zentrale Berechtigungen.'),
        ('IT', 'it_mailbox', 'mailbox', 'Mailbox', 'application', 'Verantwortung fuer Mailbox-Einrichtung.'),
        ('IT', 'it_habel', 'habel', 'Habel', 'application', 'Verantwortung fuer Habel-Zugaenge.'),
        ('IT', 'it_ln', 'ln', 'LN', 'application', 'Verantwortung fuer LN-Zugaenge.'),
        ('IT', 'it_hardware', 'hardware', 'Hardware', 'application', 'Verantwortung fuer Hardware-Bereitstellung und Einrichtung.'),
        ('QS', 'qs_babtec', 'babtec', 'Babtec', 'application', 'Verantwortung fuer Babtec in der QS.'),
        ('AV', 'av_gewatec', 'gewatec', 'Gewatec', 'application', 'Verantwortung fuer Gewatec in der AV.'),
        ('AV', 'av_provis', 'provis', 'Provis', 'application', 'Verantwortung fuer Provis in der AV.'),
        ('QMB', 'qmb_consense', 'consense', 'Consense', 'application', 'Verantwortung fuer Consense im QMB.')
)
INSERT INTO app_responsibilities (
    department_id,
    responsibility_key,
    system_key,
    name,
    responsibility_type,
    description,
    is_active
)
SELECT
    d.id,
    s.responsibility_key,
    s.system_key,
    s.responsibility_name,
    s.responsibility_type,
    s.description,
    TRUE
FROM responsibility_seed s
LEFT JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
ON CONFLICT (responsibility_key) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    system_key = EXCLUDED.system_key,
    name = EXCLUDED.name,
    responsibility_type = EXCLUDED.responsibility_type,
    description = EXCLUDED.description,
    is_active = TRUE;
