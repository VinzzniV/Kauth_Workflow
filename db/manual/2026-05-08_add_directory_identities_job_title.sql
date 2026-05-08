-- Manual migration for existing databases created before 2026-05-08.
-- Fresh databases already get the column from db/01_schema.sql.
--
-- Usage:
--   psql "$DATABASE_URL" -f db/manual/2026-05-08_add_directory_identities_job_title.sql

ALTER TABLE public.directory_identities
    ADD COLUMN IF NOT EXISTS job_title character varying(180);
