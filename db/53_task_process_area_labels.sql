-- Keep task area grouping independent from seeded/demo departments.
-- Entra is the source for real departments, but workflow task templates still
-- need stable fachliche Bereich labels such as IT, QS, AV and QMB.

WITH responsibility_area AS (
    SELECT
        id,
        CASE
            WHEN LOWER(responsibility_key) LIKE 'it\_%' ESCAPE '\' THEN 'IT'
            WHEN LOWER(responsibility_key) LIKE 'qs\_%' ESCAPE '\' THEN 'QS'
            WHEN LOWER(responsibility_key) LIKE 'av\_%' ESCAPE '\' THEN 'AV'
            WHEN LOWER(responsibility_key) LIKE 'qmb\_%' ESCAPE '\' THEN 'QMB'
            WHEN LOWER(responsibility_key) LIKE 'hr\_%' ESCAPE '\' THEN 'HR'
            ELSE NULL
        END AS process_area_label
    FROM app_responsibilities
)
UPDATE task_templates t
SET process_area_label = responsibility_area.process_area_label
FROM responsibility_area
WHERE responsibility_area.id = t.default_responsibility_id
  AND responsibility_area.process_area_label IS NOT NULL
  AND NULLIF(BTRIM(t.process_area_label), '') IS NULL;

UPDATE task_templates
SET process_area_label = 'Abteilungsleitung'
WHERE template_key = 'supervisor_fills_document'
  AND NULLIF(BTRIM(process_area_label), '') IS NULL;

WITH responsibility_area AS (
    SELECT
        r.id,
        CASE
            WHEN LOWER(r.responsibility_key) LIKE 'it\_%' ESCAPE '\' THEN 'IT'
            WHEN LOWER(r.responsibility_key) LIKE 'qs\_%' ESCAPE '\' THEN 'QS'
            WHEN LOWER(r.responsibility_key) LIKE 'av\_%' ESCAPE '\' THEN 'AV'
            WHEN LOWER(r.responsibility_key) LIKE 'qmb\_%' ESCAPE '\' THEN 'QMB'
            WHEN LOWER(r.responsibility_key) LIKE 'hr\_%' ESCAPE '\' THEN 'HR'
            ELSE NULL
        END AS process_area_label
    FROM app_responsibilities r
)
UPDATE workflow_tasks wt
SET process_area_label = COALESCE(tt.process_area_label, responsibility_area.process_area_label)
FROM task_templates tt
LEFT JOIN responsibility_area ON responsibility_area.id = tt.default_responsibility_id
WHERE wt.task_template_id = tt.id
  AND COALESCE(tt.process_area_label, responsibility_area.process_area_label) IS NOT NULL
  AND NULLIF(BTRIM(wt.process_area_label), '') IS NULL;

UPDATE workflow_tasks wt
SET process_area_label = 'Abteilungsleitung'
FROM task_templates tt
WHERE wt.task_template_id = tt.id
  AND tt.template_key = 'supervisor_fills_document'
  AND NULLIF(BTRIM(wt.process_area_label), '') IS NULL;
