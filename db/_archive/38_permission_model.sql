-- Migration: hybrid permission model with role bundles, user overrides and directory-backed department metadata.

ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS directory_synced BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS last_directory_synced_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS department_source VARCHAR(32) NOT NULL DEFAULT 'local',
    ADD COLUMN IF NOT EXISTS department_override_active BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE app_users
    DROP CONSTRAINT IF EXISTS chk_app_users_department_source;

ALTER TABLE app_users
    ADD CONSTRAINT chk_app_users_department_source
    CHECK (department_source IN ('local', 'directory', 'override', 'unassigned'));

ALTER TABLE directory_identities
    ADD COLUMN IF NOT EXISTS department_name VARCHAR(120);

CREATE TABLE IF NOT EXISTS app_permissions (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    permission_key VARCHAR(160) NOT NULL UNIQUE,
    name VARCHAR(180) NOT NULL,
    description TEXT,
    scope_kind VARCHAR(32) NOT NULL DEFAULT 'global',
    category VARCHAR(80) NOT NULL DEFAULT 'general',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_app_permissions_scope_kind
        CHECK (scope_kind IN ('global', 'department'))
);

CREATE TABLE IF NOT EXISTS app_role_permissions (
    app_role_id INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    app_permission_id INTEGER NOT NULL REFERENCES app_permissions(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_role_id, app_permission_id)
);

