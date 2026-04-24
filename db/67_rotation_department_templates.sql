-- Fix: Migration 57 hat den JOIN auf r.responsibility_key = 'ad' gemacht,
-- obwohl 'ad' der system_key ist. Dadurch hatten die Azubi-Vorlagen
-- default_responsibility_id = NULL.
-- Die eigentlichen Rotation-Vorlagen liegen jetzt in 55_rotation_dev_template_examples.sql.

UPDATE department_action_templates dat
SET default_responsibility_id = r.id
FROM app_responsibilities r
WHERE r.system_key = 'ad'
  AND dat.default_responsibility_id IS NULL
  AND dat.is_active = TRUE
  AND EXISTS (
      SELECT 1 FROM departments d
      WHERE d.id = dat.department_id
        AND LOWER(d.name) IN ('ausbildung technisch', 'ausbildung kaufmaennisch')
  );
