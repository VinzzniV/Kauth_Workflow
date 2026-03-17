ALTER TABLE app_responsibilities
ADD COLUMN IF NOT EXISTS system_key VARCHAR(64);

UPDATE app_responsibilities
SET system_key = CASE responsibility_key
    WHEN 'it_ad' THEN 'ad'
    WHEN 'it_mailbox' THEN 'mailbox'
    WHEN 'it_habel' THEN 'habel'
    WHEN 'it_ln' THEN 'ln'
    WHEN 'it_hardware' THEN 'hardware'
    WHEN 'qs_babtec' THEN 'babtec'
    WHEN 'av_gewatec' THEN 'gewatec'
    WHEN 'av_provis' THEN 'provis'
    WHEN 'qmb_consense' THEN 'consense'
    ELSE system_key
END
WHERE responsibility_type = 'application';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'app_responsibilities'::regclass
          AND conname = 'chk_app_responsibilities_application_system_key'
    ) THEN
        ALTER TABLE app_responsibilities
        ADD CONSTRAINT chk_app_responsibilities_application_system_key
            CHECK (responsibility_type <> 'application' OR system_key IS NOT NULL);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = current_schema()
          AND indexname IN (
              'uq_app_responsibilities_system_key',
              'app_responsibilities_system_key_key'
          )
    ) THEN
        CREATE UNIQUE INDEX uq_app_responsibilities_system_key
            ON app_responsibilities(system_key)
            WHERE system_key IS NOT NULL;
    END IF;
END $$;

DO $$
DECLARE
    system_key_check_name text;
BEGIN
    SELECT conname
    INTO system_key_check_name
    FROM pg_constraint
    WHERE conrelid = 'system_responsibilities'::regclass
      AND contype = 'c'
      AND pg_get_constraintdef(oid) LIKE '%system_key%';

    IF system_key_check_name IS NOT NULL THEN
        EXECUTE format(
            'ALTER TABLE system_responsibilities DROP CONSTRAINT %I',
            system_key_check_name
        );
    END IF;
END $$;

ALTER TABLE system_responsibilities
ALTER COLUMN system_key TYPE VARCHAR(64);

UPDATE system_responsibilities sr
SET system_key = r.system_key
FROM app_responsibilities r
WHERE r.id = sr.app_responsibility_id
  AND r.system_key IS NOT NULL
  AND sr.system_key <> r.system_key;