CREATE TABLE IF NOT EXISTS app_user_permission_overrides (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    app_user_id BIGINT NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    app_permission_id INTEGER NOT NULL REFERENCES app_permissions(id) ON DELETE CASCADE,
    effect VARCHAR(16) NOT NULL,
    scope VARCHAR(32) NOT NULL DEFAULT 'global',
    scope_department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_app_user_permission_overrides_effect
        CHECK (effect IN ('allow', 'deny')),
    CONSTRAINT chk_app_user_permission_overrides_scope
        CHECK (scope IN ('global', 'department'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_app_user_permission_overrides_scope
    ON app_user_permission_overrides (
        app_user_id,
        app_permission_id,
        effect,
        scope,
        COALESCE(scope_department_id, -1)
    );

CREATE INDEX IF NOT EXISTS idx_app_user_permission_overrides_user
    ON app_user_permission_overrides(app_user_id);

CREATE TABLE IF NOT EXISTS auth_permission_audit_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    event_type VARCHAR(40) NOT NULL,
    entity_type VARCHAR(64) NOT NULL,
    detail TEXT,
    old_value JSONB,
    new_value JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_auth_permission_audit_log_created_at
    ON auth_permission_audit_log(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_auth_permission_audit_log_actor
    ON auth_permission_audit_log(actor_user_id)
    WHERE actor_user_id IS NOT NULL;

ALTER TABLE directory_group_role_mappings
    DROP CONSTRAINT IF EXISTS chk_directory_group_role_mappings_scope;

ALTER TABLE directory_group_role_mappings
    ADD CONSTRAINT chk_directory_group_role_mappings_scope
    CHECK (scope IN ('global', 'department'));

WITH permission_seed(permission_key, name, description, scope_kind, category) AS (
    VALUES
        ('app.access', 'App-Zugang', 'Erlaubt die Nutzung der Anwendung.', 'global', 'general'),
        ('users.view_department', 'Benutzer der eigenen Abteilung sehen', 'Darf Benutzer der freigegebenen Abteilungen sehen.', 'department', 'users'),
        ('users.view_all_departments', 'Alle Benutzer sehen', 'Darf Benutzer aller Abteilungen sehen.', 'global', 'users'),
        ('workflows.view_department', 'Workflows der eigenen Abteilung sehen', 'Darf Workflows der freigegebenen Abteilungen sehen.', 'department', 'workflows'),
        ('workflows.view_all', 'Alle Workflows sehen', 'Darf Workflows aller Abteilungen sehen.', 'global', 'workflows'),
        ('workflows.create.onboarding', 'Onboarding starten', 'Darf Onboarding-Vorgaenge starten.', 'department', 'workflows'),
        ('workflows.create.offboarding', 'Offboarding starten', 'Darf Offboarding-Vorgaenge starten.', 'department', 'workflows'),
        ('workflows.create.department_change', 'Abteilungswechsel starten', 'Darf Abteilungswechsel-Vorgaenge starten.', 'department', 'workflows'),
        ('workflows.create.position_change', 'Positionswechsel starten', 'Darf Positionswechsel-Vorgaenge starten.', 'department', 'workflows'),
        ('workflows.create.role_change', 'Rollenwechsel starten', 'Darf Rollenwechsel-Vorgaenge starten.', 'department', 'workflows'),
        ('workflows.create.name_change', 'Namensaenderung starten', 'Darf Namensaenderungen starten.', 'department', 'workflows'),
        ('tasks.execute.supervisor', 'Supervisor-Aufgaben bearbeiten', 'Darf Supervisor-Schritte der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks'),
        ('tasks.execute.department', 'Fachbereichsaufgaben bearbeiten', 'Darf Fachbereichsaufgaben der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks'),
        ('tasks.assign.override', 'Aufgaben umverteilen', 'Darf Aufgaben administrativ umverteilen.', 'global', 'tasks'),
        ('admin.directory.manage', 'Verzeichnisverwaltung', 'Darf Entra-Sync und Gruppen-Mappings pflegen.', 'global', 'admin'),
        ('admin.permissions.manage', 'Berechtigungen verwalten', 'Darf Rollen-Bundles und Benutzer-Overrides pflegen.', 'global', 'admin')
)
INSERT INTO app_permissions (permission_key, name, description, scope_kind, category, is_active)
SELECT permission_key, name, description, scope_kind, category, TRUE
FROM permission_seed
ON CONFLICT (permission_key) DO UPDATE
SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    scope_kind = EXCLUDED.scope_kind,
    category = EXCLUDED.category,
    is_active = EXCLUDED.is_active;

WITH role_permission_seed(role_key, permission_key) AS (
    VALUES
        ('auth_admin', 'app.access'),
        ('auth_admin', 'users.view_all_departments'),
        ('auth_admin', 'workflows.view_all'),
        ('auth_admin', 'workflows.create.onboarding'),
        ('auth_admin', 'workflows.create.offboarding'),
        ('auth_admin', 'workflows.create.department_change'),
        ('auth_admin', 'workflows.create.position_change'),
        ('auth_admin', 'workflows.create.role_change'),
        ('auth_admin', 'workflows.create.name_change'),
        ('auth_admin', 'tasks.execute.supervisor'),
        ('auth_admin', 'tasks.execute.department'),
        ('auth_admin', 'tasks.assign.override'),
        ('auth_admin', 'admin.directory.manage'),
        ('auth_admin', 'admin.permissions.manage'),

        ('auth_hr', 'app.access'),
        ('auth_hr', 'users.view_all_departments'),
        ('auth_hr', 'workflows.view_all'),
        ('auth_hr', 'workflows.create.onboarding'),
        ('auth_hr', 'workflows.create.offboarding'),
        ('auth_hr', 'workflows.create.department_change'),
        ('auth_hr', 'workflows.create.position_change'),
        ('auth_hr', 'workflows.create.role_change'),
        ('auth_hr', 'workflows.create.name_change'),

        ('auth_manager', 'app.access'),
        ('auth_manager', 'users.view_department'),
        ('auth_manager', 'workflows.view_department'),
        ('auth_manager', 'workflows.create.department_change'),
        ('auth_manager', 'workflows.create.position_change'),
        ('auth_manager', 'workflows.create.role_change'),
        ('auth_manager', 'workflows.create.name_change'),
        ('auth_manager', 'tasks.execute.supervisor'),

        ('auth_worker', 'app.access'),
        ('auth_worker', 'tasks.execute.department'),

        ('auth_reader', 'app.access')
)
INSERT INTO app_role_permissions (app_role_id, app_permission_id)
SELECT r.id, p.id
FROM role_permission_seed seed
JOIN app_roles r ON r.role_key = seed.role_key
JOIN app_permissions p ON p.permission_key = seed.permission_key
ON CONFLICT (app_role_id, app_permission_id) DO NOTHING;

UPDATE app_users u
SET
    directory_synced = EXISTS (
        SELECT 1
        FROM directory_identities di
        WHERE di.app_user_id = u.id
    ),
    department_source = CASE
        WHEN department_override_active THEN 'override'
        WHEN EXISTS (
            SELECT 1
            FROM directory_identities di
            WHERE di.app_user_id = u.id
              AND di.department_name IS NOT NULL
              AND BTRIM(di.department_name) <> ''
        ) THEN 'directory'
        WHEN u.department_id IS NULL THEN 'unassigned'
        ELSE 'local'
    END;
