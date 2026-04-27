CREATE TABLE IF NOT EXISTS workflow_task_comments (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_task_id BIGINT NOT NULL REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    author_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    comment_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_workflow_task_comments_task_id
    ON workflow_task_comments(workflow_task_id, created_at DESC);
