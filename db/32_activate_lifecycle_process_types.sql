UPDATE process_types
SET is_active = TRUE
WHERE key IN (
    'offboarding',
    'department_change',
    'name_change',
    'position_change',
    'role_change'
);
