-- Development-only example configuration for rotation planning.
-- Creates example departments and initial department action templates
-- for manual testing of the rotation slice.

INSERT INTO departments (name)
SELECT department_name
FROM (
    VALUES
        ('BS'),
        ('VT'),
        ('Einkauf'),
        ('Produktion'),
        ('IT')
) AS seed(department_name)
WHERE NOT EXISTS (
    SELECT 1
    FROM departments d
    WHERE LOWER(d.name) = LOWER(seed.department_name)
);

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Einkauf', 'enter', 'Ordnerrechte Einkauf setzen', 'Technische Rechte fuer den Einsatz in Einkauf rechtzeitig vorbereiten.', 'technical', 'ad', -2, 1),
        ('Einkauf', 'enter', 'Habel-Zugang beantragen', 'Zugang fuer Habel vor Stationsbeginn anfordern.', 'technical', 'habel', -3, 1),
        ('Einkauf', 'enter', 'Ansprechpartner informieren', 'Fachbereich ueber den bevorstehenden Einsatz informieren.', 'information', NULL, -1, 0),
        ('Einkauf', 'exit', 'Ordnerrechte Einkauf entfernen', 'Nicht mehr benoetigte Rechte nach Stationsende entziehen.', 'technical', 'ad', 1, 2),
        ('Einkauf', 'exit', 'Habel-Zugang entziehen', 'Habel-Zugang nach dem Wechsel wieder entfernen.', 'technical', 'habel', 1, 2),
        ('Produktion', 'enter', 'Produktionsfreigaben pruefen', 'Arbeitsfaehigkeit im Produktionsumfeld fachlich pruefen.', 'approval', NULL, -1, 0),
        ('Produktion', 'enter', 'Benoetigte Software bereitstellen', 'Arbeitsplatz fuer Produktion technisch vorbereiten.', 'technical', 'hardware', -3, 1),
        ('Produktion', 'exit', 'Produktionszugaenge entfernen', 'Nicht mehr benoetigte Produktionszugaenge nachhalten und entziehen.', 'technical', 'ad', 1, 2),
        ('IT', 'enter', 'IT-Startcheck durchfuehren', 'Arbeitsmittel, Zugriffe und Einweisung fuer die IT-Station abstimmen.', 'manual', 'leadership_it', -1, 0),
        ('IT', 'exit', 'IT-bezogene Sonderrechte pruefen', 'Sonderrechte und temporaere Admin-Zugaenge nach dem Wechsel pruefen.', 'manual', 'leadership_it', 1, 2)
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
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1
    FROM department_action_templates existing
    WHERE existing.department_id = d.id
      AND existing.trigger_type = s.trigger_type
      AND existing.title = s.title
);
