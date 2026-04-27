-- Remove demo departments that are not backed by Entra directory data.
-- Keep explicitly modeled training departments for rotations:
--   - Ausbildung technisch
--   - Ausbildung kaufmaennisch
--
-- Existing workflow/rotation history is preserved. Departments that are still
-- referenced by workflow or rotation runtime data are not removed.

WITH demo_department_names(name) AS (
    VALUES
        ('IT'),
        ('HR'),
        ('Engineering'),
        ('QS'),
        ('AV'),
        ('QMB'),
        ('Produktion'),
        ('Vertrieb'),
        ('Prototypenbau'),
        ('BS'),
        ('VT')
),
removable_departments AS (
    SELECT d.id
    FROM departments d
    JOIN demo_department_names demo
        ON LOWER(d.name) = LOWER(demo.name)
    WHERE NOT EXISTS (
        SELECT 1
        FROM directory_identities di
        WHERE di.department_name IS NOT NULL
          AND LOWER(BTRIM(di.department_name)) = LOWER(d.name)
    )
      AND NOT EXISTS (
        SELECT 1
        FROM workflows w
        WHERE w.department_id = d.id
    )
      AND NOT EXISTS (
        SELECT 1
        FROM rotation_stations rs
        WHERE rs.department_id = d.id
    )
      AND NOT EXISTS (
        SELECT 1
        FROM rotation_generated_tasks rgt
        WHERE rgt.department_id = d.id
    )
)
DELETE FROM department_action_templates dat
USING removable_departments removable
WHERE dat.department_id = removable.id;

WITH demo_department_names(name) AS (
    VALUES
        ('IT'),
        ('HR'),
        ('Engineering'),
        ('QS'),
        ('AV'),
        ('QMB'),
        ('Produktion'),
        ('Vertrieb'),
        ('Prototypenbau'),
        ('BS'),
        ('VT')
),
removable_departments AS (
    SELECT d.id
    FROM departments d
    JOIN demo_department_names demo
        ON LOWER(d.name) = LOWER(demo.name)
    WHERE NOT EXISTS (
        SELECT 1
        FROM directory_identities di
        WHERE di.department_name IS NOT NULL
          AND LOWER(BTRIM(di.department_name)) = LOWER(d.name)
    )
      AND NOT EXISTS (
        SELECT 1
        FROM workflows w
        WHERE w.department_id = d.id
    )
      AND NOT EXISTS (
        SELECT 1
        FROM rotation_stations rs
        WHERE rs.department_id = d.id
    )
      AND NOT EXISTS (
        SELECT 1
        FROM rotation_generated_tasks rgt
        WHERE rgt.department_id = d.id
    )
)
DELETE FROM departments d
USING removable_departments removable
WHERE d.id = removable.id;
