UPDATE workflow_definition_versions v
SET
    name = CASE d.definition_key
        WHEN 'onboarding' THEN 'Onboarding Standard'
        WHEN 'offboarding' THEN 'Offboarding Standard'
        WHEN 'department_change' THEN 'Abteilungswechsel Standard'
        WHEN 'name_change' THEN 'Namensaenderung Standard'
        WHEN 'position_change' THEN 'Positionswechsel Standard'
        WHEN 'role_change' THEN 'Rollenwechsel Standard'
        ELSE v.name
    END,
    updated_at = NOW()
FROM workflow_definitions d
WHERE d.id = v.workflow_definition_id
  AND BTRIM(COALESCE(v.name, '')) = 'Business Phase Mapping'
  AND d.definition_key IN (
      'onboarding',
      'offboarding',
      'department_change',
      'name_change',
      'position_change',
      'role_change'
  );
