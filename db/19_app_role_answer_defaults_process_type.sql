ALTER TABLE app_role_answer_defaults
    ADD COLUMN IF NOT EXISTS process_type_id INTEGER REFERENCES process_types(id) ON DELETE RESTRICT;

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
        RAISE EXCEPTION 'process_types entry for onboarding is required before app_role_answer_defaults can be migrated.';
    END IF;

    UPDATE app_role_answer_defaults
    SET process_type_id = onboarding_process_type_id
    WHERE process_type_id IS NULL;
END;
$$;

ALTER TABLE app_role_answer_defaults
    ALTER COLUMN process_type_id SET NOT NULL;
