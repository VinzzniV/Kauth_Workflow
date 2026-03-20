-- Laufzeitbezogene Datenkorrekturen und Backfills fuer bestehende Workflows.
-- Auf einem frischen Schema laufen diese Statements weitgehend leer.

-- Bestehende Aufgaben auf die explizit gepflegten Verantwortlichen ziehen.
UPDATE task_assignments ta
SET
    assignee_user_id = p.app_user_id,
    assignee_responsibility_id = NULL,
    assignment_type = 'user'
FROM system_responsibilities sr
JOIN people p ON p.id = sr.responsible_person_id
JOIN app_users u ON u.id = p.app_user_id AND u.is_active = TRUE
WHERE ta.is_primary = TRUE
  AND ta.assignee_responsibility_id = sr.app_responsibility_id
  AND (ta.assignee_user_id IS DISTINCT FROM p.app_user_id OR ta.assignment_type <> 'user');

WITH supervisor_targets AS (
    SELECT
        wt.id AS workflow_task_id,
        COALESCE(requirement_user.id, lead_user.id) AS assignee_user_id,
        r.id AS assignee_responsibility_id
    FROM workflow_tasks wt
    JOIN workflows w ON w.id = wt.workflow_id
    LEFT JOIN department_settings ds ON ds.department_id = w.department_id
    LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
    LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id AND requirement_user.is_active = TRUE
    LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
    LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id AND lead_user.is_active = TRUE
    LEFT JOIN app_responsibilities r
        ON r.department_id = w.department_id
       AND r.responsibility_type = 'department_lead'
       AND r.is_active = TRUE
    WHERE wt.task_key = 'supervisor_fills_document'
)
UPDATE task_assignments ta
SET
    assignee_user_id = st.assignee_user_id,
    assignee_responsibility_id = CASE
        WHEN st.assignee_user_id IS NULL THEN st.assignee_responsibility_id
        ELSE NULL
    END,
    assignment_type = CASE
        WHEN st.assignee_user_id IS NULL THEN 'responsibility'
        ELSE 'user'
    END
FROM supervisor_targets st
WHERE ta.workflow_task_id = st.workflow_task_id
  AND ta.is_primary = TRUE;

WITH supervisor_targets AS (
    SELECT
        wt.id AS workflow_task_id,
        COALESCE(requirement_user.id, lead_user.id) AS assignee_user_id,
        r.id AS assignee_responsibility_id
    FROM workflow_tasks wt
    JOIN workflows w ON w.id = wt.workflow_id
    LEFT JOIN department_settings ds ON ds.department_id = w.department_id
    LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
    LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id AND requirement_user.is_active = TRUE
    LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
    LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id AND lead_user.is_active = TRUE
    LEFT JOIN app_responsibilities r
        ON r.department_id = w.department_id
       AND r.responsibility_type = 'department_lead'
       AND r.is_active = TRUE
    WHERE wt.task_key = 'supervisor_fills_document'
)
INSERT INTO task_assignments (
    workflow_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary
)
SELECT
    st.workflow_task_id,
    st.assignee_user_id,
    CASE
        WHEN st.assignee_user_id IS NULL THEN st.assignee_responsibility_id
        ELSE NULL
    END,
    CASE
        WHEN st.assignee_user_id IS NULL THEN 'responsibility'
        ELSE 'user'
    END,
    TRUE
FROM supervisor_targets st
WHERE NOT EXISTS (
    SELECT 1
    FROM task_assignments ta
    WHERE ta.workflow_task_id = st.workflow_task_id
      AND ta.is_primary = TRUE
);

-- Bestehende Workflow-Tasks an das aktuelle Required- und Beschreibungsschema angleichen.
UPDATE workflow_tasks wt
SET is_required = tt.is_required
FROM task_templates tt
WHERE (wt.task_template_id = tt.id OR (wt.task_template_id IS NULL AND wt.task_key = tt.template_key))
  AND wt.is_required IS DISTINCT FROM tt.is_required;

