ALTER TABLE workflow_answer_definitions
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
        RAISE EXCEPTION 'process_types entry for onboarding is required before workflow_answer_definitions can be migrated.';
    END IF;

    UPDATE workflow_answer_definitions
    SET process_type_id = onboarding_process_type_id
    WHERE process_type_id IS NULL;
END;
$$;

ALTER TABLE workflow_answer_definitions
    ALTER COLUMN process_type_id SET NOT NULL;
