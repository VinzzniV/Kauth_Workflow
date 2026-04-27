ALTER TABLE notification_email_settings
    ADD COLUMN IF NOT EXISTS sandbox_redirect_email VARCHAR(320),
    ADD COLUMN IF NOT EXISTS notify_on_workflow_created BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS notify_on_task_ready BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS notify_on_workflow_completed BOOLEAN NOT NULL DEFAULT TRUE;
