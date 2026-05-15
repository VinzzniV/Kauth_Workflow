-- Admin-Gated-Automation Slice 1 (WhatIf): Tabelle für Worker-Plan-Requests.
--
-- `automation_plan_requests` ist die Kommunikationsschiene zwischen der Linux-API und dem
-- Windows-Worker für Plan-only-Abfragen (kein Schreiben in AD). Die API legt einen Eintrag
-- an (status='pending'), der Worker claimed ihn (SKIP LOCKED), führt eine Read-only-LDAP-
-- Vorschau aus und schreibt das Ergebnis als plan_json zurück.
--
-- Status-Maschine: pending → running → completed | failed
-- Stale-Cleanup: Worker setzt running-Einträge mit claimed_at < NOW()-30s zurück auf pending.
--
-- Idempotenz:
--   * CREATE TABLE IF NOT EXISTS  -> re-run ist no-op
--   * CREATE INDEX IF NOT EXISTS  -> dito
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-15_automation_plan_requests.sql

CREATE TABLE IF NOT EXISTS public.automation_plan_requests (
    id bigint GENERATED ALWAYS AS IDENTITY
        (SEQUENCE NAME public.automation_plan_requests_id_seq
         START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1),
    workflow_instance_uid uuid NOT NULL,
    node_key character varying(80) NOT NULL,
    action_key character varying(80) NOT NULL,
    payload_json jsonb NOT NULL,
    target_runtime character varying(40),
    status character varying(20) NOT NULL DEFAULT 'pending',
    claimed_by character varying(120),
    claimed_at timestamp with time zone,
    plan_json jsonb,
    error_message text,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    completed_at timestamp with time zone,
    CONSTRAINT automation_plan_requests_pkey PRIMARY KEY (id),
    CONSTRAINT automation_plan_requests_status_check
        CHECK (status IN ('pending', 'running', 'completed', 'failed'))
);

CREATE INDEX IF NOT EXISTS idx_automation_plan_requests_pending
    ON public.automation_plan_requests USING btree (target_runtime, created_at)
    WHERE status = 'pending';
