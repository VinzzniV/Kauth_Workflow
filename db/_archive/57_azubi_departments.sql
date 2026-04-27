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

