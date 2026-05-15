-- Slice 2 (Admin-Gated-Automation, Task-Automation-Binding):
-- task-Nodes koennen kuenftig optional Actions tragen + einen Admin-Role-Slug,
-- der entscheidet, welche Rolle den Plan im Admin-Approval-Schritt (Slice 3)
-- bestaetigen darf.
ALTER TABLE public.workflow_nodes
    ADD COLUMN IF NOT EXISTS automation_admin_role character varying(40);
