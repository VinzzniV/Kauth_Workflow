-- 69_normalize_decision_conditions.sql
--
-- Best-Effort-Migration fuer alte `condition_expression`-Werte auf Decision-Edges.
--
-- Hintergrund: WorkflowDefinitionValidationService blockt seit dem ersten
-- Workflow-Builder-Commit Decision-Conditions die kein gueltiges JSON-Objekt
-- mit answerKey + operator sind. Bestandsdaten KOENNEN aber existieren falls:
--   * sie vor der Validation in die DB gekommen sind, oder
--   * sie ueber direkte SQL-Edits / Migrations-Skripte geschrieben wurden.
--
-- Symptom zur Laufzeit: PostgresWorkflowRuntimeRepository.ParseDecisionCondition
-- wirft `InvalidOperationException("Decision condition is not valid JSON")` und
-- der Workflow scheitert beim Erreichen der Decision.
--
-- Diese Migration:
--   1. Loggt im Audit-Block alle Decision-Edges mit verdaechtiger
--      condition_expression (NICHT JSON-Objekt-Form startend mit `{`).
--   2. Versucht best-effort drei bekannte Freitext-Patterns zu konvertieren:
--        `key == "value"` -> {"answerKey": "key", "operator": "eq", "expectedValueText": "value"}
--        `key != "value"` -> {"answerKey": "key", "operator": "neq", "expectedValueText": "value"}
--        `key`            -> {"answerKey": "key", "operator": "is_true"}
--      Alles andere bleibt unveraendert; die naechste Save-Validation im
--      Form-Editor wird den Eintrag dann sichtbar als Issue auswerfen.
--   3. Ist idempotent: Eintraege die bereits gueltige JSON-Objekte sind
--      werden nie angefasst.

DO $$
DECLARE
    audit_count INTEGER;
    converted_count INTEGER := 0;
    skipped_count INTEGER := 0;
BEGIN
    -- ─── Audit ──────────────────────────────────────────────────────────────
    SELECT COUNT(*)
    INTO audit_count
    FROM workflow_edges e
    JOIN workflow_nodes n ON n.id = e.source_workflow_node_id
    WHERE n.node_type = 'decision'
      AND e.condition_expression IS NOT NULL
      AND BTRIM(e.condition_expression) <> ''
      AND BTRIM(e.condition_expression) NOT LIKE '{%';

    RAISE NOTICE 'Decision-condition audit: % rows with non-JSON condition_expression', audit_count;

    IF audit_count = 0 THEN
        RAISE NOTICE 'Nothing to migrate.';
        RETURN;
    END IF;

    -- ─── Pattern: `key == "value"` oder `key == value`  ─────────────────────
    UPDATE workflow_edges e
    SET condition_expression = jsonb_build_object(
        'answerKey', match[1],
        'operator', 'eq',
        'expectedValueText', match[2]
    )::text
    FROM workflow_nodes n,
         LATERAL regexp_match(
             BTRIM(e.condition_expression),
             '^([A-Za-z_][A-Za-z0-9_]*)\s*==\s*"?([^"]*)"?\s*$'
         ) AS match
    WHERE n.id = e.source_workflow_node_id
      AND n.node_type = 'decision'
      AND e.condition_expression IS NOT NULL
      AND BTRIM(e.condition_expression) NOT LIKE '{%'
      AND match IS NOT NULL;
    GET DIAGNOSTICS converted_count = ROW_COUNT;
    RAISE NOTICE 'Converted % rows from `key == value` pattern', converted_count;

    -- ─── Pattern: `key != "value"` oder `key != value`  ─────────────────────
    UPDATE workflow_edges e
    SET condition_expression = jsonb_build_object(
        'answerKey', match[1],
        'operator', 'neq',
        'expectedValueText', match[2]
    )::text
    FROM workflow_nodes n,
         LATERAL regexp_match(
             BTRIM(e.condition_expression),
             '^([A-Za-z_][A-Za-z0-9_]*)\s*!=\s*"?([^"]*)"?\s*$'
         ) AS match
    WHERE n.id = e.source_workflow_node_id
      AND n.node_type = 'decision'
      AND e.condition_expression IS NOT NULL
      AND BTRIM(e.condition_expression) NOT LIKE '{%'
      AND match IS NOT NULL;
    GET DIAGNOSTICS converted_count = ROW_COUNT;
    RAISE NOTICE 'Converted % rows from `key != value` pattern', converted_count;

    -- ─── Pattern: `key` (boolean is_true)  ──────────────────────────────────
    UPDATE workflow_edges e
    SET condition_expression = jsonb_build_object(
        'answerKey', match[1],
        'operator', 'is_true'
    )::text
    FROM workflow_nodes n,
         LATERAL regexp_match(
             BTRIM(e.condition_expression),
             '^([A-Za-z_][A-Za-z0-9_]*)\s*$'
         ) AS match
    WHERE n.id = e.source_workflow_node_id
      AND n.node_type = 'decision'
      AND e.condition_expression IS NOT NULL
      AND BTRIM(e.condition_expression) NOT LIKE '{%'
      AND match IS NOT NULL;
    GET DIAGNOSTICS converted_count = ROW_COUNT;
    RAISE NOTICE 'Converted % rows from `key` (is_true) pattern', converted_count;

    -- ─── Verbleibende nicht-konvertierbare Eintraege loggen  ────────────────
    SELECT COUNT(*)
    INTO skipped_count
    FROM workflow_edges e
    JOIN workflow_nodes n ON n.id = e.source_workflow_node_id
    WHERE n.node_type = 'decision'
      AND e.condition_expression IS NOT NULL
      AND BTRIM(e.condition_expression) <> ''
      AND BTRIM(e.condition_expression) NOT LIKE '{%';

    IF skipped_count > 0 THEN
        RAISE WARNING 'Skipped % decision-condition rows (no known pattern matched). They will fail at runtime; please review manually:', skipped_count;
        FOR audit_count IN
            SELECT e.id
            FROM workflow_edges e
            JOIN workflow_nodes n ON n.id = e.source_workflow_node_id
            WHERE n.node_type = 'decision'
              AND e.condition_expression IS NOT NULL
              AND BTRIM(e.condition_expression) <> ''
              AND BTRIM(e.condition_expression) NOT LIKE '{%'
        LOOP
            RAISE WARNING '  workflow_edges.id = %', audit_count;
        END LOOP;
    ELSE
        RAISE NOTICE 'All non-JSON decision-conditions converted successfully.';
    END IF;
END $$;
