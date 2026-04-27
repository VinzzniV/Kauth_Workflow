ALTER TABLE workflow_tasks
    ADD COLUMN IF NOT EXISTS node_instance_id BIGINT REFERENCES workflow_node_instances(id) ON DELETE SET NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_tasks_node_instance_id
    ON workflow_tasks(node_instance_id)
    WHERE node_instance_id IS NOT NULL;
