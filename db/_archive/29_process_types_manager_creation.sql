ALTER TABLE process_types
ADD COLUMN IF NOT EXISTS allows_manager_creation BOOLEAN NOT NULL DEFAULT FALSE;

UPDATE process_types
SET allows_manager_creation = TRUE
WHERE key IN ('department_change', 'name_change', 'position_change', 'role_change');
