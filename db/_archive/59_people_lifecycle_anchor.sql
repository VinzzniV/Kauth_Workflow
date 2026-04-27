-- Migration: canonical employee lifecycle anchor and exact directory employee linking.

ALTER TABLE people
    ADD COLUMN IF NOT EXISTS current_position_role_id INTEGER REFERENCES app_roles(id) ON DELETE SET NULL;

ALTER TABLE directory_identities
    ADD COLUMN IF NOT EXISTS employee_number INTEGER;

WITH latest_workflow AS (
    SELECT DISTINCT ON (resolved.person_id)
        resolved.person_id,
        resolved.position_role_id
    FROM (
        SELECT
            w.target_person_id AS person_id,
            w.position_role_id,
            w.created_at,
            w.id
        FROM workflows w
        WHERE w.target_person_id IS NOT NULL

        UNION ALL

        SELECT
            p.id AS person_id,
            w.position_role_id,
            w.created_at,
            w.id
        FROM people p
        JOIN workflows w ON p.employee_number IS NOT NULL AND w.employee_number = p.employee_number
    ) resolved
    ORDER BY resolved.person_id, resolved.created_at DESC, resolved.id DESC
)
UPDATE people p
SET
    current_position_role_id = COALESCE(p.current_position_role_id, latest_workflow.position_role_id),
    updated_at = NOW()
FROM latest_workflow
WHERE p.id = latest_workflow.person_id
  AND p.current_position_role_id IS NULL
  AND latest_workflow.position_role_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_people_employee_number
    ON people(employee_number)
    WHERE employee_number IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_people_current_position_role_id
    ON people(current_position_role_id)
    WHERE current_position_role_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_directory_identities_employee_number
    ON directory_identities(employee_number)
    WHERE employee_number IS NOT NULL;