WITH workflow_context AS (
    SELECT
        w.id AS workflow_id,
        MAX(CASE
            WHEN d.answer_key = 'comparison_user_name'
            THEN NULLIF(BTRIM(a.value_text), '')
            ELSE NULL
        END) AS comparison_user_name,
        MAX(CASE
            WHEN d.answer_key = 'hardware_type'
            THEN COALESCE(NULLIF(BTRIM(o.option_label), ''), NULLIF(BTRIM(o.option_value), ''))
            ELSE NULL
        END) AS hardware_type_label
    FROM workflows w
    LEFT JOIN workflow_answers a ON a.workflow_id = w.id
    LEFT JOIN workflow_answer_definitions d ON d.id = a.answer_definition_id
    LEFT JOIN workflow_answer_options o ON o.id = a.selected_option_id
    GROUP BY w.id
)
UPDATE workflow_tasks wt
SET description = CONCAT(
    'AD-Berechtigungen anhand einer Vergleichsperson übernehmen.',
    CASE
        WHEN wc.comparison_user_name IS NOT NULL
            THEN ' Referenzuser: ' || wc.comparison_user_name || '.'
        ELSE ''
    END
)
FROM workflow_context wc
WHERE wt.workflow_id = wc.workflow_id
  AND wt.task_key = 'permissions_from_reference_user';

WITH workflow_context AS (
    SELECT
        w.id AS workflow_id,
        MAX(CASE
            WHEN d.answer_key = 'hardware_type'
            THEN COALESCE(NULLIF(BTRIM(o.option_label), ''), NULLIF(BTRIM(o.option_value), ''))
            ELSE NULL
        END) AS hardware_type_label
    FROM workflows w
    LEFT JOIN workflow_answers a ON a.workflow_id = w.id
    LEFT JOIN workflow_answer_definitions d ON d.id = a.answer_definition_id
    LEFT JOIN workflow_answer_options o ON o.id = a.selected_option_id
    GROUP BY w.id
)
UPDATE workflow_tasks wt
SET description = CONCAT(
    CASE wt.task_key
        WHEN 'hardware_procure' THEN 'Hardware-Bedarf prüfen und bei Bedarf passende Hardware beschaffen.'
        WHEN 'hardware_setup' THEN 'Hardware installieren und für den Einsatz vorbereiten.'
        WHEN 'hardware_handover' THEN 'Eingerichtete Hardware für die neue Person bereitstellen.'
        ELSE wt.description
    END,
    CASE
        WHEN wc.hardware_type_label IS NOT NULL
            THEN ' Gewünschter Hardware-Typ: ' || wc.hardware_type_label || '.'
        ELSE ''
    END
)
FROM workflow_context wc
WHERE wt.workflow_id = wc.workflow_id
  AND wt.task_key IN ('hardware_procure', 'hardware_setup', 'hardware_handover');

-- Legacy-Workflows mit altem Status "open" auf fehlende Aufgaben und neue Statuslogik anheben.
WITH backfill_seed(task_key, answer_key) AS (
    VALUES
        ('internet_access_enable', 'internet_requested'),
        ('internal_drive_access_grant', 'internal_drive_access_requested'),
        ('office_install', 'microsoft_office_requested'),
        ('phone_prepare', 'phone_requested'),
        ('catia_install', 'catia_requested'),
        ('datev_install', 'datev_requested'),
        ('tisoware_install', 'tiso_requested')
),
eligible_workflows AS (
    SELECT
        w.id AS workflow_id,
        w.department_id,
        tt.id AS task_template_id,
        tt.template_key,
        tt.title,
        tt.category,
        tt.description,
        tt.icon_key,
        tt.default_responsibility_id,
        tt.is_required,
        tt.sort_order,
        dep_wt.id AS depends_on_workflow_task_id,
        dep_wt.status AS depends_on_status
    FROM workflows w
    JOIN backfill_seed s ON TRUE
    JOIN task_templates tt ON tt.template_key = s.task_key AND tt.is_active = TRUE
    JOIN workflow_answers a ON a.workflow_id = w.id AND a.value_boolean = TRUE
    JOIN workflow_answer_definitions d ON d.id = a.answer_definition_id AND d.answer_key = s.answer_key
    LEFT JOIN workflow_tasks existing_task ON existing_task.workflow_id = w.id AND existing_task.task_key = tt.template_key
    LEFT JOIN workflow_tasks dep_wt ON dep_wt.workflow_id = w.id AND dep_wt.task_key = 'supervisor_fills_document'
    WHERE w.status = 'open'
      AND existing_task.id IS NULL
),
inserted_tasks AS (
    INSERT INTO workflow_tasks (
        workflow_id,
        task_template_id,
        task_key,
        title,
        category,
        description,
        icon_key,
        status,
        is_required,
        sort_order,
        ready_at
    )
    SELECT
        ew.workflow_id,
        ew.task_template_id,
        ew.template_key,
        ew.title,
        ew.category,
        ew.description,
        ew.icon_key,
        CASE
            WHEN ew.depends_on_workflow_task_id IS NOT NULL AND ew.depends_on_status = 'done' THEN 'ready'
            ELSE 'blocked'
        END AS status,
        ew.is_required,
        ew.sort_order,
        CASE
            WHEN ew.depends_on_workflow_task_id IS NOT NULL AND ew.depends_on_status = 'done' THEN NOW()
            ELSE NULL
        END AS ready_at
    FROM eligible_workflows ew
    RETURNING id, workflow_id, task_template_id, task_key
),
inserted_assignments AS (
    INSERT INTO task_assignments (
        workflow_task_id,
        assignee_user_id,
        assignee_responsibility_id,
        assignment_type,
        is_primary
    )
    SELECT
        it.id,
        responsible_user.app_user_id,
        CASE
            WHEN responsible_user.app_user_id IS NULL THEN tt.default_responsibility_id
            ELSE NULL
        END,
        CASE
            WHEN responsible_user.app_user_id IS NOT NULL THEN 'user'
            ELSE 'responsibility'
        END,
        TRUE
    FROM inserted_tasks it
    JOIN task_templates tt ON tt.id = it.task_template_id
    LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = tt.default_responsibility_id
    LEFT JOIN people responsible_user ON responsible_user.id = sr.responsible_person_id
    WHERE tt.default_responsibility_id IS NOT NULL
    RETURNING workflow_task_id
)
INSERT INTO workflow_task_dependencies (
    workflow_task_id,
    depends_on_workflow_task_id,
    required_status
)
SELECT
    it.id,
    dep_wt.id,
    'done'
