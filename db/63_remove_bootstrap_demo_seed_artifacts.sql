-- Remove legacy bootstrap/demo departments and their seeded position roles.
-- Real runtime references and Entra-backed departments remain untouched.

WITH demo_role_keys(role_key) AS (
    VALUES
        ('position_it_administrator'),
        ('position_it_support'),
        ('position_developer'),
        ('position_accountant'),
        ('position_financial_controller'),
        ('position_hr_manager'),
        ('position_hr_assistant'),
        ('position_mechanical_engineer'),
        ('position_production_engineer'),
        ('position_quality_engineer'),
        ('position_qa_analyst'),
        ('position_production_operator'),
        ('position_sales_representative'),
        ('position_prototype_builder')
)
DELETE FROM app_roles r
USING demo_role_keys demo
WHERE r.role_key = demo.role_key
  AND r.role_kind = 'position'
  AND NOT EXISTS (
      SELECT 1
      FROM workflows w
      WHERE w.position_role_id = r.id
  )
  AND NOT EXISTS (
      SELECT 1
      FROM app_user_roles aur
      WHERE aur.app_role_id = r.id
  )
  AND NOT EXISTS (
      SELECT 1
      FROM app_group_roles agr
      WHERE agr.app_role_id = r.id
  );

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
        ('VT'),
        ('Einkauf')
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
        ('VT'),
        ('Einkauf')
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
