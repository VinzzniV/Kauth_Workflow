CREATE TABLE IF NOT EXISTS workflow_definitions (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    definition_key VARCHAR(120) NOT NULL UNIQUE,
    name VARCHAR(220) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS workflow_definition_versions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_definition_id INTEGER NOT NULL REFERENCES workflow_definitions(id) ON DELETE CASCADE,
    version_number INTEGER NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'published', 'retired')),
    name VARCHAR(220),
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_definition_id, version_number)
);

CREATE TABLE IF NOT EXISTS workflow_nodes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_definition_version_id BIGINT NOT NULL REFERENCES workflow_definition_versions(id) ON DELETE CASCADE,
    node_key VARCHAR(120) NOT NULL,
    node_type VARCHAR(32) NOT NULL
        CHECK (node_type IN ('start', 'form', 'approval', 'task', 'decision', 'end')),
    title VARCHAR(220),
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_definition_version_id, node_key)
);

CREATE TABLE IF NOT EXISTS workflow_edges (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_definition_version_id BIGINT NOT NULL REFERENCES workflow_definition_versions(id) ON DELETE CASCADE,
    source_workflow_node_id BIGINT NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    target_workflow_node_id BIGINT NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    priority INTEGER NOT NULL DEFAULT 0,
    condition_expression TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_definition_version_id, source_workflow_node_id, priority)
);

CREATE TABLE IF NOT EXISTS workflow_node_configs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_node_id BIGINT NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    config_json JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_node_id)
);

CREATE INDEX IF NOT EXISTS idx_workflow_definition_versions_definition_id
    ON workflow_definition_versions(workflow_definition_id, version_number DESC);
CREATE INDEX IF NOT EXISTS idx_workflow_nodes_version_id
    ON workflow_nodes(workflow_definition_version_id, sort_order, node_key);
CREATE INDEX IF NOT EXISTS idx_workflow_edges_version_id
    ON workflow_edges(workflow_definition_version_id, source_workflow_node_id, priority);
