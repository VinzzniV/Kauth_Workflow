\set ON_ERROR_STOP on

-- Konsolidierter Init fuer prod. Statt 60+ Migrationen wird einmalig
-- das fertige Schema und die prod-Seed-Daten geladen.
-- Sobald die Plattform live ist, hier nicht mehr aendern, sondern
-- additive Migrationen anlegen.

\i /docker-entrypoint-sql/01_schema.sql
\i /docker-entrypoint-sql/02_bootstrap.sql
