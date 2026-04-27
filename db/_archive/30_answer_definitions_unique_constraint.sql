DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM workflow_answer_definitions
        GROUP BY process_type_id, answer_key
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Duplicate workflow answer definition keys exist within the same process type.';
    END IF;
END $$;

ALTER TABLE workflow_answer_definitions
DROP CONSTRAINT IF EXISTS workflow_answer_definitions_answer_key_key;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uq_workflow_answer_definitions_process_type_answer_key'
    ) THEN
        ALTER TABLE workflow_answer_definitions
        ADD CONSTRAINT uq_workflow_answer_definitions_process_type_answer_key
        UNIQUE (process_type_id, answer_key);
    END IF;
END $$;
