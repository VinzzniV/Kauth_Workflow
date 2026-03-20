-- Setzt Requirement-Behavior-Regeln sowie Task-Template-Conditions/-Dependencies
-- vor dem Seed-Run zurück. Die Deletes entfernen veraltete Einträge, die im Seed
-- nicht mehr vorkommen.
-- task_template_conditions sind zusätzlich per Unique-Index + ON CONFLICT abgesichert,
-- damit sich identische Seed-Regeln nicht mehrfach anhäufen.
--
-- Ausführungsreihenfolge: Diese Datei sortiert lexikalisch vor 02_seed.sql
-- ('r' < 's'), wird also von docker-entrypoint-initdb.d zuerst ausgeführt.

DELETE FROM workflow_answer_single_select_keep_values
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'ad_user_requested',
        'comparison_user_available',
        'comparison_user_name',
        'hardware_requested',
        'hardware_available',
        'hardware_type',
        'laptop_vpn_type',
        'phone_requested',
        'internal_drive_access_requested',
        'internal_drive_access_roles'
    )
);

DELETE FROM workflow_answer_reset_rules
WHERE answer_definition_id IN (
        SELECT id
        FROM workflow_answer_definitions
        WHERE answer_key IN (
            'ad_user_requested',
            'comparison_user_available',
            'hardware_requested',
            'hardware_type',
            'internal_drive_access_requested'
        )
    )
   OR target_answer_definition_id IN (
        SELECT id
        FROM workflow_answer_definitions
        WHERE answer_key IN (
            'comparison_user_available',
            'comparison_user_name',
            'hardware_available',
            'hardware_type',
            'laptop_vpn_type',
            'phone_requested',
            'internal_drive_access_roles'
        )
    );

DELETE FROM workflow_answer_validation_rules
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'comparison_user_name',
        'laptop_vpn_type',
        'internal_drive_access_roles'
    )
);

DELETE FROM workflow_answer_visibility_rules
WHERE answer_definition_id IN (
    SELECT id
    FROM workflow_answer_definitions
    WHERE answer_key IN (
        'comparison_user_available',
        'comparison_user_name',
        'hardware_available',
        'hardware_type',
        'laptop_vpn_type',
        'phone_requested',
        'internal_drive_access_roles'
    )
);

DELETE FROM task_template_conditions
WHERE task_template_id IN (
    SELECT id
    FROM task_templates
    WHERE template_key IN (
        'ad_user_create',
        'permissions_from_reference_user',
        'exchange_create',
        'habel_user_create',
        'ln_user_create',
        'internet_access_enable',
        'internal_drive_access_grant',
        'office_install',
        'hardware_procure',
        'hardware_setup',
        'hardware_handover',
        'phone_prepare',
        'catia_install',
        'datev_install',
        'tisoware_install',
        'babtec_user_create',
        'gewatec_user_create',
        'provis_user_create',
        'consense_setup',
        'consense_training'
    )
);

DELETE FROM task_template_dependencies
WHERE task_template_id IN (
    SELECT id
    FROM task_templates
    WHERE template_key IN (
        'supervisor_fills_document',
        'ad_user_create',
        'permissions_from_reference_user',
        'exchange_create',
        'habel_user_create',
        'ln_user_create',
        'internet_access_enable',
        'internal_drive_access_grant',
        'office_install',
        'hardware_procure',
        'hardware_setup',
        'hardware_handover',
        'phone_prepare',
        'catia_install',
        'datev_install',
        'tisoware_install',
        'babtec_user_create',
        'gewatec_user_create',
        'provis_user_create',
        'consense_setup',
        'consense_training'
    )
)
OR depends_on_task_template_id IN (
    SELECT id
    FROM task_templates
    WHERE template_key IN (
        'supervisor_fills_document',
        'ad_user_create',
        'permissions_from_reference_user',
        'exchange_create',
        'habel_user_create',
        'ln_user_create',
        'internet_access_enable',
        'internal_drive_access_grant',
        'office_install',
        'hardware_procure',
        'hardware_setup',
        'hardware_handover',
        'phone_prepare',
        'catia_install',
        'datev_install',
        'tisoware_install',
        'babtec_user_create',
        'gewatec_user_create',
        'provis_user_create',
        'consense_setup',
        'consense_training'
    )
);
