CREATE INDEX IF NOT EXISTS idx_workflow_tasks_workflow_task_key
    ON workflow_tasks(workflow_id, task_key);
