-- Remove legacy bootstrap/demo departments. Departments are Entra-managed and
-- are created by the Directory Sync from directory_identities.department_name.
-- Existing workflow departments and departments currently present in Entra data
-- are kept to avoid corrupting real historical data.

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
        ('Prototypenbau')
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
)
DELETE FROM departments d
USING removable_departments removable
WHERE d.id = removable.id;