FROM inserted_tasks it
JOIN workflow_tasks dep_wt ON dep_wt.workflow_id = it.workflow_id AND dep_wt.task_key = 'supervisor_fills_document'
ON CONFLICT (workflow_task_id, depends_on_workflow_task_id) DO NOTHING;

-- Legacy-cancelled workflows must be normalized before the global status rollup.
UPDATE workflow_task_dependencies
SET required_status = 'skipped'
WHERE required_status = 'cancelled';

UPDATE task_template_dependencies
SET required_status = 'skipped'
WHERE required_status = 'cancelled';

UPDATE workflow_tasks wt
SET status = 'skipped',
    cancelled_at = COALESCE(wt.cancelled_at, w.cancelled_at, NOW()),
    completed_at = NULL
FROM workflows w
WHERE wt.workflow_id = w.id
  AND (
      wt.status = 'cancelled'
      OR (w.status = 'cancelled' AND wt.status NOT IN ('done', 'skipped'))
  );

UPDATE workflows
SET status = 'completed',
    completed_at = COALESCE(completed_at, cancelled_at, NOW()),
    cancelled_at = NULL
WHERE status = 'cancelled';

WITH workflow_rollup AS (
    SELECT
        w.id AS workflow_id,
        w.status AS current_status,
        COUNT(wt.id) AS task_count,
        COALESCE(BOOL_AND(wt.status IN ('done', 'skipped')), FALSE) AS all_tasks_done,
        COALESCE(BOOL_OR(wt.task_key = 'supervisor_fills_document'), FALSE) AS has_supervisor_task,
        COALESCE(BOOL_OR(wt.task_key = 'supervisor_fills_document' AND wt.status = 'done'), FALSE) AS supervisor_done,
        COALESCE(BOOL_OR(wt.task_key = 'supervisor_fills_document' AND wt.status IN ('ready', 'in_progress')), FALSE) AS supervisor_active,
        COALESCE(BOOL_OR(wt.task_key <> 'supervisor_fills_document' AND wt.status = 'in_progress'), FALSE) AS department_in_progress,
        COALESCE(BOOL_OR(wt.task_key <> 'supervisor_fills_document' AND wt.status IN ('open', 'ready', 'blocked', 'in_progress')), FALSE) AS department_active
    FROM workflows w
    LEFT JOIN workflow_tasks wt ON wt.workflow_id = w.id
    GROUP BY w.id, w.status
),
workflow_status_recalc AS (
    SELECT
        workflow_id,
        CASE
            WHEN task_count = 0 AND current_status = 'completed' THEN 'completed'
            WHEN task_count = 0 THEN 'draft'
            WHEN all_tasks_done THEN 'completed'
            WHEN has_supervisor_task AND supervisor_active THEN 'waiting_for_supervisor'
            WHEN has_supervisor_task AND supervisor_done AND department_in_progress THEN 'in_progress'
            WHEN has_supervisor_task AND supervisor_done THEN 'waiting_for_department'
            WHEN has_supervisor_task THEN 'draft'
            WHEN department_in_progress THEN 'in_progress'
            WHEN department_active THEN 'waiting_for_department'
            ELSE 'draft'
        END AS next_status
    FROM workflow_rollup
)
UPDATE workflows w
SET
    status = s.next_status,
    started_at = CASE
        WHEN s.next_status IN ('waiting_for_supervisor', 'waiting_for_department', 'in_progress', 'completed')
            THEN COALESCE(w.started_at, NOW())
        ELSE w.started_at
    END,
    completed_at = CASE
        WHEN s.next_status = 'completed' THEN COALESCE(w.completed_at, NOW())
        ELSE NULL
    END,
    cancelled_at = NULL
