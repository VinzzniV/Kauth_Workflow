-- ============================================================================
-- 01_schema.sql
--
-- Konsolidiertes Schema. Erzeugt aus pg_dump --schema-only nach Anwendung
-- aller Migrationen 03_* bis 67_* gegen den damaligen 01_schema.sql / 02_bootstrap.sql.
--
-- Solange das Projekt nicht produktiv laeuft, wird hier direkt geaendert
-- statt neue Migrationen anzulegen. Die ursprünglichen Migrations-Dateien
-- liegen unter db/_archive/ als Referenz.
--
-- Sobald die Plattform live ist: ab dann nur noch additive Migrationen
-- in db/migrations/ einspielen, dieses File einfrieren.
-- ============================================================================

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pgcrypto; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- Name: ensure_rotation_plan_source_workflow_completed(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.ensure_rotation_plan_source_workflow_completed() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    workflow_status VARCHAR(40);
BEGIN
    SELECT status
    INTO workflow_status
    FROM workflows
    WHERE id = NEW.source_workflow_id;

    IF workflow_status IS NULL THEN
        RETURN NEW;
    END IF;

    IF workflow_status <> 'completed' THEN
        RAISE EXCEPTION 'rotation_plans.source_workflow_id % must reference a completed workflow', NEW.source_workflow_id;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: sync_people_department_to_user(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.sync_people_department_to_user() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
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
$$;


--
-- Name: sync_user_department_to_people(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.sync_user_department_to_people() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
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
$$;


--
-- Name: upsert_linearized_workflow_definition(text, text, text, text, text, text, jsonb, jsonb); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.upsert_linearized_workflow_definition(p_definition_key text, p_definition_name text, p_definition_description text, p_version_name text, p_version_description text, p_nodes jsonb, p_edges jsonb) RETURNS void
    LANGUAGE plpgsql
    AS $$
DECLARE
    definition_id INTEGER;
    definition_version_id BIGINT;
    node_item RECORD;
    edge_item RECORD;
    created_node_id BIGINT;
    node_ids JSONB := '{}'::jsonb;
BEGIN
    INSERT INTO workflow_definitions (
        definition_key,
        name,
        description,
        updated_at
    )
    VALUES (
        LOWER(TRIM(p_definition_key)),
        p_definition_name,
        p_definition_description,
        NOW()
    )
    ON CONFLICT (definition_key) DO UPDATE
    SET
        name = EXCLUDED.name,
        description = EXCLUDED.description,
        updated_at = NOW()
    RETURNING id
    INTO definition_id;

    SELECT id
    INTO definition_version_id
    FROM workflow_definition_versions
    WHERE workflow_definition_id = definition_id
      AND status = 'published'
    ORDER BY version_number DESC, id DESC
    LIMIT 1;

    IF definition_version_id IS NULL THEN
        SELECT id
        INTO definition_version_id
        FROM workflow_definition_versions
        WHERE workflow_definition_id = definition_id
        ORDER BY version_number DESC, id DESC
        LIMIT 1;
    END IF;

    IF definition_version_id IS NULL THEN
        INSERT INTO workflow_definition_versions (
            workflow_definition_id,
            version_number,
            status,
            name,
            description,
            updated_at,
            published_at
        )
        VALUES (
            definition_id,
            1,
            'published',
            p_version_name,
            p_version_description,
            NOW(),
            NOW()
        )
        RETURNING id
        INTO definition_version_id;
    END IF;

    UPDATE workflow_definition_versions
    SET
        status = CASE WHEN id = definition_version_id THEN 'published' ELSE 'retired' END,
        name = CASE WHEN id = definition_version_id THEN p_version_name ELSE name END,
        description = CASE WHEN id = definition_version_id THEN p_version_description ELSE description END,
        updated_at = NOW(),
        published_at = CASE WHEN id = definition_version_id THEN COALESCE(published_at, NOW()) ELSE published_at END
    WHERE workflow_definition_id = definition_id;

    DELETE FROM workflow_edges
    WHERE workflow_definition_version_id = definition_version_id;

    DELETE FROM workflow_node_configs
    WHERE workflow_node_id IN (
        SELECT id
        FROM workflow_nodes
        WHERE workflow_definition_version_id = definition_version_id
    );

    DELETE FROM workflow_nodes
    WHERE workflow_definition_version_id = definition_version_id;

    FOR node_item IN
        SELECT *
        FROM jsonb_to_recordset(p_nodes) AS x(
            node_key TEXT,
            node_type TEXT,
            title TEXT,
            sort_order INTEGER,
            config_json JSONB
        )
    LOOP
        INSERT INTO workflow_nodes (
            workflow_definition_version_id,
            node_key,
            node_type,
            title,
            sort_order
        )
        VALUES (
            definition_version_id,
            LOWER(TRIM(node_item.node_key)),
            LOWER(TRIM(node_item.node_type)),
            node_item.title,
            node_item.sort_order
        )
        RETURNING id
        INTO created_node_id;

        node_ids := jsonb_set(
            node_ids,
            ARRAY[LOWER(TRIM(node_item.node_key))],
            to_jsonb(created_node_id),
            TRUE);

        IF node_item.config_json IS NOT NULL AND node_item.config_json <> 'null'::jsonb THEN
            INSERT INTO workflow_node_configs (
                workflow_node_id,
                config_json
            )
            VALUES (
                created_node_id,
                node_item.config_json
            );
        END IF;
    END LOOP;

    FOR edge_item IN
        SELECT *
        FROM jsonb_to_recordset(p_edges) AS x(
            source_node_key TEXT,
            target_node_key TEXT,
            priority INTEGER,
            condition_expression TEXT
        )
    LOOP
        INSERT INTO workflow_edges (
            workflow_definition_version_id,
            source_workflow_node_id,
            target_workflow_node_id,
            priority,
            condition_expression
        )
        VALUES (
            definition_version_id,
            (node_ids ->> LOWER(TRIM(edge_item.source_node_key)))::BIGINT,
            (node_ids ->> LOWER(TRIM(edge_item.target_node_key)))::BIGINT,
            edge_item.priority,
            edge_item.condition_expression
        );
    END LOOP;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: action_definitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.action_definitions (
    id bigint NOT NULL,
    action_key character varying(120) NOT NULL,
    name character varying(220) NOT NULL,
    description text,
    handler_type character varying(120) NOT NULL,
    parameter_schema_json jsonb,
    is_active boolean DEFAULT true NOT NULL,
    requires_approval boolean DEFAULT false NOT NULL,
    is_idempotent boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: action_definitions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.action_definitions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.action_definitions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_group_responsibilities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_group_responsibilities (
    app_group_id integer NOT NULL,
    app_responsibility_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_group_roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_group_roles (
    app_group_id integer NOT NULL,
    app_role_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_groups; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_groups (
    id integer NOT NULL,
    group_key character varying(120) NOT NULL,
    name character varying(160) NOT NULL,
    description text,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_groups_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_groups ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_groups_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_permissions (
    id integer NOT NULL,
    permission_key character varying(160) NOT NULL,
    name character varying(180) NOT NULL,
    description text,
    scope_kind character varying(32) DEFAULT 'global'::character varying NOT NULL,
    category character varying(80) DEFAULT 'general'::character varying NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_app_permissions_scope_kind CHECK (((scope_kind)::text = ANY ((ARRAY['global'::character varying, 'department'::character varying])::text[])))
);


--
-- Name: app_permissions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_permissions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_permissions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_responsibilities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_responsibilities (
    id integer NOT NULL,
    department_id integer,
    responsibility_key character varying(120) NOT NULL,
    system_key character varying(64),
    name character varying(160) NOT NULL,
    responsibility_type character varying(32) NOT NULL,
    description text,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT app_responsibilities_responsibility_type_check CHECK (((responsibility_type)::text = ANY ((ARRAY['process'::character varying, 'department_lead'::character varying, 'application'::character varying])::text[]))),
    CONSTRAINT chk_app_responsibilities_application_system_key CHECK ((((responsibility_type)::text <> 'application'::text) OR (system_key IS NOT NULL)))
);


--
-- Name: app_responsibilities_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_responsibilities ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_responsibilities_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_role_answer_default_options; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_role_answer_default_options (
    app_role_id integer NOT NULL,
    answer_definition_id integer NOT NULL,
    answer_option_id integer NOT NULL,
    is_default boolean DEFAULT false NOT NULL
);


--
-- Name: app_role_answer_defaults; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_role_answer_defaults (
    workflow_definition_id integer NOT NULL,
    app_role_id integer NOT NULL,
    answer_definition_id integer NOT NULL,
    is_recommended boolean DEFAULT true NOT NULL,
    is_default boolean DEFAULT false NOT NULL,
    default_value_boolean boolean,
    default_value_text text,
    default_value_number numeric(12,2),
    sort_order integer DEFAULT 0 NOT NULL
);


--
-- Name: app_role_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_role_permissions (
    app_role_id integer NOT NULL,
    app_permission_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_roles (
    id integer NOT NULL,
    department_id integer,
    role_key character varying(120) NOT NULL,
    name character varying(160) NOT NULL,
    role_kind character varying(32) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT app_roles_role_kind_check CHECK (((role_kind)::text = ANY ((ARRAY['position'::character varying, 'system'::character varying])::text[])))
);


--
-- Name: app_roles_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_roles ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_roles_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_user_groups; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_user_groups (
    app_user_id bigint NOT NULL,
    app_group_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_user_permission_overrides; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_user_permission_overrides (
    id bigint NOT NULL,
    app_user_id bigint NOT NULL,
    app_permission_id integer NOT NULL,
    effect character varying(16) NOT NULL,
    scope character varying(32) DEFAULT 'global'::character varying NOT NULL,
    scope_department_id integer,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_app_user_permission_overrides_effect CHECK (((effect)::text = ANY ((ARRAY['allow'::character varying, 'deny'::character varying])::text[]))),
    CONSTRAINT chk_app_user_permission_overrides_scope CHECK (((scope)::text = ANY ((ARRAY['global'::character varying, 'department'::character varying])::text[])))
);


--
-- Name: app_user_permission_overrides_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_user_permission_overrides ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_user_permission_overrides_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: app_user_responsibilities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_user_responsibilities (
    app_user_id bigint NOT NULL,
    app_responsibility_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_user_roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_user_roles (
    app_user_id bigint NOT NULL,
    app_role_id integer NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: app_users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.app_users (
    id bigint NOT NULL,
    external_key character varying(120),
    department_id integer,
    display_name character varying(180) NOT NULL,
    email character varying(320) NOT NULL,
    notification_email character varying(320),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    entra_object_id uuid,
    directory_synced boolean DEFAULT false NOT NULL,
    last_directory_synced_at timestamp with time zone,
    department_source character varying(32) DEFAULT 'local'::character varying NOT NULL,
    department_override_active boolean DEFAULT false NOT NULL,
    CONSTRAINT chk_app_users_department_source CHECK (((department_source)::text = ANY ((ARRAY['local'::character varying, 'directory'::character varying, 'override'::character varying, 'unassigned'::character varying])::text[])))
);


--
-- Name: app_users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.app_users ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.app_users_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auth_permission_audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auth_permission_audit_log (
    id bigint NOT NULL,
    actor_user_id bigint,
    event_type character varying(40) NOT NULL,
    entity_type character varying(64) NOT NULL,
    detail text,
    old_value jsonb,
    new_value jsonb,
    reason text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: auth_permission_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auth_permission_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.auth_permission_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: person_match_audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.person_match_audit_log (
    id bigint NOT NULL,
    matched_person_id bigint,
    app_user_id bigint,
    directory_identity_id bigint,
    employee_number integer,
    match_strategy character varying(40) NOT NULL,
    match_score numeric(5, 2),
    fallback_used boolean DEFAULT false NOT NULL,
    source character varying(40) NOT NULL,
    detail jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: person_match_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.person_match_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.person_match_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: person_match_audit_log_matched_person_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX person_match_audit_log_matched_person_idx ON public.person_match_audit_log USING btree (matched_person_id) WHERE (matched_person_id IS NOT NULL);


--
-- Name: person_match_audit_log_created_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX person_match_audit_log_created_idx ON public.person_match_audit_log USING btree (created_at DESC);


--
-- Name: person_match_audit_log_strategy_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX person_match_audit_log_strategy_idx ON public.person_match_audit_log USING btree (match_strategy, created_at DESC);


--
-- Name: person_match_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.person_match_audit_log
    ADD CONSTRAINT person_match_audit_log_pkey PRIMARY KEY (id);


--
-- Name: automation_job_attempts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.automation_job_attempts (
    id bigint NOT NULL,
    automation_job_id bigint NOT NULL,
    attempt_number integer NOT NULL,
    status character varying(32) NOT NULL,
    error_message text,
    started_at timestamp with time zone DEFAULT now() NOT NULL,
    completed_at timestamp with time zone,
    CONSTRAINT automation_job_attempts_status_check CHECK (((status)::text = ANY ((ARRAY['running'::character varying, 'succeeded'::character varying, 'failed'::character varying])::text[])))
);


--
-- Name: automation_job_attempts_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.automation_job_attempts ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.automation_job_attempts_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: automation_job_logs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.automation_job_logs (
    id bigint NOT NULL,
    automation_job_id bigint NOT NULL,
    level character varying(16) NOT NULL,
    message text NOT NULL,
    details_json jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT automation_job_logs_level_check CHECK (((level)::text = ANY ((ARRAY['debug'::character varying, 'info'::character varying, 'warning'::character varying, 'error'::character varying])::text[])))
);


--
-- Name: automation_job_logs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.automation_job_logs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.automation_job_logs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: automation_jobs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.automation_jobs (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    workflow_node_instance_id bigint NOT NULL,
    workflow_node_action_id bigint NOT NULL,
    action_definition_id bigint NOT NULL,
    status character varying(32) NOT NULL,
    payload_json jsonb,
    available_at timestamp with time zone DEFAULT now() NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    CONSTRAINT automation_jobs_status_check CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'running'::character varying, 'succeeded'::character varying, 'failed'::character varying, 'cancelled'::character varying])::text[])))
);


--
-- Name: automation_jobs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.automation_jobs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.automation_jobs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: department_action_templates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.department_action_templates (
    id integer NOT NULL,
    department_id integer NOT NULL,
    trigger_type character varying(16) NOT NULL,
    title character varying(220) NOT NULL,
    description text,
    task_type character varying(32) NOT NULL,
    default_responsibility_id integer,
    due_offset_days integer NOT NULL,
    reminder_offset_days integer,
    is_automatable boolean DEFAULT false NOT NULL,
    automation_key character varying(120),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_department_action_templates_automation_key CHECK ((is_automatable OR (automation_key IS NULL))),
    CONSTRAINT chk_department_action_templates_reminder_offset CHECK (((reminder_offset_days IS NULL) OR (reminder_offset_days >= 0))),
    CONSTRAINT department_action_templates_task_type_check CHECK (((task_type)::text = ANY ((ARRAY['manual'::character varying, 'technical'::character varying, 'approval'::character varying, 'information'::character varying])::text[]))),
    CONSTRAINT department_action_templates_trigger_type_check CHECK (((trigger_type)::text = ANY ((ARRAY['enter'::character varying, 'exit'::character varying])::text[])))
);


--
-- Name: department_action_templates_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.department_action_templates ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.department_action_templates_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: department_settings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.department_settings (
    department_id integer NOT NULL,
    department_lead_person_id bigint,
    requirement_approver_person_id bigint,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: departments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.departments (
    id integer NOT NULL,
    name character varying(120) NOT NULL
);


--
-- Name: departments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.departments ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.departments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: directory_group_members; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_group_members (
    directory_group_id integer NOT NULL,
    directory_identity_id bigint NOT NULL,
    synced_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: directory_group_role_mappings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_group_role_mappings (
    id integer NOT NULL,
    directory_group_id integer NOT NULL,
    app_role_id integer NOT NULL,
    scope character varying(64) DEFAULT 'global'::character varying NOT NULL,
    scope_department_id integer,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_directory_group_role_mappings_scope CHECK (((scope)::text = ANY ((ARRAY['global'::character varying, 'department'::character varying])::text[])))
);


--
-- Name: directory_group_role_mappings_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.directory_group_role_mappings ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.directory_group_role_mappings_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: directory_groups; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_groups (
    id integer NOT NULL,
    external_group_id uuid NOT NULL,
    display_name character varying(260) NOT NULL,
    description text,
    source_system character varying(64) DEFAULT 'entra'::character varying NOT NULL,
    last_synced_at timestamp with time zone DEFAULT now() NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: directory_groups_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.directory_groups ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.directory_groups_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: directory_identities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_identities (
    id bigint NOT NULL,
    entra_object_id uuid NOT NULL,
    onprem_object_guid uuid,
    user_principal_name character varying(320) NOT NULL,
    mail character varying(320),
    display_name character varying(180) NOT NULL,
    account_enabled boolean DEFAULT true NOT NULL,
    source_system character varying(64) DEFAULT 'entra'::character varying NOT NULL,
    is_managed_externally boolean DEFAULT true NOT NULL,
    app_user_id bigint,
    last_synced_at timestamp with time zone DEFAULT now() NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    department_name character varying(120),
    employee_number integer
);


--
-- Name: directory_identities_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.directory_identities ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.directory_identities_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: directory_mapping_audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_mapping_audit_log (
    id bigint NOT NULL,
    actor_user_id bigint,
    event_type character varying(40) NOT NULL,
    entity_type character varying(40) NOT NULL,
    detail text,
    old_value jsonb,
    new_value jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: directory_mapping_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.directory_mapping_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.directory_mapping_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: directory_sync_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.directory_sync_log (
    id bigint NOT NULL,
    sync_type character varying(40) NOT NULL,
    status character varying(20) NOT NULL,
    groups_synced integer DEFAULT 0 NOT NULL,
    identities_synced integer DEFAULT 0 NOT NULL,
    memberships_synced integer DEFAULT 0 NOT NULL,
    error_message text,
    started_at timestamp with time zone DEFAULT now() NOT NULL,
    completed_at timestamp with time zone
);


--
-- Name: directory_sync_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.directory_sync_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.directory_sync_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: notification_email_settings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notification_email_settings (
    id smallint DEFAULT 1 NOT NULL,
    enabled boolean DEFAULT false NOT NULL,
    sender_email character varying(320),
    frontend_base_url character varying(500) DEFAULT 'http://localhost:5173'::character varying NOT NULL,
    test_recipient_email character varying(320),
    sandbox_redirect_email character varying(320),
    notify_on_workflow_created boolean DEFAULT true NOT NULL,
    notify_on_task_ready boolean DEFAULT true NOT NULL,
    notify_on_workflow_completed boolean DEFAULT true NOT NULL,
    last_test_status character varying(16) DEFAULT 'never'::character varying NOT NULL,
    last_test_at timestamp with time zone,
    last_error text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT notification_email_settings_id_check CHECK ((id = 1)),
    CONSTRAINT notification_email_settings_last_test_status_check CHECK (((last_test_status)::text = ANY ((ARRAY['never'::character varying, 'succeeded'::character varying, 'failed'::character varying, 'disabled'::character varying])::text[])))
);


--
-- Name: notification_templates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notification_templates (
    template_key character varying(64) NOT NULL,
    display_name character varying(160) NOT NULL,
    trigger_description text NOT NULL,
    subject_template text NOT NULL,
    body_template text NOT NULL,
    is_system_locked boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: people; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.people (
    id bigint NOT NULL,
    app_user_id bigint,
    department_id integer,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    directory_identity_id bigint,
    first_name character varying(120),
    last_name character varying(120),
    employee_number integer,
    badge_number integer,
    employment_status character varying(32),
    entry_date date,
    exit_date date,
    current_position_role_id integer
);


--
-- Name: people_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.people ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.people_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_audit_log (
    id bigint NOT NULL,
    rotation_plan_id bigint NOT NULL,
    rotation_station_id bigint,
    generated_task_id bigint,
    actor_user_id bigint,
    event_type character varying(80) NOT NULL,
    old_value jsonb,
    new_value jsonb,
    detail text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: rotation_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_generated_tasks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_generated_tasks (
    id bigint NOT NULL,
    rotation_plan_id bigint NOT NULL,
    rotation_station_id bigint,
    person_id bigint NOT NULL,
    department_id integer NOT NULL,
    template_id integer,
    trigger_type character varying(16) NOT NULL,
    anchor_date date NOT NULL,
    title character varying(220) NOT NULL,
    description text,
    task_type character varying(32) NOT NULL,
    responsibility_id integer,
    due_date date,
    status character varying(32) DEFAULT 'open'::character varying NOT NULL,
    completion_note text,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT rotation_generated_tasks_status_check CHECK (((status)::text = ANY ((ARRAY['open'::character varying, 'in_progress'::character varying, 'completed'::character varying, 'failed'::character varying, 'cancelled'::character varying])::text[]))),
    CONSTRAINT rotation_generated_tasks_task_type_check CHECK (((task_type)::text = ANY ((ARRAY['manual'::character varying, 'technical'::character varying, 'approval'::character varying, 'information'::character varying])::text[]))),
    CONSTRAINT rotation_generated_tasks_trigger_type_check CHECK (((trigger_type)::text = ANY ((ARRAY['enter'::character varying, 'exit'::character varying])::text[])))
);


--
-- Name: rotation_generated_tasks_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_generated_tasks ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_generated_tasks_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_notifications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_notifications (
    id bigint NOT NULL,
    rotation_plan_id bigint NOT NULL,
    rotation_station_id bigint,
    generated_task_id bigint,
    notification_type character varying(32) NOT NULL,
    recipient_email character varying(320) NOT NULL,
    recipient_user_id bigint,
    subject character varying(220) NOT NULL,
    payload_json jsonb,
    status character varying(32) DEFAULT 'pending'::character varying NOT NULL,
    attempts integer DEFAULT 0 NOT NULL,
    last_error text,
    sent_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT rotation_notifications_attempts_check CHECK ((attempts >= 0)),
    CONSTRAINT rotation_notifications_notification_type_check CHECK (((notification_type)::text = ANY ((ARRAY['upcoming_change'::character varying, 'reminder'::character varying, 'overdue'::character varying, 'escalation'::character varying])::text[]))),
    CONSTRAINT rotation_notifications_status_check CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'sent'::character varying, 'failed'::character varying, 'disabled'::character varying])::text[])))
);


--
-- Name: rotation_notifications_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_notifications ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_notifications_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_plans; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_plans (
    id bigint NOT NULL,
    person_id bigint NOT NULL,
    source_workflow_id bigint NOT NULL,
    title character varying(220) NOT NULL,
    status character varying(32) DEFAULT 'draft'::character varying NOT NULL,
    created_by_user_id bigint,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT rotation_plans_status_check CHECK (((status)::text = ANY ((ARRAY['draft'::character varying, 'active'::character varying, 'completed'::character varying, 'archived'::character varying])::text[])))
);


--
-- Name: rotation_plans_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_plans ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_plans_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_stations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_stations (
    id bigint NOT NULL,
    rotation_plan_id bigint NOT NULL,
    department_id integer NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    order_index integer DEFAULT 0 NOT NULL,
    location character varying(160),
    notes text,
    status character varying(32) DEFAULT 'planned'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_rotation_stations_date_range CHECK ((end_date >= start_date)),
    CONSTRAINT rotation_stations_order_index_check CHECK ((order_index >= 0)),
    CONSTRAINT rotation_stations_status_check CHECK (((status)::text = ANY ((ARRAY['planned'::character varying, 'active'::character varying, 'completed'::character varying, 'cancelled'::character varying])::text[])))
);


--
-- Name: rotation_stations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_stations ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_stations_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_task_assignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_task_assignments (
    id bigint NOT NULL,
    rotation_generated_task_id bigint NOT NULL,
    assignee_user_id bigint,
    assignee_responsibility_id integer,
    assignment_type character varying(32) NOT NULL,
    is_primary boolean DEFAULT false NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL,
    completed_at timestamp with time zone,
    CONSTRAINT rotation_task_assignments_assignment_type_check CHECK (((assignment_type)::text = ANY ((ARRAY['user'::character varying, 'responsibility'::character varying])::text[]))),
    CONSTRAINT rotation_task_assignments_check CHECK (((((assignment_type)::text = 'user'::text) AND (assignee_user_id IS NOT NULL) AND (assignee_responsibility_id IS NULL)) OR (((assignment_type)::text = 'responsibility'::text) AND (assignee_responsibility_id IS NOT NULL))))
);


--
-- Name: rotation_task_assignments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_task_assignments ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_task_assignments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rotation_task_comments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rotation_task_comments (
    id bigint NOT NULL,
    rotation_generated_task_id bigint NOT NULL,
    author_user_id bigint,
    comment_text text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: rotation_task_comments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.rotation_task_comments ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.rotation_task_comments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: system_event_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.system_event_log (
    id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    severity character varying(16) NOT NULL,
    source character varying(32) NOT NULL,
    category character varying(64) NOT NULL,
    event_key character varying(128) NOT NULL,
    message text NOT NULL,
    user_message text,
    actor_user_id bigint,
    client_route character varying(500),
    client_function character varying(160),
    http_method character varying(16),
    http_path character varying(500),
    http_status integer,
    trace_identifier character varying(128),
    workflow_uid uuid,
    rotation_plan_id bigint,
    task_ref character varying(160),
    entity_type character varying(64),
    entity_id character varying(128),
    details_json jsonb,
    CONSTRAINT system_event_log_severity_check CHECK (((severity)::text = ANY ((ARRAY['info'::character varying, 'warning'::character varying, 'error'::character varying])::text[]))),
    CONSTRAINT system_event_log_source_check CHECK (((source)::text = ANY ((ARRAY['frontend'::character varying, 'api'::character varying, 'system'::character varying, 'mail'::character varying, 'entra'::character varying, 'directory'::character varying, 'automation'::character varying, 'workflow'::character varying, 'task'::character varying, 'rotation'::character varying, 'admin'::character varying])::text[])))
);


--
-- Name: system_event_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.system_event_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.system_event_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: system_responsibilities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.system_responsibilities (
    id integer NOT NULL,
    system_key character varying(64) NOT NULL,
    app_responsibility_id integer NOT NULL,
    responsible_person_id bigint,
    responsible_department_id integer,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT system_responsibilities_check CHECK (((responsible_person_id IS NOT NULL) OR (responsible_department_id IS NOT NULL)))
);


--
-- Name: system_responsibilities_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.system_responsibilities ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.system_responsibilities_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: task_assignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.task_assignments (
    id bigint NOT NULL,
    workflow_task_id bigint NOT NULL,
    assignee_user_id bigint,
    assignee_responsibility_id integer,
    assignment_type character varying(16) DEFAULT 'responsibility'::character varying NOT NULL,
    is_primary boolean DEFAULT true NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL,
    completed_at timestamp with time zone,
    CONSTRAINT chk_task_assignments_target_matches_type CHECK (((((assignment_type)::text = 'user'::text) AND (assignee_user_id IS NOT NULL) AND (assignee_responsibility_id IS NULL)) OR (((assignment_type)::text = 'responsibility'::text) AND (assignee_responsibility_id IS NOT NULL) AND (assignee_user_id IS NULL)))),
    CONSTRAINT task_assignments_assignment_type_check CHECK (((assignment_type)::text = ANY ((ARRAY['responsibility'::character varying, 'user'::character varying])::text[])))
);


--
-- Name: task_assignments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.task_assignments ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.task_assignments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);





--
-- Name: workflow_answer_definitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_definitions (
    id integer NOT NULL,
    workflow_definition_id integer NOT NULL,
    answer_key character varying(120) NOT NULL,
    title character varying(180) NOT NULL,
    category character varying(80) DEFAULT 'general'::character varying NOT NULL,
    description text NOT NULL,
    icon_key character varying(80) DEFAULT 'berechtigungen'::character varying NOT NULL,
    input_type character varying(32) NOT NULL,
    is_required boolean DEFAULT false NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    CONSTRAINT workflow_answer_definitions_input_type_check CHECK (((input_type)::text = ANY ((ARRAY['boolean'::character varying, 'text'::character varying, 'select'::character varying, 'multi_select'::character varying])::text[])))
);


--
-- Name: workflow_answer_definitions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_definitions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_definitions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_derivation_rules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_derivation_rules (
    id integer NOT NULL,
    source_workflow_definition_id integer NOT NULL,
    target_workflow_definition_id integer NOT NULL,
    source_answer_key character varying(120) NOT NULL,
    target_answer_key character varying(120) NOT NULL,
    derivation_kind character varying(40) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_answer_derivation_rules_check CHECK ((source_workflow_definition_id <> target_workflow_definition_id)),
    CONSTRAINT workflow_answer_derivation_rules_derivation_kind_check CHECK (((derivation_kind)::text = ANY ((ARRAY['copy_boolean'::character varying, 'copy_text'::character varying, 'copy_number'::character varying, 'copy_selected_option'::character varying])::text[])))
);


--
-- Name: workflow_answer_derivation_rules_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_derivation_rules ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_derivation_rules_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_options; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_options (
    id integer NOT NULL,
    answer_definition_id integer NOT NULL,
    option_key character varying(120) NOT NULL,
    option_value character varying(180) NOT NULL,
    option_label character varying(180) NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL
);


--
-- Name: workflow_answer_options_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_options ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_options_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_reset_rules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_reset_rules (
    id bigint NOT NULL,
    answer_definition_id integer NOT NULL,
    trigger_kind character varying(40) NOT NULL,
    target_answer_definition_id integer NOT NULL,
    clear_boolean boolean DEFAULT false NOT NULL,
    clear_text boolean DEFAULT false NOT NULL,
    clear_number boolean DEFAULT false NOT NULL,
    clear_selected_option boolean DEFAULT false NOT NULL,
    clear_selected_options boolean DEFAULT false NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_answer_reset_rules_check CHECK ((answer_definition_id <> target_answer_definition_id)),
    CONSTRAINT workflow_answer_reset_rules_check1 CHECK ((clear_boolean OR clear_text OR clear_number OR clear_selected_option OR clear_selected_options)),
    CONSTRAINT workflow_answer_reset_rules_trigger_kind_check CHECK (((trigger_kind)::text = ANY ((ARRAY['when_not_true'::character varying, 'single_select_mismatch'::character varying])::text[])))
);


--
-- Name: workflow_answer_reset_rules_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_reset_rules ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_reset_rules_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_selected_options; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_selected_options (
    id bigint NOT NULL,
    workflow_answer_id bigint NOT NULL,
    answer_option_id integer NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_answer_selected_options_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_selected_options ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_selected_options_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_single_select_keep_values; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_single_select_keep_values (
    id bigint NOT NULL,
    answer_definition_id integer NOT NULL,
    option_value character varying(180) NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_answer_single_select_keep_values_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_single_select_keep_values ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_single_select_keep_values_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answer_validation_rules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_validation_rules (
    answer_definition_id integer NOT NULL,
    validation_kind character varying(40) NOT NULL,
    message text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_answer_validation_rules_validation_kind_check CHECK (((validation_kind)::text = ANY ((ARRAY['text_required'::character varying, 'single_select_required'::character varying, 'multi_select_required'::character varying])::text[])))
);


--
-- Name: workflow_answer_visibility_rules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answer_visibility_rules (
    id bigint NOT NULL,
    answer_definition_id integer NOT NULL,
    dependency_answer_definition_id integer NOT NULL,
    dependency_kind character varying(40) NOT NULL,
    expected_value_text text,
    missing_result boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_answer_visibility_rules_check CHECK ((answer_definition_id <> dependency_answer_definition_id)),
    CONSTRAINT workflow_answer_visibility_rules_dependency_kind_check CHECK (((dependency_kind)::text = ANY ((ARRAY['boolean_true'::character varying, 'selected_option_value'::character varying])::text[])))
);


--
-- Name: workflow_answer_visibility_rules_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answer_visibility_rules ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answer_visibility_rules_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_answers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_answers (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    answer_definition_id integer NOT NULL,
    answer_key character varying(120) NOT NULL,
    input_type character varying(32) NOT NULL,
    value_boolean boolean,
    value_text text,
    value_number numeric(12,2),
    selected_option_id integer,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_answers_input_type_check CHECK (((input_type)::text = ANY ((ARRAY['boolean'::character varying, 'text'::character varying, 'select'::character varying, 'multi_select'::character varying])::text[])))
);


--
-- Name: workflow_answers_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_answers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_answers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_audit_log (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    task_id bigint,
    actor_user_id bigint,
    event_type character varying(80) NOT NULL,
    old_value character varying(80),
    new_value character varying(80),
    detail text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_definition_versions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_definition_versions (
    id bigint NOT NULL,
    workflow_definition_id integer NOT NULL,
    version_number integer NOT NULL,
    status character varying(32) DEFAULT 'draft'::character varying NOT NULL,
    name character varying(220),
    description text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    published_at timestamp with time zone,
    CONSTRAINT workflow_definition_versions_status_check CHECK (((status)::text = ANY ((ARRAY['draft'::character varying, 'published'::character varying, 'retired'::character varying])::text[])))
);


--
-- Name: workflow_definition_versions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_definition_versions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_definition_versions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_definitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_definitions (
    id integer NOT NULL,
    definition_key character varying(120) NOT NULL,
    name character varying(220) NOT NULL,
    description text,
    allows_manager_creation boolean DEFAULT false NOT NULL,
    requires_supervisor_step boolean DEFAULT false NOT NULL,
    requires_target_person boolean DEFAULT false NOT NULL,
    approval_spec_key character varying(120),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_definitions_supervisor_step_requires_approval_spec CHECK (((NOT requires_supervisor_step) OR (approval_spec_key IS NOT NULL)))
);


--
-- Name: workflow_definitions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_definitions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_definitions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_edges; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_edges (
    id bigint NOT NULL,
    workflow_definition_version_id bigint NOT NULL,
    source_workflow_node_id bigint NOT NULL,
    target_workflow_node_id bigint NOT NULL,
    priority integer DEFAULT 0 NOT NULL,
    condition_expression text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_edges_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_edges ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_edges_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_links; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_links (
    id bigint NOT NULL,
    source_workflow_id bigint NOT NULL,
    target_workflow_id bigint NOT NULL,
    link_type character varying(40) NOT NULL,
    created_by_user_id bigint,
    notes text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_links_check CHECK ((source_workflow_id <> target_workflow_id)),
    CONSTRAINT workflow_links_link_type_check CHECK (((link_type)::text = ANY ((ARRAY['derived_from'::character varying, 'supersedes'::character varying, 'related'::character varying])::text[])))
);


--
-- Name: workflow_links_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_links ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_links_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_node_actions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_node_actions (
    id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    action_definition_id bigint NOT NULL,
    input_mapping_json jsonb,
    execution_order integer DEFAULT 0 NOT NULL,
    on_error_behavior character varying(32) DEFAULT 'fail_workflow'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_node_actions_on_error_behavior_check CHECK (((on_error_behavior)::text = 'fail_workflow'::text))
);


--
-- Name: workflow_node_actions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_node_actions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_actions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_node_configs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_node_configs (
    id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    config_json jsonb NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_node_configs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_node_configs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_configs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_node_instances; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_node_instances (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    status character varying(32) NOT NULL,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    result_json jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_node_instances_status_check CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'active'::character varying, 'done'::character varying, 'failed'::character varying, 'cancelled'::character varying])::text[])))
);


--
-- Name: workflow_node_instances_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_node_instances ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_instances_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_nodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_nodes (
    id bigint NOT NULL,
    workflow_definition_version_id bigint NOT NULL,
    node_key character varying(120) NOT NULL,
    node_type character varying(32) NOT NULL,
    title character varying(220),
    sort_order integer DEFAULT 0 NOT NULL,
    position_x integer,
    position_y integer,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_nodes_node_type_check CHECK (((node_type)::text = ANY ((ARRAY['start'::character varying, 'form'::character varying, 'approval'::character varying, 'task'::character varying, 'decision'::character varying, 'parallel_split'::character varying, 'parallel_join'::character varying, 'automation'::character varying, 'measure_provision'::character varying, 'measure_deprovision'::character varying, 'measure_change'::character varying, 'measure_rename'::character varying, 'end'::character varying])::text[])))
);


--
-- Name: workflow_nodes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_nodes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_nodes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_notifications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_notifications (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    workflow_task_id bigint,
    recipient_user_id bigint,
    target_email character varying(320) NOT NULL,
    target_name character varying(180) NOT NULL,
    notification_type character varying(80) DEFAULT 'workflow_created'::character varying NOT NULL,
    status character varying(32) DEFAULT 'pending'::character varying NOT NULL,
    attempts integer DEFAULT 0 NOT NULL,
    last_error text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    sent_at timestamp with time zone,
    CONSTRAINT workflow_notifications_status_check CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'sent'::character varying, 'failed'::character varying, 'disabled'::character varying])::text[])))
);


--
-- Name: workflow_notifications_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_notifications ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_notifications_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_runtime_events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_runtime_events (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    workflow_node_instance_id bigint,
    event_type character varying(80) NOT NULL,
    payload_json jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_runtime_events_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_runtime_events ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_runtime_events_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_task_comments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_task_comments (
    id bigint NOT NULL,
    workflow_task_id bigint NOT NULL,
    author_user_id bigint,
    comment_text text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: workflow_task_comments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_task_comments ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_task_comments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_task_dependencies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_task_dependencies (
    id bigint NOT NULL,
    workflow_task_id bigint NOT NULL,
    depends_on_workflow_task_id bigint NOT NULL,
    required_status character varying(32) DEFAULT 'done'::character varying NOT NULL,
    CONSTRAINT workflow_task_dependencies_check CHECK ((workflow_task_id <> depends_on_workflow_task_id)),
    CONSTRAINT workflow_task_dependencies_required_status_check CHECK (((required_status)::text = ANY ((ARRAY['open'::character varying, 'ready'::character varying, 'in_progress'::character varying, 'blocked'::character varying, 'done'::character varying])::text[])))
);


--
-- Name: workflow_task_dependencies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_task_dependencies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_task_dependencies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflow_tasks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflow_tasks (
    id bigint NOT NULL,
    workflow_id bigint NOT NULL,
    workflow_node_task_spec_id bigint,
    task_key character varying(120) NOT NULL,
    title character varying(220) NOT NULL,
    category character varying(80) NOT NULL,
    description text NOT NULL,
    icon_key character varying(80) DEFAULT 'berechtigungen'::character varying NOT NULL,
    process_area_label character varying(80),
    is_department_phase_task boolean DEFAULT true NOT NULL,
    status character varying(32) DEFAULT 'open'::character varying NOT NULL,
    is_required boolean DEFAULT true NOT NULL,
    due_in_days integer,
    due_at timestamp with time zone,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    ready_at timestamp with time zone,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    node_instance_id bigint,
    CONSTRAINT workflow_tasks_status_check CHECK (((status)::text = ANY ((ARRAY['open'::character varying, 'ready'::character varying, 'in_progress'::character varying, 'blocked'::character varying, 'done'::character varying])::text[])))
);


--
-- Name: workflow_tasks_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflow_tasks ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_tasks_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflows; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflows (
    id bigint NOT NULL,
    uid uuid DEFAULT gen_random_uuid() NOT NULL,
    workflow_definition_id integer NOT NULL,
    workflow_definition_version_id bigint,
    department_id integer NOT NULL,
    position_role_id integer NOT NULL,
    created_by_user_id bigint,
    target_person_id bigint,
    first_name character varying(120) NOT NULL,
    last_name character varying(120) NOT NULL,
    employee_number integer NOT NULL,
    badge_number integer NOT NULL,
    deadline_date date,
    current_runtime_status character varying(32),
    status character varying(40) DEFAULT 'draft'::character varying NOT NULL,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    archived_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflows_status_check CHECK (((status)::text = ANY ((ARRAY['draft'::character varying, 'in_progress'::character varying, 'waiting_for_supervisor'::character varying, 'waiting_for_department'::character varying, 'completed'::character varying])::text[])))
);


--
-- Name: workflows_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflows ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflows_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: action_definitions action_definitions_action_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.action_definitions
    ADD CONSTRAINT action_definitions_action_key_key UNIQUE (action_key);


--
-- Name: action_definitions action_definitions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.action_definitions
    ADD CONSTRAINT action_definitions_pkey PRIMARY KEY (id);


--
-- Name: app_group_responsibilities app_group_responsibilities_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_responsibilities
    ADD CONSTRAINT app_group_responsibilities_pkey PRIMARY KEY (app_group_id, app_responsibility_id);


--
-- Name: app_group_roles app_group_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_roles
    ADD CONSTRAINT app_group_roles_pkey PRIMARY KEY (app_group_id, app_role_id);


--
-- Name: app_groups app_groups_group_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_groups
    ADD CONSTRAINT app_groups_group_key_key UNIQUE (group_key);


--
-- Name: app_groups app_groups_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_groups
    ADD CONSTRAINT app_groups_name_key UNIQUE (name);


--
-- Name: app_groups app_groups_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_groups
    ADD CONSTRAINT app_groups_pkey PRIMARY KEY (id);


--
-- Name: app_permissions app_permissions_permission_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_permissions
    ADD CONSTRAINT app_permissions_permission_key_key UNIQUE (permission_key);


--
-- Name: app_permissions app_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_permissions
    ADD CONSTRAINT app_permissions_pkey PRIMARY KEY (id);


--
-- Name: app_responsibilities app_responsibilities_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_responsibilities
    ADD CONSTRAINT app_responsibilities_pkey PRIMARY KEY (id);


--
-- Name: app_responsibilities app_responsibilities_responsibility_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_responsibilities
    ADD CONSTRAINT app_responsibilities_responsibility_key_key UNIQUE (responsibility_key);


--
-- Name: app_responsibilities app_responsibilities_system_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_responsibilities
    ADD CONSTRAINT app_responsibilities_system_key_key UNIQUE (system_key);


--
-- Name: app_role_answer_default_options app_role_answer_default_options_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_default_options
    ADD CONSTRAINT app_role_answer_default_options_pkey PRIMARY KEY (app_role_id, answer_definition_id, answer_option_id);


--
-- Name: app_role_answer_defaults app_role_answer_defaults_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_defaults
    ADD CONSTRAINT app_role_answer_defaults_pkey PRIMARY KEY (app_role_id, answer_definition_id);


--
-- Name: app_role_permissions app_role_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_permissions
    ADD CONSTRAINT app_role_permissions_pkey PRIMARY KEY (app_role_id, app_permission_id);


--
-- Name: app_roles app_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_roles
    ADD CONSTRAINT app_roles_pkey PRIMARY KEY (id);


--
-- Name: app_roles app_roles_role_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_roles
    ADD CONSTRAINT app_roles_role_key_key UNIQUE (role_key);


--
-- Name: app_user_groups app_user_groups_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_groups
    ADD CONSTRAINT app_user_groups_pkey PRIMARY KEY (app_user_id, app_group_id);


--
-- Name: app_user_permission_overrides app_user_permission_overrides_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_permission_overrides
    ADD CONSTRAINT app_user_permission_overrides_pkey PRIMARY KEY (id);


--
-- Name: app_user_responsibilities app_user_responsibilities_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_responsibilities
    ADD CONSTRAINT app_user_responsibilities_pkey PRIMARY KEY (app_user_id, app_responsibility_id);


--
-- Name: app_user_roles app_user_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_roles
    ADD CONSTRAINT app_user_roles_pkey PRIMARY KEY (app_user_id, app_role_id);


--
-- Name: app_users app_users_email_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_users
    ADD CONSTRAINT app_users_email_key UNIQUE (email);


--
-- Name: app_users app_users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_users
    ADD CONSTRAINT app_users_pkey PRIMARY KEY (id);


--
-- Name: auth_permission_audit_log auth_permission_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_permission_audit_log
    ADD CONSTRAINT auth_permission_audit_log_pkey PRIMARY KEY (id);


--
-- Name: automation_job_attempts automation_job_attempts_automation_job_id_attempt_number_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_job_attempts
    ADD CONSTRAINT automation_job_attempts_automation_job_id_attempt_number_key UNIQUE (automation_job_id, attempt_number);


--
-- Name: automation_job_attempts automation_job_attempts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_job_attempts
    ADD CONSTRAINT automation_job_attempts_pkey PRIMARY KEY (id);


--
-- Name: automation_job_logs automation_job_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_job_logs
    ADD CONSTRAINT automation_job_logs_pkey PRIMARY KEY (id);


--
-- Name: automation_jobs automation_jobs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_jobs
    ADD CONSTRAINT automation_jobs_pkey PRIMARY KEY (id);


--
-- Name: department_action_templates department_action_templates_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_action_templates
    ADD CONSTRAINT department_action_templates_pkey PRIMARY KEY (id);


--
-- Name: department_settings department_settings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_settings
    ADD CONSTRAINT department_settings_pkey PRIMARY KEY (department_id);


--
-- Name: departments departments_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT departments_name_key UNIQUE (name);


--
-- Name: departments departments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT departments_pkey PRIMARY KEY (id);


--
-- Name: directory_group_members directory_group_members_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_members
    ADD CONSTRAINT directory_group_members_pkey PRIMARY KEY (directory_group_id, directory_identity_id);


--
-- Name: directory_group_role_mappings directory_group_role_mappings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_role_mappings
    ADD CONSTRAINT directory_group_role_mappings_pkey PRIMARY KEY (id);


--
-- Name: directory_groups directory_groups_external_group_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_groups
    ADD CONSTRAINT directory_groups_external_group_id_key UNIQUE (external_group_id);


--
-- Name: directory_groups directory_groups_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_groups
    ADD CONSTRAINT directory_groups_pkey PRIMARY KEY (id);


--
-- Name: directory_identities directory_identities_entra_object_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_identities
    ADD CONSTRAINT directory_identities_entra_object_id_key UNIQUE (entra_object_id);


--
-- Name: directory_identities directory_identities_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_identities
    ADD CONSTRAINT directory_identities_pkey PRIMARY KEY (id);


--
-- Name: directory_mapping_audit_log directory_mapping_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_mapping_audit_log
    ADD CONSTRAINT directory_mapping_audit_log_pkey PRIMARY KEY (id);


--
-- Name: directory_sync_log directory_sync_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_sync_log
    ADD CONSTRAINT directory_sync_log_pkey PRIMARY KEY (id);


--
-- Name: notification_email_settings notification_email_settings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_email_settings
    ADD CONSTRAINT notification_email_settings_pkey PRIMARY KEY (id);


--
-- Name: notification_templates notification_templates_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_templates
    ADD CONSTRAINT notification_templates_pkey PRIMARY KEY (template_key);


--
-- Name: people people_app_user_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_app_user_id_key UNIQUE (app_user_id);


--
-- Name: people people_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_pkey PRIMARY KEY (id);


--
-- Name: rotation_audit_log rotation_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_audit_log
    ADD CONSTRAINT rotation_audit_log_pkey PRIMARY KEY (id);


--
-- Name: rotation_generated_tasks rotation_generated_tasks_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_pkey PRIMARY KEY (id);


--
-- Name: rotation_notifications rotation_notifications_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_notifications
    ADD CONSTRAINT rotation_notifications_pkey PRIMARY KEY (id);


--
-- Name: rotation_plans rotation_plans_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_plans
    ADD CONSTRAINT rotation_plans_pkey PRIMARY KEY (id);


--
-- Name: rotation_stations rotation_stations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_stations
    ADD CONSTRAINT rotation_stations_pkey PRIMARY KEY (id);


--
-- Name: rotation_stations rotation_stations_rotation_plan_id_order_index_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_stations
    ADD CONSTRAINT rotation_stations_rotation_plan_id_order_index_key UNIQUE (rotation_plan_id, order_index);


--
-- Name: rotation_task_assignments rotation_task_assignments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_assignments
    ADD CONSTRAINT rotation_task_assignments_pkey PRIMARY KEY (id);


--
-- Name: rotation_task_comments rotation_task_comments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_comments
    ADD CONSTRAINT rotation_task_comments_pkey PRIMARY KEY (id);


--
-- Name: system_event_log system_event_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_event_log
    ADD CONSTRAINT system_event_log_pkey PRIMARY KEY (id);


--
-- Name: system_responsibilities system_responsibilities_app_responsibility_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_app_responsibility_id_key UNIQUE (app_responsibility_id);


--
-- Name: system_responsibilities system_responsibilities_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_pkey PRIMARY KEY (id);


--
-- Name: system_responsibilities system_responsibilities_system_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_system_key_key UNIQUE (system_key);


--
-- Name: task_assignments task_assignments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.task_assignments
    ADD CONSTRAINT task_assignments_pkey PRIMARY KEY (id);





--
-- Name: workflow_answer_definitions uq_workflow_answer_definitions_id_workflow_definition; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_definitions
    ADD CONSTRAINT uq_workflow_answer_definitions_id_workflow_definition UNIQUE (id, workflow_definition_id);


--
-- Name: workflow_answer_definitions uq_workflow_answer_definitions_workflow_definition_answer_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_definitions
    ADD CONSTRAINT uq_workflow_answer_definitions_workflow_definition_answer_key UNIQUE (workflow_definition_id, answer_key);


--
-- Name: workflow_answer_options uq_workflow_answer_options_definition_option_pair; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_options
    ADD CONSTRAINT uq_workflow_answer_options_definition_option_pair UNIQUE (answer_definition_id, id);


--
-- Name: workflow_answer_definitions workflow_answer_definitions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_definitions
    ADD CONSTRAINT workflow_answer_definitions_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_derivation_rules workflow_answer_derivation_ru_source_answer_key_target_answ_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_derivation_rules
    ADD CONSTRAINT workflow_answer_derivation_ru_source_answer_key_target_answ_key UNIQUE (source_answer_key, target_answer_key);


--
-- Name: workflow_answer_derivation_rules workflow_answer_derivation_rules_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_derivation_rules
    ADD CONSTRAINT workflow_answer_derivation_rules_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_options workflow_answer_options_answer_definition_id_option_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_options
    ADD CONSTRAINT workflow_answer_options_answer_definition_id_option_key_key UNIQUE (answer_definition_id, option_key);


--
-- Name: workflow_answer_options workflow_answer_options_answer_definition_id_option_value_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_options
    ADD CONSTRAINT workflow_answer_options_answer_definition_id_option_value_key UNIQUE (answer_definition_id, option_value);


--
-- Name: workflow_answer_options workflow_answer_options_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_options
    ADD CONSTRAINT workflow_answer_options_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_reset_rules workflow_answer_reset_rules_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_reset_rules
    ADD CONSTRAINT workflow_answer_reset_rules_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_selected_options workflow_answer_selected_opti_workflow_answer_id_answer_opt_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_selected_options
    ADD CONSTRAINT workflow_answer_selected_opti_workflow_answer_id_answer_opt_key UNIQUE (workflow_answer_id, answer_option_id);


--
-- Name: workflow_answer_selected_options workflow_answer_selected_options_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_selected_options
    ADD CONSTRAINT workflow_answer_selected_options_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_single_select_keep_values workflow_answer_single_select_answer_definition_id_option_v_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_single_select_keep_values
    ADD CONSTRAINT workflow_answer_single_select_answer_definition_id_option_v_key UNIQUE (answer_definition_id, option_value);


--
-- Name: workflow_answer_single_select_keep_values workflow_answer_single_select_keep_values_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_single_select_keep_values
    ADD CONSTRAINT workflow_answer_single_select_keep_values_pkey PRIMARY KEY (id);


--
-- Name: workflow_answer_validation_rules workflow_answer_validation_rules_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_validation_rules
    ADD CONSTRAINT workflow_answer_validation_rules_pkey PRIMARY KEY (answer_definition_id);


--
-- Name: workflow_answer_visibility_rules workflow_answer_visibility_rules_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_visibility_rules
    ADD CONSTRAINT workflow_answer_visibility_rules_pkey PRIMARY KEY (id);


--
-- Name: workflow_answers workflow_answers_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answers
    ADD CONSTRAINT workflow_answers_pkey PRIMARY KEY (id);


--
-- Name: workflow_answers workflow_answers_workflow_id_answer_definition_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answers
    ADD CONSTRAINT workflow_answers_workflow_id_answer_definition_id_key UNIQUE (workflow_id, answer_definition_id);


--
-- Name: workflow_audit_log workflow_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_audit_log
    ADD CONSTRAINT workflow_audit_log_pkey PRIMARY KEY (id);


--
-- Name: workflow_definition_versions workflow_definition_versions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_definition_versions
    ADD CONSTRAINT workflow_definition_versions_pkey PRIMARY KEY (id);


--
-- Name: workflow_definition_versions workflow_definition_versions_workflow_definition_id_version_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_definition_versions
    ADD CONSTRAINT workflow_definition_versions_workflow_definition_id_version_key UNIQUE (workflow_definition_id, version_number);


--
-- Name: workflow_definitions workflow_definitions_definition_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_definitions
    ADD CONSTRAINT workflow_definitions_definition_key_key UNIQUE (definition_key);


--
-- Name: workflow_definitions workflow_definitions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_definitions
    ADD CONSTRAINT workflow_definitions_pkey PRIMARY KEY (id);


--
-- Name: workflow_edges workflow_edges_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_edges
    ADD CONSTRAINT workflow_edges_pkey PRIMARY KEY (id);


--
-- Name: workflow_edges workflow_edges_workflow_definition_version_id_source_workfl_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_edges
    ADD CONSTRAINT workflow_edges_workflow_definition_version_id_source_workfl_key UNIQUE (workflow_definition_version_id, source_workflow_node_id, priority);


--
-- Name: workflow_links workflow_links_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_links
    ADD CONSTRAINT workflow_links_pkey PRIMARY KEY (id);


--
-- Name: workflow_links workflow_links_source_workflow_id_target_workflow_id_link_t_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_links
    ADD CONSTRAINT workflow_links_source_workflow_id_target_workflow_id_link_t_key UNIQUE (source_workflow_id, target_workflow_id, link_type);


--
-- Name: workflow_node_actions workflow_node_actions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_actions
    ADD CONSTRAINT workflow_node_actions_pkey PRIMARY KEY (id);


--
-- Name: workflow_node_actions workflow_node_actions_workflow_node_id_action_definition_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_actions
    ADD CONSTRAINT workflow_node_actions_workflow_node_id_action_definition_id_key UNIQUE (workflow_node_id, action_definition_id, execution_order);


--
-- Name: workflow_node_actions workflow_node_actions_workflow_node_id_execution_order_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_actions
    ADD CONSTRAINT workflow_node_actions_workflow_node_id_execution_order_key UNIQUE (workflow_node_id, execution_order);


--
-- Name: workflow_node_configs workflow_node_configs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_configs
    ADD CONSTRAINT workflow_node_configs_pkey PRIMARY KEY (id);


--
-- Name: workflow_node_configs workflow_node_configs_workflow_node_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_configs
    ADD CONSTRAINT workflow_node_configs_workflow_node_id_key UNIQUE (workflow_node_id);


--
-- Name: workflow_node_instances workflow_node_instances_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_instances
    ADD CONSTRAINT workflow_node_instances_pkey PRIMARY KEY (id);


--
-- Name: workflow_node_instances workflow_node_instances_workflow_id_workflow_node_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_instances
    ADD CONSTRAINT workflow_node_instances_workflow_id_workflow_node_id_key UNIQUE (workflow_id, workflow_node_id);


--
-- Name: workflow_nodes workflow_nodes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_nodes
    ADD CONSTRAINT workflow_nodes_pkey PRIMARY KEY (id);


--
-- Name: workflow_nodes workflow_nodes_workflow_definition_version_id_node_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_nodes
    ADD CONSTRAINT workflow_nodes_workflow_definition_version_id_node_key_key UNIQUE (workflow_definition_version_id, node_key);


--
-- Name: workflow_notifications workflow_notifications_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_notifications
    ADD CONSTRAINT workflow_notifications_pkey PRIMARY KEY (id);


--
-- Name: workflow_runtime_events workflow_runtime_events_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_runtime_events
    ADD CONSTRAINT workflow_runtime_events_pkey PRIMARY KEY (id);


--
-- Name: workflow_task_comments workflow_task_comments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_comments
    ADD CONSTRAINT workflow_task_comments_pkey PRIMARY KEY (id);


--
-- Name: workflow_task_dependencies workflow_task_dependencies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_dependencies
    ADD CONSTRAINT workflow_task_dependencies_pkey PRIMARY KEY (id);


--
-- Name: workflow_task_dependencies workflow_task_dependencies_workflow_task_id_depends_on_work_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_dependencies
    ADD CONSTRAINT workflow_task_dependencies_workflow_task_id_depends_on_work_key UNIQUE (workflow_task_id, depends_on_workflow_task_id);


--
-- Name: workflow_tasks workflow_tasks_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_tasks
    ADD CONSTRAINT workflow_tasks_pkey PRIMARY KEY (id);


--
-- Name: workflow_tasks workflow_tasks_workflow_id_task_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_tasks
    ADD CONSTRAINT workflow_tasks_workflow_id_task_key_key UNIQUE (workflow_id, task_key);


--
-- Name: workflows workflows_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_pkey PRIMARY KEY (id);


--
-- Name: workflows workflows_uid_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_uid_key UNIQUE (uid);


--
-- Name: idx_app_group_responsibilities_responsibility; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_group_responsibilities_responsibility ON public.app_group_responsibilities USING btree (app_responsibility_id);


--
-- Name: idx_app_group_roles_role; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_group_roles_role ON public.app_group_roles USING btree (app_role_id);


--
-- Name: idx_app_responsibilities_department_type; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_responsibilities_department_type ON public.app_responsibilities USING btree (department_id, responsibility_type);


--
-- Name: idx_app_roles_department_kind; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_roles_department_kind ON public.app_roles USING btree (department_id, role_kind);


--
-- Name: idx_app_user_groups_group; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_user_groups_group ON public.app_user_groups USING btree (app_group_id);


--
-- Name: idx_app_user_permission_overrides_user; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_user_permission_overrides_user ON public.app_user_permission_overrides USING btree (app_user_id);


--
-- Name: idx_app_user_responsibilities_responsibility; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_user_responsibilities_responsibility ON public.app_user_responsibilities USING btree (app_responsibility_id);


--
-- Name: idx_app_user_roles_role; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_app_user_roles_role ON public.app_user_roles USING btree (app_role_id);


--
-- Name: idx_auth_permission_audit_log_actor; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_auth_permission_audit_log_actor ON public.auth_permission_audit_log USING btree (actor_user_id) WHERE (actor_user_id IS NOT NULL);


--
-- Name: idx_auth_permission_audit_log_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_auth_permission_audit_log_created_at ON public.auth_permission_audit_log USING btree (created_at DESC);


--
-- Name: idx_automation_job_attempts_job_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_automation_job_attempts_job_id ON public.automation_job_attempts USING btree (automation_job_id, attempt_number);


--
-- Name: idx_automation_job_logs_job_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_automation_job_logs_job_id ON public.automation_job_logs USING btree (automation_job_id, created_at, id);


--
-- Name: idx_automation_jobs_claim; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_automation_jobs_claim ON public.automation_jobs USING btree (status, available_at, created_at, id) WHERE ((status)::text = 'pending'::text);


--
-- Name: idx_automation_jobs_node_instance_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_automation_jobs_node_instance_id ON public.automation_jobs USING btree (workflow_node_instance_id, created_at, id);


--
-- Name: idx_automation_jobs_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_automation_jobs_workflow_id ON public.automation_jobs USING btree (workflow_id, created_at, id);


--
-- Name: idx_department_action_templates_department_trigger_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_department_action_templates_department_trigger_active ON public.department_action_templates USING btree (department_id, trigger_type, is_active);


--
-- Name: idx_department_settings_lead_person; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_department_settings_lead_person ON public.department_settings USING btree (department_lead_person_id);


--
-- Name: idx_department_settings_requirement_approver; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_department_settings_requirement_approver ON public.department_settings USING btree (requirement_approver_person_id);


--
-- Name: idx_derivation_rules_source_process; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_derivation_rules_source_definition ON public.workflow_answer_derivation_rules USING btree (source_workflow_definition_id);


--
-- Name: idx_derivation_rules_target_process; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_derivation_rules_target_definition ON public.workflow_answer_derivation_rules USING btree (target_workflow_definition_id);


--
-- Name: idx_dgrm_group; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_dgrm_group ON public.directory_group_role_mappings USING btree (directory_group_id);


--
-- Name: idx_dgrm_role; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_dgrm_role ON public.directory_group_role_mappings USING btree (app_role_id);


--
-- Name: idx_directory_identities_app_user; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_directory_identities_app_user ON public.directory_identities USING btree (app_user_id) WHERE (app_user_id IS NOT NULL);


--
-- Name: idx_directory_identities_employee_number; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_directory_identities_employee_number ON public.directory_identities USING btree (employee_number) WHERE (employee_number IS NOT NULL);


--
-- Name: idx_directory_identities_upn; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_directory_identities_upn ON public.directory_identities USING btree (user_principal_name);


--
-- Name: idx_directory_mapping_audit_log_actor; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_directory_mapping_audit_log_actor ON public.directory_mapping_audit_log USING btree (actor_user_id) WHERE (actor_user_id IS NOT NULL);


--
-- Name: idx_directory_mapping_audit_log_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_directory_mapping_audit_log_created_at ON public.directory_mapping_audit_log USING btree (created_at DESC);


--
-- Name: idx_people_current_position_role_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_people_current_position_role_id ON public.people USING btree (current_position_role_id) WHERE (current_position_role_id IS NOT NULL);


--
-- Name: idx_people_department_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_people_department_id ON public.people USING btree (department_id);


--
-- Name: idx_rotation_audit_log_generated_task_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_audit_log_generated_task_id ON public.rotation_audit_log USING btree (generated_task_id);


--
-- Name: idx_rotation_audit_log_plan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_audit_log_plan_id ON public.rotation_audit_log USING btree (rotation_plan_id, created_at DESC);


--
-- Name: idx_rotation_generated_tasks_department_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_generated_tasks_department_status ON public.rotation_generated_tasks USING btree (department_id, status);


--
-- Name: idx_rotation_generated_tasks_due_date_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_generated_tasks_due_date_status ON public.rotation_generated_tasks USING btree (due_date, status);


--
-- Name: idx_rotation_generated_tasks_plan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_generated_tasks_plan_id ON public.rotation_generated_tasks USING btree (rotation_plan_id);


--
-- Name: idx_rotation_generated_tasks_station_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_generated_tasks_station_id ON public.rotation_generated_tasks USING btree (rotation_station_id);


--
-- Name: idx_rotation_notifications_plan_type_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_notifications_plan_type_status ON public.rotation_notifications USING btree (rotation_plan_id, notification_type, status);


--
-- Name: idx_rotation_notifications_station_type; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_notifications_station_type ON public.rotation_notifications USING btree (rotation_station_id, notification_type);


--
-- Name: idx_rotation_plans_person_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_plans_person_id ON public.rotation_plans USING btree (person_id);


--
-- Name: idx_rotation_plans_source_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_plans_source_workflow_id ON public.rotation_plans USING btree (source_workflow_id);


--
-- Name: idx_rotation_task_assignments_task_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_task_assignments_task_id ON public.rotation_task_assignments USING btree (rotation_generated_task_id);


--
-- Name: idx_rotation_task_comments_task_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_rotation_task_comments_task_id ON public.rotation_task_comments USING btree (rotation_generated_task_id);


--
-- Name: idx_system_event_log_actor_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_actor_user_id ON public.system_event_log USING btree (actor_user_id, created_at DESC, id DESC);


--
-- Name: idx_system_event_log_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_created_at ON public.system_event_log USING btree (created_at DESC, id DESC);


--
-- Name: idx_system_event_log_rotation_plan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_rotation_plan_id ON public.system_event_log USING btree (rotation_plan_id, created_at DESC, id DESC);


--
-- Name: idx_system_event_log_severity_source_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_severity_source_created_at ON public.system_event_log USING btree (severity, source, created_at DESC, id DESC);


--
-- Name: idx_system_event_log_task_ref; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_task_ref ON public.system_event_log USING btree (task_ref, created_at DESC, id DESC);


--
-- Name: idx_system_event_log_workflow_uid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_event_log_workflow_uid ON public.system_event_log USING btree (workflow_uid, created_at DESC, id DESC);


--
-- Name: idx_system_responsibilities_department; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_responsibilities_department ON public.system_responsibilities USING btree (responsible_department_id);


--
-- Name: idx_system_responsibilities_person; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_system_responsibilities_person ON public.system_responsibilities USING btree (responsible_person_id);


--
-- Name: idx_task_assignments_task_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_task_assignments_task_id ON public.task_assignments USING btree (workflow_task_id);



--
-- Name: idx_workflow_answer_reset_rules_definition; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_answer_reset_rules_definition ON public.workflow_answer_reset_rules USING btree (answer_definition_id);


--
-- Name: idx_workflow_answer_visibility_rules_definition; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_answer_visibility_rules_definition ON public.workflow_answer_visibility_rules USING btree (answer_definition_id);


--
-- Name: idx_workflow_answers_key; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_answers_key ON public.workflow_answers USING btree (answer_key);


--
-- Name: idx_workflow_answers_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_answers_workflow_id ON public.workflow_answers USING btree (workflow_id);


--
-- Name: idx_workflow_audit_log_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_audit_log_workflow_id ON public.workflow_audit_log USING btree (workflow_id, created_at DESC);


--
-- Name: idx_workflow_definition_versions_definition_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_definition_versions_definition_id ON public.workflow_definition_versions USING btree (workflow_definition_id, version_number DESC);


--
-- Name: idx_workflow_edges_version_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_edges_version_id ON public.workflow_edges USING btree (workflow_definition_version_id, source_workflow_node_id, priority);


--
-- Name: idx_workflow_links_source; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_links_source ON public.workflow_links USING btree (source_workflow_id);


--
-- Name: idx_workflow_links_target; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_links_target ON public.workflow_links USING btree (target_workflow_id);


--
-- Name: idx_workflow_node_actions_workflow_node_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_node_actions_workflow_node_id ON public.workflow_node_actions USING btree (workflow_node_id, execution_order, id);


--
-- Name: idx_workflow_node_instances_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_node_instances_workflow_id ON public.workflow_node_instances USING btree (workflow_id, status, id);


--
-- Name: idx_workflow_nodes_version_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_nodes_version_id ON public.workflow_nodes USING btree (workflow_definition_version_id, sort_order, node_key);


--
-- Name: idx_workflow_notifications_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_notifications_workflow_id ON public.workflow_notifications USING btree (workflow_id);


--
-- Name: idx_workflow_runtime_events_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_runtime_events_workflow_id ON public.workflow_runtime_events USING btree (workflow_id, created_at, id);


--
-- Name: idx_workflow_task_comments_task_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_task_comments_task_id ON public.workflow_task_comments USING btree (workflow_task_id, created_at DESC);


--
-- Name: idx_workflow_tasks_workflow_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_tasks_workflow_id ON public.workflow_tasks USING btree (workflow_id);


--
-- Name: idx_workflow_tasks_workflow_task_key; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_tasks_workflow_task_key ON public.workflow_tasks USING btree (workflow_id, task_key);


--
-- Name: idx_workflows_archived_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflows_archived_at ON public.workflows USING btree (archived_at) WHERE (archived_at IS NOT NULL);


--
-- Name: idx_workflows_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflows_created_at ON public.workflows USING btree (created_at DESC);


--
-- Name: idx_workflows_current_runtime_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflows_current_runtime_status ON public.workflows USING btree (current_runtime_status) WHERE (current_runtime_status IS NOT NULL);


--
-- Name: idx_workflows_definition_version_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflows_definition_version_id ON public.workflows USING btree (workflow_definition_version_id) WHERE (workflow_definition_version_id IS NOT NULL);


--
-- Name: idx_workflows_uid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflows_uid ON public.workflows USING btree (uid);


--
-- Name: uq_app_user_permission_overrides_scope; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_app_user_permission_overrides_scope ON public.app_user_permission_overrides USING btree (app_user_id, app_permission_id, effect, scope, COALESCE(scope_department_id, '-1'::integer));


--
-- Name: uq_app_users_entra_object_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_app_users_entra_object_id ON public.app_users USING btree (entra_object_id) WHERE (entra_object_id IS NOT NULL);


--
-- Name: uq_app_users_external_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_app_users_external_key ON public.app_users USING btree (external_key) WHERE (external_key IS NOT NULL);


--
-- Name: uq_dgrm_unique_scope; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_dgrm_unique_scope ON public.directory_group_role_mappings USING btree (directory_group_id, app_role_id, scope, COALESCE(scope_department_id, '-1'::integer));


--
-- Name: uq_directory_identities_onprem_guid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_directory_identities_onprem_guid ON public.directory_identities USING btree (onprem_object_guid) WHERE (onprem_object_guid IS NOT NULL);


--
-- Name: uq_people_app_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_people_app_user_id ON public.people USING btree (app_user_id) WHERE (app_user_id IS NOT NULL);


--
-- Name: uq_people_directory_identity_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_people_directory_identity_id ON public.people USING btree (directory_identity_id) WHERE (directory_identity_id IS NOT NULL);


--
-- Name: uq_people_employee_number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_people_employee_number ON public.people USING btree (employee_number) WHERE (employee_number IS NOT NULL);


--
-- Name: uq_rotation_generated_tasks_station_template; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_rotation_generated_tasks_station_template ON public.rotation_generated_tasks USING btree (rotation_station_id, template_id) WHERE ((rotation_station_id IS NOT NULL) AND (template_id IS NOT NULL));


--
-- Name: uq_rotation_plans_active_per_person; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_rotation_plans_active_per_person ON public.rotation_plans USING btree (person_id) WHERE ((status)::text = 'active'::text);


--
-- Name: uq_rotation_plans_open_source_workflow; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_rotation_plans_open_source_workflow ON public.rotation_plans USING btree (source_workflow_id) WHERE ((status)::text = ANY ((ARRAY['draft'::character varying, 'active'::character varying])::text[]));


--
-- Name: uq_rotation_task_assignments_primary; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_rotation_task_assignments_primary ON public.rotation_task_assignments USING btree (rotation_generated_task_id) WHERE (is_primary = true);


--
-- Name: uq_task_assignments_primary_per_task; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_task_assignments_primary_per_task ON public.task_assignments USING btree (workflow_task_id) WHERE (is_primary = true);



--
-- Name: uq_workflow_answer_reset_rules; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_workflow_answer_reset_rules ON public.workflow_answer_reset_rules USING btree (answer_definition_id, trigger_kind, target_answer_definition_id);


--
-- Name: uq_workflow_answer_visibility_rules; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_workflow_answer_visibility_rules ON public.workflow_answer_visibility_rules USING btree (answer_definition_id, dependency_answer_definition_id, dependency_kind, ((expected_value_text IS NULL)), COALESCE(expected_value_text, ''::text));


--
-- Name: uq_workflow_definition_versions_published_per_definition; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_workflow_definition_versions_published_per_definition ON public.workflow_definition_versions USING btree (workflow_definition_id) WHERE ((status)::text = 'published'::text);


--
-- Name: ux_workflow_tasks_node_instance_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_workflow_tasks_node_instance_id ON public.workflow_tasks USING btree (node_instance_id) WHERE (node_instance_id IS NOT NULL);


--
-- Name: app_users trg_app_users_sync_department_to_people; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_app_users_sync_department_to_people AFTER INSERT OR UPDATE OF department_id ON public.app_users FOR EACH ROW EXECUTE FUNCTION public.sync_user_department_to_people();


--
-- Name: people trg_people_sync_department_to_user; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_people_sync_department_to_user AFTER INSERT OR UPDATE OF department_id ON public.people FOR EACH ROW EXECUTE FUNCTION public.sync_people_department_to_user();


--
-- Name: rotation_plans trg_rotation_plans_validate_source_workflow; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_rotation_plans_validate_source_workflow BEFORE INSERT OR UPDATE OF source_workflow_id ON public.rotation_plans FOR EACH ROW EXECUTE FUNCTION public.ensure_rotation_plan_source_workflow_completed();


--
-- Name: app_group_responsibilities app_group_responsibilities_app_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_responsibilities
    ADD CONSTRAINT app_group_responsibilities_app_group_id_fkey FOREIGN KEY (app_group_id) REFERENCES public.app_groups(id) ON DELETE CASCADE;


--
-- Name: app_group_responsibilities app_group_responsibilities_app_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_responsibilities
    ADD CONSTRAINT app_group_responsibilities_app_responsibility_id_fkey FOREIGN KEY (app_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE CASCADE;


--
-- Name: app_group_roles app_group_roles_app_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_roles
    ADD CONSTRAINT app_group_roles_app_group_id_fkey FOREIGN KEY (app_group_id) REFERENCES public.app_groups(id) ON DELETE CASCADE;


--
-- Name: app_group_roles app_group_roles_app_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_group_roles
    ADD CONSTRAINT app_group_roles_app_role_id_fkey FOREIGN KEY (app_role_id) REFERENCES public.app_roles(id) ON DELETE CASCADE;


--
-- Name: app_responsibilities app_responsibilities_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_responsibilities
    ADD CONSTRAINT app_responsibilities_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: app_role_answer_default_options app_role_answer_default_optio_app_role_id_answer_definitio_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_default_options
    ADD CONSTRAINT app_role_answer_default_optio_app_role_id_answer_definitio_fkey FOREIGN KEY (app_role_id, answer_definition_id) REFERENCES public.app_role_answer_defaults(app_role_id, answer_definition_id) ON DELETE CASCADE;


--
-- Name: app_role_answer_default_options app_role_answer_default_options_answer_option_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_default_options
    ADD CONSTRAINT app_role_answer_default_options_answer_option_id_fkey FOREIGN KEY (answer_option_id) REFERENCES public.workflow_answer_options(id) ON DELETE CASCADE;


--
-- Name: app_role_answer_defaults app_role_answer_defaults_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_defaults
    ADD CONSTRAINT app_role_answer_defaults_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: app_role_answer_defaults app_role_answer_defaults_app_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_defaults
    ADD CONSTRAINT app_role_answer_defaults_app_role_id_fkey FOREIGN KEY (app_role_id) REFERENCES public.app_roles(id) ON DELETE CASCADE;


--
-- Name: app_role_answer_defaults app_role_answer_defaults_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_defaults
    ADD CONSTRAINT app_role_answer_defaults_workflow_definition_id_fkey FOREIGN KEY (workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE RESTRICT;


--
-- Name: app_role_permissions app_role_permissions_app_permission_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_permissions
    ADD CONSTRAINT app_role_permissions_app_permission_id_fkey FOREIGN KEY (app_permission_id) REFERENCES public.app_permissions(id) ON DELETE CASCADE;


--
-- Name: app_role_permissions app_role_permissions_app_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_permissions
    ADD CONSTRAINT app_role_permissions_app_role_id_fkey FOREIGN KEY (app_role_id) REFERENCES public.app_roles(id) ON DELETE CASCADE;


--
-- Name: app_roles app_roles_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_roles
    ADD CONSTRAINT app_roles_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: app_user_groups app_user_groups_app_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_groups
    ADD CONSTRAINT app_user_groups_app_group_id_fkey FOREIGN KEY (app_group_id) REFERENCES public.app_groups(id) ON DELETE CASCADE;


--
-- Name: app_user_groups app_user_groups_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_groups
    ADD CONSTRAINT app_user_groups_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE CASCADE;


--
-- Name: app_user_permission_overrides app_user_permission_overrides_app_permission_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_permission_overrides
    ADD CONSTRAINT app_user_permission_overrides_app_permission_id_fkey FOREIGN KEY (app_permission_id) REFERENCES public.app_permissions(id) ON DELETE CASCADE;


--
-- Name: app_user_permission_overrides app_user_permission_overrides_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_permission_overrides
    ADD CONSTRAINT app_user_permission_overrides_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE CASCADE;


--
-- Name: app_user_permission_overrides app_user_permission_overrides_scope_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_permission_overrides
    ADD CONSTRAINT app_user_permission_overrides_scope_department_id_fkey FOREIGN KEY (scope_department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: app_user_responsibilities app_user_responsibilities_app_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_responsibilities
    ADD CONSTRAINT app_user_responsibilities_app_responsibility_id_fkey FOREIGN KEY (app_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE CASCADE;


--
-- Name: app_user_responsibilities app_user_responsibilities_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_responsibilities
    ADD CONSTRAINT app_user_responsibilities_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE CASCADE;


--
-- Name: app_user_roles app_user_roles_app_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_roles
    ADD CONSTRAINT app_user_roles_app_role_id_fkey FOREIGN KEY (app_role_id) REFERENCES public.app_roles(id) ON DELETE CASCADE;


--
-- Name: app_user_roles app_user_roles_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_user_roles
    ADD CONSTRAINT app_user_roles_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE CASCADE;


--
-- Name: app_users app_users_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_users
    ADD CONSTRAINT app_users_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: auth_permission_audit_log auth_permission_audit_log_actor_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_permission_audit_log
    ADD CONSTRAINT auth_permission_audit_log_actor_user_id_fkey FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: automation_job_attempts automation_job_attempts_automation_job_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_job_attempts
    ADD CONSTRAINT automation_job_attempts_automation_job_id_fkey FOREIGN KEY (automation_job_id) REFERENCES public.automation_jobs(id) ON DELETE CASCADE;


--
-- Name: automation_job_logs automation_job_logs_automation_job_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_job_logs
    ADD CONSTRAINT automation_job_logs_automation_job_id_fkey FOREIGN KEY (automation_job_id) REFERENCES public.automation_jobs(id) ON DELETE CASCADE;


--
-- Name: automation_jobs automation_jobs_action_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_jobs
    ADD CONSTRAINT automation_jobs_action_definition_id_fkey FOREIGN KEY (action_definition_id) REFERENCES public.action_definitions(id) ON DELETE RESTRICT;


--
-- Name: automation_jobs automation_jobs_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_jobs
    ADD CONSTRAINT automation_jobs_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: automation_jobs automation_jobs_workflow_node_action_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_jobs
    ADD CONSTRAINT automation_jobs_workflow_node_action_id_fkey FOREIGN KEY (workflow_node_action_id) REFERENCES public.workflow_node_actions(id) ON DELETE RESTRICT;


--
-- Name: automation_jobs automation_jobs_workflow_node_instance_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.automation_jobs
    ADD CONSTRAINT automation_jobs_workflow_node_instance_id_fkey FOREIGN KEY (workflow_node_instance_id) REFERENCES public.workflow_node_instances(id) ON DELETE CASCADE;


--
-- Name: department_action_templates department_action_templates_default_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_action_templates
    ADD CONSTRAINT department_action_templates_default_responsibility_id_fkey FOREIGN KEY (default_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE SET NULL;


--
-- Name: department_action_templates department_action_templates_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_action_templates
    ADD CONSTRAINT department_action_templates_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE RESTRICT;


--
-- Name: department_settings department_settings_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_settings
    ADD CONSTRAINT department_settings_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE CASCADE;


--
-- Name: department_settings department_settings_department_lead_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_settings
    ADD CONSTRAINT department_settings_department_lead_person_id_fkey FOREIGN KEY (department_lead_person_id) REFERENCES public.people(id) ON DELETE SET NULL;


--
-- Name: department_settings department_settings_requirement_approver_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.department_settings
    ADD CONSTRAINT department_settings_requirement_approver_person_id_fkey FOREIGN KEY (requirement_approver_person_id) REFERENCES public.people(id) ON DELETE SET NULL;


--
-- Name: directory_group_members directory_group_members_directory_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_members
    ADD CONSTRAINT directory_group_members_directory_group_id_fkey FOREIGN KEY (directory_group_id) REFERENCES public.directory_groups(id) ON DELETE CASCADE;


--
-- Name: directory_group_members directory_group_members_directory_identity_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_members
    ADD CONSTRAINT directory_group_members_directory_identity_id_fkey FOREIGN KEY (directory_identity_id) REFERENCES public.directory_identities(id) ON DELETE CASCADE;


--
-- Name: directory_group_role_mappings directory_group_role_mappings_app_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_role_mappings
    ADD CONSTRAINT directory_group_role_mappings_app_role_id_fkey FOREIGN KEY (app_role_id) REFERENCES public.app_roles(id) ON DELETE CASCADE;


--
-- Name: directory_group_role_mappings directory_group_role_mappings_directory_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_role_mappings
    ADD CONSTRAINT directory_group_role_mappings_directory_group_id_fkey FOREIGN KEY (directory_group_id) REFERENCES public.directory_groups(id) ON DELETE CASCADE;


--
-- Name: directory_group_role_mappings directory_group_role_mappings_scope_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_group_role_mappings
    ADD CONSTRAINT directory_group_role_mappings_scope_department_id_fkey FOREIGN KEY (scope_department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: directory_identities directory_identities_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_identities
    ADD CONSTRAINT directory_identities_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: directory_mapping_audit_log directory_mapping_audit_log_actor_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.directory_mapping_audit_log
    ADD CONSTRAINT directory_mapping_audit_log_actor_user_id_fkey FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: app_role_answer_defaults fk_app_role_answer_defaults_definition_workflow_definition; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.app_role_answer_defaults
    ADD CONSTRAINT fk_app_role_answer_defaults_definition_workflow_definition FOREIGN KEY (answer_definition_id, workflow_definition_id) REFERENCES public.workflow_answer_definitions(id, workflow_definition_id) ON DELETE CASCADE;


--
-- Name: task_assignments fk_task_assignments_assignee_responsibility; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.task_assignments
    ADD CONSTRAINT fk_task_assignments_assignee_responsibility FOREIGN KEY (assignee_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE RESTRICT;


--
-- Name: task_assignments fk_task_assignments_assignee_user; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.task_assignments
    ADD CONSTRAINT fk_task_assignments_assignee_user FOREIGN KEY (assignee_user_id) REFERENCES public.app_users(id) ON DELETE RESTRICT;


--
-- Name: workflow_answers fk_workflow_answers_selected_option_matches_definition; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answers
    ADD CONSTRAINT fk_workflow_answers_selected_option_matches_definition FOREIGN KEY (answer_definition_id, selected_option_id) REFERENCES public.workflow_answer_options(answer_definition_id, id) ON DELETE RESTRICT;


--
-- Name: workflows fk_workflows_workflow_definition_version; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT fk_workflows_workflow_definition_version FOREIGN KEY (workflow_definition_version_id) REFERENCES public.workflow_definition_versions(id) ON DELETE SET NULL;


--
-- Name: people people_app_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_app_user_id_fkey FOREIGN KEY (app_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: people people_current_position_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_current_position_role_id_fkey FOREIGN KEY (current_position_role_id) REFERENCES public.app_roles(id) ON DELETE SET NULL;


--
-- Name: people people_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: people people_directory_identity_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.people
    ADD CONSTRAINT people_directory_identity_id_fkey FOREIGN KEY (directory_identity_id) REFERENCES public.directory_identities(id) ON DELETE SET NULL;


--
-- Name: rotation_audit_log rotation_audit_log_actor_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_audit_log
    ADD CONSTRAINT rotation_audit_log_actor_user_id_fkey FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: rotation_audit_log rotation_audit_log_generated_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_audit_log
    ADD CONSTRAINT rotation_audit_log_generated_task_id_fkey FOREIGN KEY (generated_task_id) REFERENCES public.rotation_generated_tasks(id) ON DELETE SET NULL;


--
-- Name: rotation_audit_log rotation_audit_log_rotation_plan_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_audit_log
    ADD CONSTRAINT rotation_audit_log_rotation_plan_id_fkey FOREIGN KEY (rotation_plan_id) REFERENCES public.rotation_plans(id) ON DELETE CASCADE;


--
-- Name: rotation_audit_log rotation_audit_log_rotation_station_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_audit_log
    ADD CONSTRAINT rotation_audit_log_rotation_station_id_fkey FOREIGN KEY (rotation_station_id) REFERENCES public.rotation_stations(id) ON DELETE SET NULL;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE RESTRICT;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_person_id_fkey FOREIGN KEY (person_id) REFERENCES public.people(id) ON DELETE RESTRICT;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_responsibility_id_fkey FOREIGN KEY (responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE SET NULL;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_rotation_plan_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_rotation_plan_id_fkey FOREIGN KEY (rotation_plan_id) REFERENCES public.rotation_plans(id) ON DELETE CASCADE;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_rotation_station_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_rotation_station_id_fkey FOREIGN KEY (rotation_station_id) REFERENCES public.rotation_stations(id) ON DELETE SET NULL;


--
-- Name: rotation_generated_tasks rotation_generated_tasks_template_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_template_id_fkey FOREIGN KEY (template_id) REFERENCES public.department_action_templates(id) ON DELETE SET NULL;


--
-- Name: rotation_notifications rotation_notifications_generated_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_notifications
    ADD CONSTRAINT rotation_notifications_generated_task_id_fkey FOREIGN KEY (generated_task_id) REFERENCES public.rotation_generated_tasks(id) ON DELETE SET NULL;


--
-- Name: rotation_notifications rotation_notifications_recipient_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_notifications
    ADD CONSTRAINT rotation_notifications_recipient_user_id_fkey FOREIGN KEY (recipient_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: rotation_notifications rotation_notifications_rotation_plan_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_notifications
    ADD CONSTRAINT rotation_notifications_rotation_plan_id_fkey FOREIGN KEY (rotation_plan_id) REFERENCES public.rotation_plans(id) ON DELETE CASCADE;


--
-- Name: rotation_notifications rotation_notifications_rotation_station_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_notifications
    ADD CONSTRAINT rotation_notifications_rotation_station_id_fkey FOREIGN KEY (rotation_station_id) REFERENCES public.rotation_stations(id) ON DELETE SET NULL;


--
-- Name: rotation_plans rotation_plans_created_by_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_plans
    ADD CONSTRAINT rotation_plans_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: rotation_plans rotation_plans_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_plans
    ADD CONSTRAINT rotation_plans_person_id_fkey FOREIGN KEY (person_id) REFERENCES public.people(id) ON DELETE RESTRICT;


--
-- Name: rotation_plans rotation_plans_source_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_plans
    ADD CONSTRAINT rotation_plans_source_workflow_id_fkey FOREIGN KEY (source_workflow_id) REFERENCES public.workflows(id) ON DELETE RESTRICT;


--
-- Name: rotation_stations rotation_stations_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_stations
    ADD CONSTRAINT rotation_stations_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE RESTRICT;


--
-- Name: rotation_stations rotation_stations_rotation_plan_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_stations
    ADD CONSTRAINT rotation_stations_rotation_plan_id_fkey FOREIGN KEY (rotation_plan_id) REFERENCES public.rotation_plans(id) ON DELETE CASCADE;


--
-- Name: rotation_task_assignments rotation_task_assignments_assignee_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_assignments
    ADD CONSTRAINT rotation_task_assignments_assignee_responsibility_id_fkey FOREIGN KEY (assignee_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE SET NULL;


--
-- Name: rotation_task_assignments rotation_task_assignments_assignee_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_assignments
    ADD CONSTRAINT rotation_task_assignments_assignee_user_id_fkey FOREIGN KEY (assignee_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: rotation_task_assignments rotation_task_assignments_rotation_generated_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_assignments
    ADD CONSTRAINT rotation_task_assignments_rotation_generated_task_id_fkey FOREIGN KEY (rotation_generated_task_id) REFERENCES public.rotation_generated_tasks(id) ON DELETE CASCADE;


--
-- Name: rotation_task_comments rotation_task_comments_author_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_comments
    ADD CONSTRAINT rotation_task_comments_author_user_id_fkey FOREIGN KEY (author_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: rotation_task_comments rotation_task_comments_rotation_generated_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rotation_task_comments
    ADD CONSTRAINT rotation_task_comments_rotation_generated_task_id_fkey FOREIGN KEY (rotation_generated_task_id) REFERENCES public.rotation_generated_tasks(id) ON DELETE CASCADE;


--
-- Name: system_event_log system_event_log_actor_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_event_log
    ADD CONSTRAINT system_event_log_actor_user_id_fkey FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: system_responsibilities system_responsibilities_app_responsibility_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_app_responsibility_id_fkey FOREIGN KEY (app_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE CASCADE;


--
-- Name: system_responsibilities system_responsibilities_responsible_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_responsible_department_id_fkey FOREIGN KEY (responsible_department_id) REFERENCES public.departments(id) ON DELETE SET NULL;


--
-- Name: system_responsibilities system_responsibilities_responsible_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_responsibilities
    ADD CONSTRAINT system_responsibilities_responsible_person_id_fkey FOREIGN KEY (responsible_person_id) REFERENCES public.people(id) ON DELETE SET NULL;


--
-- Name: task_assignments task_assignments_workflow_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.task_assignments
    ADD CONSTRAINT task_assignments_workflow_task_id_fkey FOREIGN KEY (workflow_task_id) REFERENCES public.workflow_tasks(id) ON DELETE CASCADE;








--
-- Name: workflow_answer_definitions workflow_answer_definitions_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_definitions
    ADD CONSTRAINT workflow_answer_definitions_workflow_definition_id_fkey FOREIGN KEY (workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE RESTRICT;


--
-- Name: workflow_answer_derivation_rules workflow_answer_derivation_rules_source_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_derivation_rules
    ADD CONSTRAINT workflow_answer_derivation_rules_source_workflow_definition_id_fkey FOREIGN KEY (source_workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_derivation_rules workflow_answer_derivation_rules_target_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_derivation_rules
    ADD CONSTRAINT workflow_answer_derivation_rules_target_workflow_definition_id_fkey FOREIGN KEY (target_workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_options workflow_answer_options_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_options
    ADD CONSTRAINT workflow_answer_options_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_reset_rules workflow_answer_reset_rules_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_reset_rules
    ADD CONSTRAINT workflow_answer_reset_rules_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_reset_rules workflow_answer_reset_rules_target_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_reset_rules
    ADD CONSTRAINT workflow_answer_reset_rules_target_answer_definition_id_fkey FOREIGN KEY (target_answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_selected_options workflow_answer_selected_options_answer_option_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_selected_options
    ADD CONSTRAINT workflow_answer_selected_options_answer_option_id_fkey FOREIGN KEY (answer_option_id) REFERENCES public.workflow_answer_options(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_selected_options workflow_answer_selected_options_workflow_answer_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_selected_options
    ADD CONSTRAINT workflow_answer_selected_options_workflow_answer_id_fkey FOREIGN KEY (workflow_answer_id) REFERENCES public.workflow_answers(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_single_select_keep_values workflow_answer_single_select_keep_va_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_single_select_keep_values
    ADD CONSTRAINT workflow_answer_single_select_keep_va_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_validation_rules workflow_answer_validation_rules_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_validation_rules
    ADD CONSTRAINT workflow_answer_validation_rules_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_visibility_rules workflow_answer_visibility_ru_dependency_answer_definition_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_visibility_rules
    ADD CONSTRAINT workflow_answer_visibility_ru_dependency_answer_definition_fkey FOREIGN KEY (dependency_answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answer_visibility_rules workflow_answer_visibility_rules_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answer_visibility_rules
    ADD CONSTRAINT workflow_answer_visibility_rules_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_answers workflow_answers_answer_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answers
    ADD CONSTRAINT workflow_answers_answer_definition_id_fkey FOREIGN KEY (answer_definition_id) REFERENCES public.workflow_answer_definitions(id) ON DELETE RESTRICT;


--
-- Name: workflow_answers workflow_answers_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_answers
    ADD CONSTRAINT workflow_answers_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_audit_log workflow_audit_log_actor_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_audit_log
    ADD CONSTRAINT workflow_audit_log_actor_user_id_fkey FOREIGN KEY (actor_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: workflow_audit_log workflow_audit_log_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_audit_log
    ADD CONSTRAINT workflow_audit_log_task_id_fkey FOREIGN KEY (task_id) REFERENCES public.workflow_tasks(id) ON DELETE SET NULL;


--
-- Name: workflow_audit_log workflow_audit_log_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_audit_log
    ADD CONSTRAINT workflow_audit_log_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_definition_versions workflow_definition_versions_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_definition_versions
    ADD CONSTRAINT workflow_definition_versions_workflow_definition_id_fkey FOREIGN KEY (workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE CASCADE;


--
-- Name: workflow_edges workflow_edges_source_workflow_node_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_edges
    ADD CONSTRAINT workflow_edges_source_workflow_node_id_fkey FOREIGN KEY (source_workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;


--
-- Name: workflow_edges workflow_edges_target_workflow_node_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_edges
    ADD CONSTRAINT workflow_edges_target_workflow_node_id_fkey FOREIGN KEY (target_workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;


--
-- Name: workflow_edges workflow_edges_workflow_definition_version_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_edges
    ADD CONSTRAINT workflow_edges_workflow_definition_version_id_fkey FOREIGN KEY (workflow_definition_version_id) REFERENCES public.workflow_definition_versions(id) ON DELETE CASCADE;


--
-- Name: workflow_links workflow_links_created_by_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_links
    ADD CONSTRAINT workflow_links_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: workflow_links workflow_links_source_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_links
    ADD CONSTRAINT workflow_links_source_workflow_id_fkey FOREIGN KEY (source_workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_links workflow_links_target_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_links
    ADD CONSTRAINT workflow_links_target_workflow_id_fkey FOREIGN KEY (target_workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_node_actions workflow_node_actions_action_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_actions
    ADD CONSTRAINT workflow_node_actions_action_definition_id_fkey FOREIGN KEY (action_definition_id) REFERENCES public.action_definitions(id) ON DELETE RESTRICT;


--
-- Name: workflow_node_actions workflow_node_actions_workflow_node_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_actions
    ADD CONSTRAINT workflow_node_actions_workflow_node_id_fkey FOREIGN KEY (workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;


--
-- Name: workflow_node_configs workflow_node_configs_workflow_node_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_configs
    ADD CONSTRAINT workflow_node_configs_workflow_node_id_fkey FOREIGN KEY (workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;


--
-- Name: workflow_node_instances workflow_node_instances_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_instances
    ADD CONSTRAINT workflow_node_instances_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_node_instances workflow_node_instances_workflow_node_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_node_instances
    ADD CONSTRAINT workflow_node_instances_workflow_node_id_fkey FOREIGN KEY (workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;


--
-- Name: workflow_nodes workflow_nodes_workflow_definition_version_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_nodes
    ADD CONSTRAINT workflow_nodes_workflow_definition_version_id_fkey FOREIGN KEY (workflow_definition_version_id) REFERENCES public.workflow_definition_versions(id) ON DELETE CASCADE;


--
-- Name: workflow_notifications workflow_notifications_recipient_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_notifications
    ADD CONSTRAINT workflow_notifications_recipient_user_id_fkey FOREIGN KEY (recipient_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: workflow_notifications workflow_notifications_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_notifications
    ADD CONSTRAINT workflow_notifications_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_notifications workflow_notifications_workflow_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_notifications
    ADD CONSTRAINT workflow_notifications_workflow_task_id_fkey FOREIGN KEY (workflow_task_id) REFERENCES public.workflow_tasks(id) ON DELETE CASCADE;


--
-- Name: workflow_runtime_events workflow_runtime_events_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_runtime_events
    ADD CONSTRAINT workflow_runtime_events_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflow_runtime_events workflow_runtime_events_workflow_node_instance_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_runtime_events
    ADD CONSTRAINT workflow_runtime_events_workflow_node_instance_id_fkey FOREIGN KEY (workflow_node_instance_id) REFERENCES public.workflow_node_instances(id) ON DELETE SET NULL;


--
-- Name: workflow_task_comments workflow_task_comments_author_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_comments
    ADD CONSTRAINT workflow_task_comments_author_user_id_fkey FOREIGN KEY (author_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: workflow_task_comments workflow_task_comments_workflow_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_comments
    ADD CONSTRAINT workflow_task_comments_workflow_task_id_fkey FOREIGN KEY (workflow_task_id) REFERENCES public.workflow_tasks(id) ON DELETE CASCADE;


--
-- Name: workflow_task_dependencies workflow_task_dependencies_depends_on_workflow_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_dependencies
    ADD CONSTRAINT workflow_task_dependencies_depends_on_workflow_task_id_fkey FOREIGN KEY (depends_on_workflow_task_id) REFERENCES public.workflow_tasks(id) ON DELETE CASCADE;


--
-- Name: workflow_task_dependencies workflow_task_dependencies_workflow_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_task_dependencies
    ADD CONSTRAINT workflow_task_dependencies_workflow_task_id_fkey FOREIGN KEY (workflow_task_id) REFERENCES public.workflow_tasks(id) ON DELETE CASCADE;


--
-- Name: workflow_tasks workflow_tasks_node_instance_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_tasks
    ADD CONSTRAINT workflow_tasks_node_instance_id_fkey FOREIGN KEY (node_instance_id) REFERENCES public.workflow_node_instances(id) ON DELETE SET NULL;



--
-- Name: workflow_tasks workflow_tasks_workflow_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflow_tasks
    ADD CONSTRAINT workflow_tasks_workflow_id_fkey FOREIGN KEY (workflow_id) REFERENCES public.workflows(id) ON DELETE CASCADE;


--
-- Name: workflows workflows_created_by_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.app_users(id) ON DELETE SET NULL;


--
-- Name: workflows workflows_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id) ON DELETE RESTRICT;


--
-- Name: workflows workflows_position_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_position_role_id_fkey FOREIGN KEY (position_role_id) REFERENCES public.app_roles(id) ON DELETE RESTRICT;


--
-- Name: workflows workflows_workflow_definition_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_workflow_definition_id_fkey FOREIGN KEY (workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE RESTRICT;


--
-- Name: workflows workflows_target_person_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflows
    ADD CONSTRAINT workflows_target_person_id_fkey FOREIGN KEY (target_person_id) REFERENCES public.people(id) ON DELETE RESTRICT;


--
-- LA5: Per-Node Task-Spezifikationen (siehe
-- KauthWorkflow/Architektur/LA5-TaskSpezifikation-Skizze.md).
--

CREATE TABLE public.workflow_node_task_specs (
    id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    spec_key character varying(120) NOT NULL,
    title character varying(220) NOT NULL,
    description text NOT NULL,
    category character varying(80) DEFAULT 'general'::character varying NOT NULL,
    icon_key character varying(80) DEFAULT 'berechtigungen'::character varying NOT NULL,
    default_responsibility_id integer,
    process_area_label character varying(80),
    is_department_phase_task boolean DEFAULT true NOT NULL,
    is_required boolean DEFAULT true NOT NULL,
    due_in_days integer,
    sort_order integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

ALTER TABLE public.workflow_node_task_specs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_task_specs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);

ALTER TABLE ONLY public.workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_node_spec_key UNIQUE (workflow_node_id, spec_key);

-- Composite-UNIQUE als Ziel fuer dependencies-Composite-FK (Same-Node-Constraint)
ALTER TABLE ONLY public.workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_id_node_uk UNIQUE (id, workflow_node_id);

CREATE INDEX idx_workflow_node_task_specs_node
    ON public.workflow_node_task_specs USING btree (workflow_node_id, sort_order, id);

ALTER TABLE ONLY public.workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_workflow_node_id_fkey
    FOREIGN KEY (workflow_node_id) REFERENCES public.workflow_nodes(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_default_responsibility_id_fkey
    FOREIGN KEY (default_responsibility_id) REFERENCES public.app_responsibilities(id) ON DELETE SET NULL;


CREATE TABLE public.workflow_node_task_spec_conditions (
    id bigint NOT NULL,
    workflow_node_task_spec_id bigint NOT NULL,
    answer_key character varying(120) NOT NULL,
    operator character varying(32) NOT NULL,
    expected_value_text text,
    expected_value_boolean boolean,
    expected_value_number numeric(12,2),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT workflow_node_task_spec_conditions_operator_check
        CHECK (((operator)::text = ANY ((ARRAY['eq'::character varying, 'neq'::character varying, 'is_true'::character varying, 'is_false'::character varying, 'is_null'::character varying, 'is_not_null'::character varying])::text[])))
);

ALTER TABLE public.workflow_node_task_spec_conditions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_task_spec_conditions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);

ALTER TABLE ONLY public.workflow_node_task_spec_conditions
    ADD CONSTRAINT workflow_node_task_spec_conditions_pkey PRIMARY KEY (id);

CREATE INDEX idx_workflow_node_task_spec_conditions_spec
    ON public.workflow_node_task_spec_conditions USING btree (workflow_node_task_spec_id);

ALTER TABLE ONLY public.workflow_node_task_spec_conditions
    ADD CONSTRAINT workflow_node_task_spec_conditions_spec_id_fkey
    FOREIGN KEY (workflow_node_task_spec_id) REFERENCES public.workflow_node_task_specs(id) ON DELETE CASCADE;


CREATE TABLE public.workflow_node_task_spec_dependencies (
    id bigint NOT NULL,
    workflow_node_task_spec_id bigint NOT NULL,
    depends_on_workflow_node_task_spec_id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    CONSTRAINT workflow_node_task_spec_deps_no_self_ref
        CHECK (workflow_node_task_spec_id <> depends_on_workflow_node_task_spec_id)
);

ALTER TABLE public.workflow_node_task_spec_dependencies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.workflow_node_task_spec_dependencies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);

ALTER TABLE ONLY public.workflow_node_task_spec_dependencies
    ADD CONSTRAINT workflow_node_task_spec_dependencies_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.workflow_node_task_spec_dependencies
    ADD CONSTRAINT workflow_node_task_spec_deps_pair_unique
    UNIQUE (workflow_node_task_spec_id, depends_on_workflow_node_task_spec_id);

CREATE INDEX idx_workflow_node_task_spec_deps_dependent
    ON public.workflow_node_task_spec_dependencies USING btree (workflow_node_task_spec_id);

CREATE INDEX idx_workflow_node_task_spec_deps_depends_on
    ON public.workflow_node_task_spec_dependencies USING btree (depends_on_workflow_node_task_spec_id);

-- Composite-FKs erzwingen, dass Dependent + Depends-On am SELBEN Node liegen
ALTER TABLE ONLY public.workflow_node_task_spec_dependencies
    ADD CONSTRAINT workflow_node_task_spec_deps_dependent_fkey
    FOREIGN KEY (workflow_node_task_spec_id, workflow_node_id)
    REFERENCES public.workflow_node_task_specs(id, workflow_node_id) ON DELETE CASCADE;

ALTER TABLE ONLY public.workflow_node_task_spec_dependencies
    ADD CONSTRAINT workflow_node_task_spec_deps_depends_on_fkey
    FOREIGN KEY (depends_on_workflow_node_task_spec_id, workflow_node_id)
    REFERENCES public.workflow_node_task_specs(id, workflow_node_id) ON DELETE CASCADE;


-- workflow_tasks-Audit-Link auf Spec-Tabelle
ALTER TABLE ONLY public.workflow_tasks
    ADD CONSTRAINT workflow_tasks_workflow_node_task_spec_id_fkey
    FOREIGN KEY (workflow_node_task_spec_id) REFERENCES public.workflow_node_task_specs(id) ON DELETE SET NULL;
