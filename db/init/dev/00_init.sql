\set ON_ERROR_STOP on

-- Konsolidierter Init fuer dev. Statt 60+ Migrationen wird einmalig
-- das fertige Schema und die dev-Seed-Daten geladen.
-- 02_dev_seed.sql enthaelt prod-Bootstrap plus dev-Extras
-- (Rotations-Beispieltemplates, Department-Templates, lokale notification_email_settings).

\i /docker-entrypoint-sql/01_schema.sql
\i /docker-entrypoint-sql/02_dev_seed.sql