FROM workflow_status_recalc s
WHERE w.id = s.workflow_id;
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_task_template_conditions_answer_key'
    ) THEN
        ALTER TABLE task_template_conditions
        ADD CONSTRAINT fk_task_template_conditions_answer_key
        FOREIGN KEY (answer_key)
        REFERENCES workflow_answer_definitions(answer_key)
        ON UPDATE CASCADE
        ON DELETE RESTRICT;
    END IF;
END $$;

UPDATE people p
SET
    department_id = sync.department_id,
    updated_at = NOW()
FROM (
    SELECT
        p_inner.id,
        COALESCE(p_inner.department_id, u.department_id) AS department_id
    FROM people p_inner
    JOIN app_users u ON u.id = p_inner.app_user_id
) sync
WHERE p.id = sync.id
  AND p.department_id IS DISTINCT FROM sync.department_id;

UPDATE app_users u
SET department_id = sync.department_id
FROM (
    SELECT
        u_inner.id,
        COALESCE(p.department_id, u_inner.department_id) AS department_id
    FROM app_users u_inner
    LEFT JOIN people p ON p.app_user_id = u_inner.id
) sync
WHERE u.id = sync.id
  AND u.department_id IS DISTINCT FROM sync.department_id;

CREATE OR REPLACE FUNCTION sync_people_department_to_user()
RETURNS TRIGGER AS $$
BEGIN
    IF pg_trigger_depth() > 1 THEN
        RETURN NEW;
    END IF;

    UPDATE app_users
    SET department_id = NEW.department_id
    WHERE id = NEW.app_user_id
      AND department_id IS DISTINCT FROM NEW.department_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sync_user_department_to_people()
RETURNS TRIGGER AS $$
BEGIN
    IF pg_trigger_depth() > 1 THEN
        RETURN NEW;
    END IF;

    UPDATE people
    SET
        department_id = NEW.department_id,
        updated_at = NOW()
    WHERE app_user_id = NEW.id
      AND department_id IS DISTINCT FROM NEW.department_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_people_sync_department_to_user ON people;
CREATE TRIGGER trg_people_sync_department_to_user
AFTER INSERT OR UPDATE OF department_id ON people
FOR EACH ROW
EXECUTE FUNCTION sync_people_department_to_user();

DROP TRIGGER IF EXISTS trg_app_users_sync_department_to_people ON app_users;
CREATE TRIGGER trg_app_users_sync_department_to_people
AFTER INSERT OR UPDATE OF department_id ON app_users
FOR EACH ROW
EXECUTE FUNCTION sync_user_department_to_people();

UPDATE workflow_answers wa
SET selected_option_id = NULL
WHERE selected_option_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM workflow_answer_options wao
      WHERE wao.id = wa.selected_option_id
        AND wao.answer_definition_id = wa.answer_definition_id
  );

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_workflow_answer_options_definition_option_pair'
    ) THEN
        ALTER TABLE workflow_answer_options
        ADD CONSTRAINT uq_workflow_answer_options_definition_option_pair
        UNIQUE (answer_definition_id, id);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_workflow_answers_selected_option_matches_definition'
    ) THEN
        ALTER TABLE workflow_answers
        ADD CONSTRAINT fk_workflow_answers_selected_option_matches_definition
        FOREIGN KEY (answer_definition_id, selected_option_id)
        REFERENCES workflow_answer_options(answer_definition_id, id)
        ON DELETE RESTRICT;
    END IF;
END $$;
