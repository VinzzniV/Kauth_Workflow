-- Migrationspfad-Etappe 9a Schritt 6 Sub-A: Temporary-Credentials-Vault.
--
-- Neue Tabelle `temporary_credentials` haelt symmetrisch verschluesselte Initial-Passwoerter
-- (pgcrypto / pgp_sym_encrypt). Der Worker schreibt am Schreib-Punkt direkt in dieselbe
-- DB-Transaktion wie der Job-Success (siehe Sub-Slice 6b). Der `SendWelcomeMailGraphHandler`
-- liest und entschluesselt zur Run-time (siehe Sub-Slice 6c). Es gibt KEINE Mapping-Source,
-- die das Plain-Passwort beim Payload-Build exponieren wuerde — Vault-Grenze ist im Code.
--
-- Idempotenz:
--   * CREATE TABLE IF NOT EXISTS  -> re-run ist no-op auf vorhandene Tabelle
--   * CREATE INDEX IF NOT EXISTS  -> dito
--   * UNIQUE constraint per inline (CREATE TABLE schaltet bei IF NOT EXISTS nicht mehr nach;
--     fuer Bestands-DBs ohne UNIQUE waere das ein eigener manueller Reparatur-Pfad — derzeit
--     trifft das niemanden, weil die Tabelle in keinem Bestand vorkommt)
--   * GRANT-Block ist DO-Block-geschuetzt: nur ausgefuehrt, wenn der jeweilige Postgres-User
--     existiert (frische Dev-DBs ohne kauth_worker werden nicht zur Fehlermeldung gedraengt)
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-13_temporary_credentials_vault.sql

CREATE TABLE IF NOT EXISTS public.temporary_credentials (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    workflow_node_instance_id bigint NOT NULL,
    credential_type character varying(60) NOT NULL,
    encrypted_value bytea NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    expires_at timestamp with time zone NOT NULL,
    first_read_at timestamp with time zone NULL,
    read_count integer NOT NULL DEFAULT 0,
    CONSTRAINT temporary_credentials_pkey PRIMARY KEY (id),
    CONSTRAINT temporary_credentials_type CHECK (credential_type IN ('ad_initial_password')),
    CONSTRAINT temporary_credentials_node_instance_unique
        UNIQUE (workflow_node_instance_id, credential_type),
    CONSTRAINT temporary_credentials_node_instance_fkey
        FOREIGN KEY (workflow_node_instance_id)
        REFERENCES public.workflow_node_instances(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_temporary_credentials_node_instance_id
    ON public.temporary_credentials USING btree (workflow_node_instance_id);

-- Eng-geschnittene Rollen-GRANTs.
-- Der Worker darf nur INSERT (Schreib-Pfad in derselben Tx wie MarkJobSucceededAsync).
-- Der API-User braucht SELECT (Decrypt) + UPDATE (first_read_at/read_count Audit).
-- DELETE ist bewusst nirgendwo — Vault-Cleanup ist eigener Folge-Slice.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kauth_worker') THEN
        EXECUTE 'GRANT INSERT ON public.temporary_credentials TO kauth_worker';
    END IF;

    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kauth_api') THEN
        EXECUTE 'GRANT SELECT, UPDATE ON public.temporary_credentials TO kauth_api';
    END IF;
END
$$;
