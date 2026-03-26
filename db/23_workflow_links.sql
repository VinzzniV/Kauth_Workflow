-- =========================
-- [F7.6.1] Workflow-Verknüpfung: Link-Tabelle, Ableitungsregeln, Seed
-- =========================
-- Ermöglicht die Verknüpfung von Workflows untereinander (z. B. Offboarding
-- verweist auf den ursprünglichen Onboarding-Workflow) und die automatische
-- Ableitung von Requirement-Antworten aus einem Quell-Workflow.
-- =========================

-- =========================
-- Tabelle: workflow_links
-- Verknüpft zwei Workflows mit einem typisiertem Beziehungstyp.
-- =========================
CREATE TABLE IF NOT EXISTS workflow_links (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    target_workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    link_type VARCHAR(40) NOT NULL CHECK (link_type IN ('derived_from', 'supersedes', 'related')),
    created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (source_workflow_id, target_workflow_id, link_type),
    CHECK (source_workflow_id <> target_workflow_id)
);

CREATE INDEX IF NOT EXISTS idx_workflow_links_source ON workflow_links(source_workflow_id);
CREATE INDEX IF NOT EXISTS idx_workflow_links_target ON workflow_links(target_workflow_id);

-- =========================
-- Tabelle: workflow_answer_derivation_rules
-- Definiert wie Antworten eines Prozesstyps auf einen anderen übertragen werden.
-- Beispiel: Onboarding has_ad_account=true → Offboarding ob_has_ad_account=true
-- =========================
CREATE TABLE IF NOT EXISTS workflow_answer_derivation_rules (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_process_type_id INTEGER NOT NULL REFERENCES process_types(id) ON DELETE CASCADE,
    target_process_type_id INTEGER NOT NULL REFERENCES process_types(id) ON DELETE CASCADE,
    source_answer_key VARCHAR(120) NOT NULL REFERENCES workflow_answer_definitions(answer_key) ON UPDATE CASCADE ON DELETE CASCADE,
    target_answer_key VARCHAR(120) NOT NULL REFERENCES workflow_answer_definitions(answer_key) ON UPDATE CASCADE ON DELETE CASCADE,
    derivation_kind VARCHAR(40) NOT NULL CHECK (derivation_kind IN ('copy_boolean', 'copy_text', 'copy_number', 'copy_selected_option')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (source_answer_key, target_answer_key),
    CHECK (source_process_type_id <> target_process_type_id)
);

CREATE INDEX IF NOT EXISTS idx_derivation_rules_source_process
    ON workflow_answer_derivation_rules(source_process_type_id);
CREATE INDEX IF NOT EXISTS idx_derivation_rules_target_process
    ON workflow_answer_derivation_rules(target_process_type_id);

-- =========================
-- Seed: Onboarding → Offboarding Ableitungsregeln
-- Welche Systeme beim Onboarding eingerichtet wurden, werden beim Offboarding
-- automatisch als "vorhanden" vorbelegt, damit HR sie nicht erneut manuell erfassen muss.
-- =========================
WITH derivation_seed(source_key, target_key, kind) AS (
    VALUES
        ('has_ad_account', 'ob_has_ad_account', 'copy_boolean'),
        ('has_mailbox',    'ob_has_mailbox',    'copy_boolean'),
        ('has_hardware',   'ob_has_hardware',   'copy_boolean'),
        ('has_phone',      'ob_has_phone',      'copy_boolean'),
        ('has_habel',      'ob_has_habel',      'copy_boolean'),
        ('has_ln',         'ob_has_ln',         'copy_boolean'),
        ('has_babtec',     'ob_has_babtec',     'copy_boolean'),
        ('has_gewatec',    'ob_has_gewatec',    'copy_boolean'),
        ('has_provis',     'ob_has_provis',     'copy_boolean'),
        ('has_consense',   'ob_has_consense',   'copy_boolean')
)
INSERT INTO workflow_answer_derivation_rules (
    source_process_type_id,
    target_process_type_id,
    source_answer_key,
    target_answer_key,
    derivation_kind,
    is_active,
    sort_order
)
SELECT
    (SELECT id FROM process_types WHERE key = 'onboarding'),
    (SELECT id FROM process_types WHERE key = 'offboarding'),
    s.source_key,
    s.target_key,
    s.kind,
    TRUE,
    ROW_NUMBER() OVER (ORDER BY s.source_key)
FROM derivation_seed s
ON CONFLICT (source_answer_key, target_answer_key) DO UPDATE
SET
    source_process_type_id = EXCLUDED.source_process_type_id,
    target_process_type_id = EXCLUDED.target_process_type_id,
    derivation_kind        = EXCLUDED.derivation_kind,
    is_active              = EXCLUDED.is_active,
    sort_order             = EXCLUDED.sort_order;

-- =========================
-- Seed: Onboarding → Abteilungswechsel Ableitungsregeln
-- Welche Systeme beim Onboarding eingerichtet wurden, werden beim Abteilungswechsel
-- als "anpassungsbedürftig" vorbelegt.
-- =========================
WITH derivation_seed(source_key, target_key, kind) AS (
    VALUES
        ('has_ad_account', 'dc_ad_group_change',     'copy_boolean'),
        ('has_hardware',   'dc_hardware_change',     'copy_boolean'),
        ('has_habel',      'dc_has_habel',           'copy_boolean'),
        ('has_ln',         'dc_has_ln',              'copy_boolean'),
        ('has_babtec',     'dc_has_babtec',          'copy_boolean'),
        ('has_gewatec',    'dc_has_gewatec',         'copy_boolean'),
        ('has_provis',     'dc_has_provis',          'copy_boolean'),
        ('has_consense',   'dc_has_consense',        'copy_boolean')
)
INSERT INTO workflow_answer_derivation_rules (
    source_process_type_id,
    target_process_type_id,
    source_answer_key,
    target_answer_key,
    derivation_kind,
    is_active,
    sort_order
)
SELECT
    (SELECT id FROM process_types WHERE key = 'onboarding'),
    (SELECT id FROM process_types WHERE key = 'department_change'),
    s.source_key,
    s.target_key,
    s.kind,
    TRUE,
    ROW_NUMBER() OVER (ORDER BY s.source_key)
FROM derivation_seed s
ON CONFLICT (source_answer_key, target_answer_key) DO UPDATE
SET
    source_process_type_id = EXCLUDED.source_process_type_id,
    target_process_type_id = EXCLUDED.target_process_type_id,
    derivation_kind        = EXCLUDED.derivation_kind,
    is_active              = EXCLUDED.is_active,
    sort_order             = EXCLUDED.sort_order;
