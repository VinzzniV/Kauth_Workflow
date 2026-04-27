ALTER TABLE workflow_nodes
    ADD COLUMN IF NOT EXISTS position_x INTEGER;

ALTER TABLE workflow_nodes
    ADD COLUMN IF NOT EXISTS position_y INTEGER;
