-- Migration: Add entra_object_id column to app_users for stable Entra identity linking.
-- This UUID is the immutable object identifier from Microsoft Entra ID (formerly Azure AD).
-- It supplements external_key (which is text-based and still useful for local simulation identities).

ALTER TABLE app_users ADD COLUMN IF NOT EXISTS entra_object_id UUID;

CREATE UNIQUE INDEX IF NOT EXISTS uq_app_users_entra_object_id
    ON app_users(entra_object_id)
    WHERE entra_object_id IS NOT NULL;
