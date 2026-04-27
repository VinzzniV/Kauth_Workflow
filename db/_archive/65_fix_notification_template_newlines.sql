UPDATE notification_templates
SET
    body_template = REPLACE(body_template, '\n', E'\n'),
    updated_at = NOW()
WHERE body_template LIKE '%\n%';
