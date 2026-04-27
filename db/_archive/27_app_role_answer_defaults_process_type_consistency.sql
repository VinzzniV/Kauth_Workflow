UPDATE app_role_answer_defaults ard
SET process_type_id = wad.process_type_id
FROM workflow_answer_definitions wad
WHERE wad.id = ard.answer_definition_id
  AND ard.process_type_id <> wad.process_type_id;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_workflow_answer_definitions_id_process_type'
    ) THEN
        ALTER TABLE workflow_answer_definitions
            ADD CONSTRAINT uq_workflow_answer_definitions_id_process_type
                UNIQUE (id, process_type_id);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_app_role_answer_defaults_definition_process_type'
    ) THEN
        ALTER TABLE app_role_answer_defaults
            ADD CONSTRAINT fk_app_role_answer_defaults_definition_process_type
                FOREIGN KEY (answer_definition_id, process_type_id)
                REFERENCES workflow_answer_definitions(id, process_type_id)
                ON DELETE CASCADE;
    END IF;
END $$;
