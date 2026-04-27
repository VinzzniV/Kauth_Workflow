ALTER TABLE workflows
ADD COLUMN IF NOT EXISTS archived_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS idx_workflows_archived_at
    ON workflows(archived_at)
    WHERE archived_at IS NOT NULL;
