UPDATE workflow_answer_definitions
SET title = 'Consense-User anlegen?',
    description = 'Soll fuer die neue Person ein Consense-User angelegt werden?',
    icon_key = 'consense'
WHERE answer_key = 'consense_requested';

UPDATE workflow_answer_definitions
SET title = 'Consense-Zugang vorhanden?',
    description = 'Hat die Person einen aktiven Consense-User?',
    icon_key = 'consense'
WHERE answer_key = 'ob_has_consense';

UPDATE workflow_answer_definitions
SET title = 'Consense-Zugang anpassen?',
    description = 'Muss der Consense-Zugang fuer die neue Abteilung angepasst oder neu eingerichtet werden?',
    icon_key = 'consense'
WHERE answer_key = 'dc_has_consense';

UPDATE workflow_answer_definitions
SET title = 'Consense-Zugang anpassen?',
    description = 'Muessen Consense-Berechtigungen wegen der neuen Position angepasst werden?',
    icon_key = 'consense'
WHERE answer_key = 'pc_has_consense';

UPDATE workflow_answer_definitions
SET title = 'Consense-Zugang anpassen?',
    description = 'Muessen Consense-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?',
    icon_key = 'consense'
WHERE answer_key = 'rc_has_consense';

UPDATE task_templates
SET title = 'Consense User anlegen',
    description = 'Consense-User fuer die neue Person anlegen.',
    icon_key = 'consense',
    is_active = TRUE
WHERE template_key = 'consense_setup';

DELETE FROM task_template_conditions
WHERE task_template_id IN (
    SELECT id
    FROM task_templates
    WHERE template_key = 'consense_training'
);

DELETE FROM task_template_dependencies
WHERE task_template_id IN (
        SELECT id
        FROM task_templates
        WHERE template_key = 'consense_training'
    )
   OR depends_on_task_template_id IN (
        SELECT id
        FROM task_templates
        WHERE template_key = 'consense_training'
    );

UPDATE task_templates
SET is_active = FALSE
WHERE template_key = 'consense_training';

UPDATE task_templates
SET title = 'Consense-User deaktivieren',
    description = 'Consense-Zugang der ausscheidenden Person deaktivieren.',
    icon_key = 'consense'
WHERE template_key = 'ob_consense_user_disable';

UPDATE task_templates
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Berechtigungen auf die neue Abteilung umstellen.',
    icon_key = 'consense'
WHERE template_key = 'dc_consense_access_update';

UPDATE task_templates
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Berechtigungen auf die neue Position umstellen.',
    icon_key = 'consense'
WHERE template_key = 'pc_consense_access_update';

UPDATE task_templates
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Rollen oder Berechtigungen an die neue Rolle anpassen.',
    icon_key = 'consense'
WHERE template_key = 'rc_consense_access_update';

UPDATE workflow_tasks
SET title = 'Consense User anlegen',
    description = 'Consense-User fuer die neue Person anlegen.',
    icon_key = 'consense'
WHERE task_key = 'consense_setup';

UPDATE workflow_tasks
SET title = 'Consense-User deaktivieren',
    description = 'Consense-Zugang der ausscheidenden Person deaktivieren.',
    icon_key = 'consense'
WHERE task_key = 'ob_consense_user_disable';

UPDATE workflow_tasks
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Berechtigungen auf die neue Abteilung umstellen.',
    icon_key = 'consense'
WHERE task_key = 'dc_consense_access_update';

UPDATE workflow_tasks
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Berechtigungen auf die neue Position umstellen.',
    icon_key = 'consense'
WHERE task_key = 'pc_consense_access_update';

UPDATE workflow_tasks
SET title = 'Consense-Zugang anpassen',
    description = 'Consense-Rollen oder Berechtigungen an die neue Rolle anpassen.',
    icon_key = 'consense'
WHERE task_key = 'rc_consense_access_update';

DELETE FROM workflow_tasks
WHERE task_key = 'consense_training';
