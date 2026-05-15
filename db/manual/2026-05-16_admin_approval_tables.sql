-- Slice 3 (Admin-Gated-Automation, Admin-Approval-Endpoint + Re-Auth-Gate):
-- Zwei neue Tabellen für admin-getriggerte task-Node-Ausführung.
--
-- automation_reauth_tokens: one-shot Re-Auth-Token-Store (60s TTL); Approval-Endpoint
-- konsumiert via atomarem UPDATE ... WHERE used_at IS NULL.
--
-- automation_approvals: Audit + Linkage von Approval zu Node-Instanz. UNIQUE auf
-- workflow_node_instance_id verhindert Doppel-Approval; Drift-Tx rollbackt vor INSERT.

CREATE TABLE IF NOT EXISTS public.automation_reauth_tokens (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    token_hash character varying(64) NOT NULL,
    user_id bigint NOT NULL,
    purpose character varying(40) NOT NULL DEFAULT 'automation_approval',
    issued_at timestamp with time zone NOT NULL DEFAULT now(),
    expires_at timestamp with time zone NOT NULL,
    used_at timestamp with time zone,
    CONSTRAINT automation_reauth_tokens_token_hash_uk UNIQUE (token_hash),
    CONSTRAINT automation_reauth_tokens_user_fkey
        FOREIGN KEY (user_id) REFERENCES public.app_users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_automation_reauth_tokens_user_purpose
    ON public.automation_reauth_tokens USING btree (user_id, purpose)
    WHERE used_at IS NULL;

CREATE TABLE IF NOT EXISTS public.automation_approvals (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id bigint NOT NULL,
    workflow_node_instance_id bigint NOT NULL,
    actor_user_id bigint NOT NULL,
    reauth_token_id bigint NOT NULL,
    plan_hash character varying(64) NOT NULL,
    plan_snapshot_json jsonb NOT NULL,
    hash_algorithm character varying(20) NOT NULL DEFAULT 'SHA-256',
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT automation_approvals_node_instance_uk UNIQUE (workflow_node_instance_id),
    CONSTRAINT automation_approvals_workflow_fkey
        FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE,
    CONSTRAINT automation_approvals_node_instance_fkey
        FOREIGN KEY (workflow_node_instance_id) REFERENCES public.workflow_node_instances(id) ON DELETE CASCADE,
    CONSTRAINT automation_approvals_user_fkey
        FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id),
    CONSTRAINT automation_approvals_token_fkey
        FOREIGN KEY (reauth_token_id) REFERENCES public.automation_reauth_tokens(id)
);
