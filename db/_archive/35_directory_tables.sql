-- Migration: Directory identity and group projection tables.
-- These tables store external identities and groups synced from Microsoft Entra ID (Azure AD).
-- The app maps and enriches these — it does NOT own them as primary source.

-- External identities from the directory (AD/Entra).
CREATE TABLE IF NOT EXISTS directory_identities (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    entra_object_id UUID NOT NULL UNIQUE,
    onprem_object_guid UUID,
    user_principal_name VARCHAR(320) NOT NULL,
    mail          VARCHAR(320),
    display_name  VARCHAR(180) NOT NULL,
    account_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    source_system VARCHAR(64) NOT NULL DEFAULT 'entra',
    is_managed_externally BOOLEAN NOT NULL DEFAULT TRUE,
    app_user_id   BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_directory_identities_onprem_guid
    ON directory_identities(onprem_object_guid) WHERE onprem_object_guid IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_directory_identities_app_user
    ON directory_identities(app_user_id) WHERE app_user_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_directory_identities_upn
    ON directory_identities(user_principal_name);

-- External groups from the directory.
CREATE TABLE IF NOT EXISTS directory_groups (
    id            INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    external_group_id UUID NOT NULL UNIQUE,
    display_name  VARCHAR(260) NOT NULL,
    description   TEXT,
    source_system VARCHAR(64) NOT NULL DEFAULT 'entra',
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Membership: which directory identities belong to which directory groups.
CREATE TABLE IF NOT EXISTS directory_group_members (
    directory_group_id    INTEGER NOT NULL REFERENCES directory_groups(id) ON DELETE CASCADE,
    directory_identity_id BIGINT NOT NULL REFERENCES directory_identities(id) ON DELETE CASCADE,
    synced_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (directory_group_id, directory_identity_id)
);

-- Mapping: which directory groups grant which app roles.
-- This is the central configuration table for group-based role derivation.
CREATE TABLE IF NOT EXISTS directory_group_role_mappings (
    id                    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    directory_group_id    INTEGER NOT NULL REFERENCES directory_groups(id) ON DELETE CASCADE,
    app_role_id           INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    scope                 VARCHAR(64) NOT NULL DEFAULT 'global',
    scope_department_id   INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    is_active             BOOLEAN NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_dgrm_group ON directory_group_role_mappings(directory_group_id);
CREATE INDEX IF NOT EXISTS idx_dgrm_role ON directory_group_role_mappings(app_role_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_dgrm_unique_scope
    ON directory_group_role_mappings(directory_group_id, app_role_id, scope, COALESCE(scope_department_id, -1));

-- Sync log: track sync runs for the admin dashboard.
CREATE TABLE IF NOT EXISTS directory_sync_log (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sync_type     VARCHAR(40) NOT NULL, -- 'groups', 'identities', 'full'
    status        VARCHAR(20) NOT NULL, -- 'success', 'failed', 'partial'
    groups_synced INTEGER NOT NULL DEFAULT 0,
    identities_synced INTEGER NOT NULL DEFAULT 0,
    memberships_synced INTEGER NOT NULL DEFAULT 0,
    error_message TEXT,
    started_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at  TIMESTAMPTZ
);
