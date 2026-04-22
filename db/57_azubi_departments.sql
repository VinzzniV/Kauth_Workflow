-- Abteilungen fuer Ausbildung und Studenten (technisch und kaufmaennisch).
--
-- Designentscheidung:
--   Ausbildung technisch: stabile Ausbildungsleitung (ausbildungsleitung_technisch).
--   Ausbildung kaufmaennisch: keine feste Ausbildungsleitung auf Abteilungsebene.
--   Die Verantwortlichen bei kaufmaennischen Azubis kommen pro Rotationsstation
--   aus der jeweiligen besuchten Abteilung (Einkauf-Lead, IT-Lead usw.).
--   HR uebernimmt die administrative Begleitung (hr_onboarding).

-- =========================
-- Abteilungen
-- =========================

INSERT INTO departments (name)
SELECT department_name
FROM (
    VALUES
        ('Ausbildung technisch'),
        ('Ausbildung kaufmaennisch')
) AS seed(department_name)
WHERE NOT EXISTS (
    SELECT 1
    FROM departments d
    WHERE LOWER(d.name) = LOWER(seed.department_name)
);

-- =========================
-- Responsibility: Ausbildungsleitung Technik
-- Stabile, immer gleiche Leitung fuer technische Azubis.
-- department_lead-Typ benoetigt keinen system_key.
-- =========================

WITH dept AS (
    SELECT id FROM departments WHERE LOWER(name) = 'ausbildung technisch'
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
    dept.id,
    'ausbildungsleitung_technisch',
    NULL,
    'Ausbildungsleitung Technik',
    'department_lead',
    'Stabile Leitung fuer technische Ausbildung und Studenten. Immer dieselbe Ansprechperson.',
    TRUE
FROM dept
ON CONFLICT (responsibility_key) DO UPDATE
SET
    department_id    = EXCLUDED.department_id,
    name             = EXCLUDED.name,
    responsibility_type = EXCLUDED.responsibility_type,
    description      = EXCLUDED.description,
    is_active        = EXCLUDED.is_active;

-- =========================
-- DepartmentActionTemplates: Ausbildung technisch
-- Verantwortlicher: ausbildungsleitung_technisch (stabile Leitung)
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        -- Eintritt: Betreuung und Grundvorbereitung
        ('Ausbildung technisch', 'enter',
         'Betreuer aus Ausbildungsleitung Technik benennen',
         'Zustaendige Kontaktperson fuer den technischen Azubi oder Studenten festlegen und dem Azubi mitteilen.',
         'manual', 'ausbildungsleitung_technisch', -5, 1),

        ('Ausbildung technisch', 'enter',
         'Technische Sicherheitsunterweisung koordinieren',
         'Pflichtunterweisung fuer technischen Bereich vor Stationsstart sicherstellen.',
         'manual', 'ausbildungsleitung_technisch', -2, 0),

        ('Ausbildung technisch', 'enter',
         'AD-Konto und Grundausstattung vorbereiten',
         'AD-Zugang und notwendige Hardware rechtzeitig vor Stationsbeginn bereitstellen.',
         'technical', 'ad', -5, 1),

        -- Austritt: Abschluss und Beurteilung
        ('Ausbildung technisch', 'exit',
         'Abschlussbeurteilung durch Ausbildungsleitung Technik erstellen',
         'Fachliche Beurteilung durch die technische Ausbildungsleitung nach Stationsende anfertigen.',
         'manual', 'ausbildungsleitung_technisch', 3, 1)
)
INSERT INTO department_action_templates (
    department_id,
    trigger_type,
    title,
    description,
    task_type,
    default_responsibility_id,
    due_offset_days,
    reminder_offset_days,
    is_automatable,
    automation_key,
    is_active
)
SELECT
    d.id,
    s.trigger_type,
    s.title,
    s.description,
    s.task_type,
    r.id,
    s.due_offset_days,
    s.reminder_offset_days,
    FALSE,
    NULL,
    TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1
    FROM department_action_templates existing
    WHERE existing.department_id = d.id
      AND existing.trigger_type  = s.trigger_type
      AND existing.title         = s.title
);

-- =========================
-- DepartmentActionTemplates: Ausbildung kaufmaennisch
-- Keine feste Ausbildungsleitung auf dieser Ebene.
-- Operative Aufgaben kommen pro Station aus den besuchten Abteilungen.
-- Administrative Begleitung laeuft ueber hr_onboarding.
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        -- Eintritt: administrative Koordination
        ('Ausbildung kaufmaennisch', 'enter',
         'AD-Konto und Grundausstattung vorbereiten',
         'AD-Zugang und notwendige Hardware rechtzeitig vor Stationsstart bereitstellen.',
         'technical', 'ad', -5, 1),

        ('Ausbildung kaufmaennisch', 'enter',
         'Aufnahme in Abteilung durch HR koordinieren',
         'HR informiert die aufnehmende Abteilung ueber den bevorstehenden Einsatz und klaert offene Fragen.',
         'information', 'hr_onboarding', -2, 0),

        -- Austritt: administrativer Abschluss
        ('Ausbildung kaufmaennisch', 'exit',
         'Abschlussbericht bei aufnehmender Abteilung anfordern',
         'HR fordert nach Stationsende eine kurze Rueckmeldung bei der Abteilung an.',
         'manual', 'hr_onboarding', 3, 1)
)
INSERT INTO department_action_templates (
    department_id,
    trigger_type,
    title,
    description,
    task_type,
    default_responsibility_id,
    due_offset_days,
    reminder_offset_days,
    is_automatable,
    automation_key,
    is_active
)
SELECT
    d.id,
    s.trigger_type,
    s.title,
    s.description,
    s.task_type,
    r.id,
    s.due_offset_days,
    s.reminder_offset_days,
    FALSE,
    NULL,
    TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.responsibility_key = s.responsibility_key
WHERE NOT EXISTS (
    SELECT 1
    FROM department_action_templates existing
    WHERE existing.department_id = d.id
      AND existing.trigger_type  = s.trigger_type
      AND existing.title         = s.title
);
