ALTER TABLE workflows
    ADD COLUMN IF NOT EXISTS process_type_id INTEGER REFERENCES process_types(id) ON DELETE RESTRICT;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'workflows'
          AND column_name = 'onboarding_role_id'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'workflows'
          AND column_name = 'position_role_id'
    ) THEN
        ALTER TABLE workflows
            RENAME COLUMN onboarding_role_id TO position_role_id;
    END IF;
END;
$$;

ALTER TABLE workflows
    ADD COLUMN IF NOT EXISTS target_person_id BIGINT REFERENCES people(id) ON DELETE RESTRICT;

DO $$
DECLARE
    onboarding_process_type_id INTEGER;
BEGIN
    SELECT id
    INTO onboarding_process_type_id
    FROM process_types
    WHERE key = 'onboarding'
    LIMIT 1;

    IF onboarding_process_type_id IS NULL THEN
        RAISE EXCEPTION 'process_types entry for onboarding is required before workflows can be migrated.';
    END IF;

    UPDATE workflows
    SET process_type_id = onboarding_process_type_id
    WHERE process_type_id IS NULL;
END;
$$;

ALTER TABLE workflows
    ALTER COLUMN process_type_id SET NOT NULL;
