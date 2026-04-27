ALTER TABLE workflows
    ADD COLUMN IF NOT EXISTS deadline_date DATE;
