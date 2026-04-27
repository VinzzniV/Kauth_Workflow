-- Migration: separate person master data from login identities.
-- A person may exist without a linked app_user and should survive identity changes.

ALTER TABLE people
    ADD COLUMN IF NOT EXISTS directory_identity_id BIGINT REFERENCES directory_identities(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS first_name VARCHAR(120),
    ADD COLUMN IF NOT EXISTS last_name VARCHAR(120),
    ADD COLUMN IF NOT EXISTS employee_number INTEGER,
    ADD COLUMN IF NOT EXISTS badge_number INTEGER,
    ADD COLUMN IF NOT EXISTS employment_status VARCHAR(32),
    ADD COLUMN IF NOT EXISTS entry_date DATE,
    ADD COLUMN IF NOT EXISTS exit_date DATE;

ALTER TABLE people
    ALTER COLUMN app_user_id DROP NOT NULL;

ALTER TABLE people
    DROP CONSTRAINT IF EXISTS people_app_user_id_fkey;

ALTER TABLE people
    ADD CONSTRAINT people_app_user_id_fkey
    FOREIGN KEY (app_user_id) REFERENCES app_users(id) ON DELETE SET NULL;

DROP INDEX IF EXISTS uq_people_app_user_id;
CREATE UNIQUE INDEX IF NOT EXISTS uq_people_app_user_id
    ON people(app_user_id)
    WHERE app_user_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_people_directory_identity_id
    ON people(directory_identity_id)
    WHERE directory_identity_id IS NOT NULL;

UPDATE people p
SET directory_identity_id = di.id
FROM directory_identities di
WHERE p.directory_identity_id IS NULL
  AND p.app_user_id IS NOT NULL
  AND di.app_user_id = p.app_user_id;

WITH latest_workflow AS (
    SELECT DISTINCT ON (w.target_person_id)
        w.target_person_id AS person_id,
        w.first_name,
        w.last_name,
        w.employee_number,
        w.badge_number
    FROM workflows w
    WHERE w.target_person_id IS NOT NULL
    ORDER BY w.target_person_id, w.created_at DESC, w.id DESC
)
UPDATE people p
SET
    first_name = COALESCE(p.first_name, latest_workflow.first_name),
    last_name = COALESCE(p.last_name, latest_workflow.last_name),
    employee_number = COALESCE(p.employee_number, latest_workflow.employee_number),
    badge_number = COALESCE(p.badge_number, latest_workflow.badge_number),
    employment_status = COALESCE(
        p.employment_status,
        CASE
            WHEN p.exit_date IS NOT NULL THEN 'exited'
            ELSE 'active'
        END
    ),
    updated_at = NOW()
FROM latest_workflow
WHERE p.id = latest_workflow.person_id;

UPDATE people p
SET
    first_name = COALESCE(p.first_name, NULLIF(split_part(u.display_name, ' ', 1), '')),
    last_name = COALESCE(
        p.last_name,
        NULLIF(BTRIM(SUBSTRING(u.display_name FROM LENGTH(split_part(u.display_name, ' ', 1)) + 1)), '')
    ),
    employment_status = COALESCE(
        p.employment_status,
        CASE
            WHEN p.exit_date IS NOT NULL THEN 'exited'
            WHEN u.is_active THEN 'active'
            ELSE 'inactive'
        END
    ),
    updated_at = NOW()
FROM app_users u
WHERE p.app_user_id = u.id;

UPDATE people
SET employment_status = COALESCE(
    employment_status,
    CASE
        WHEN exit_date IS NOT NULL THEN 'exited'
        ELSE 'active'
    END
);
