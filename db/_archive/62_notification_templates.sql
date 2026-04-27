CREATE TABLE IF NOT EXISTS notification_templates (
    template_key VARCHAR(64) PRIMARY KEY,
    display_name VARCHAR(160) NOT NULL,
    trigger_description TEXT NOT NULL,
    subject_template TEXT NOT NULL,
    body_template TEXT NOT NULL,
    is_system_locked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO notification_templates (
    template_key,
    display_name,
    trigger_description,
    subject_template,
    body_template,
    is_system_locked
)
VALUES
(
    'workflow_created',
    'Vorgang gestartet',
    'Wird ausgelöst, wenn ein neuer Vorgang gestartet wurde und Empfänger für den Startfall auflösbar sind.',
    '{{workflow_label}} gestartet',
    E'Ein neuer {{workflow_label}} wurde gestartet und wartet auf Ihre Bearbeitung.\n\nBitte öffnen Sie den Vorgang über den folgenden Link.',
    FALSE
),
(
    'task_ready',
    'Aufgabe bereit',
    'Wird ausgelöst, wenn aktuell benachrichtigbare offene oder bereitstehende Aufgaben für einen Vorgang vorhanden sind.',
    'Neue Aufgaben für {{recipient_name}}',
    E'Für Sie wurden im {{process_label}} Aufgaben vorbereitet.\n\n{{task_list_text}}\n\nBitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.',
    FALSE
),
(
    'workflow_completed',
    'Vorgang abgeschlossen',
    'Wird ausgelöst, wenn ein Vorgang abgeschlossen ist und der Initiator aktuell benachrichtigt werden kann.',
    '{{process_name}} abgeschlossen',
    E'Der von Ihnen gestartete {{workflow_label}} wurde abgeschlossen.\n\nSie können den Vorgang bei Bedarf über den folgenden Link öffnen.',
    FALSE
),
(
    'upcoming_change',
    'Bevorstehender Wechsel',
    'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell ein bevorstehender Bereichswechsel benachrichtigt werden soll.',
    'Bevorstehender Wechsel: {{person_name}}',
    E'Für den Durchlaufplan {{plan_title}} von {{person_name}} steht ein Bereichswechsel bevor.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.',
    FALSE
),
(
    'reminder',
    'Erinnerung fällige Aufgaben',
    'Wird ausgelöst, wenn für einen aktiven Durchlaufplan heute fällige Rotationsaufgaben benachrichtigt werden sollen.',
    'Fällige Rotationsaufgaben für {{person_name}}',
    E'Für den Durchlaufplan {{plan_title}} von {{person_name}} haben offene Aufgaben heute ihren Fälligkeitstermin erreicht.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.',
    FALSE
),
(
    'overdue',
    'Überfällige Aufgaben',
    'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell überfällige Rotationsaufgaben vorhanden sind.',
    'Überfällige Rotationsaufgaben für {{person_name}}',
    E'Für den Durchlaufplan {{plan_title}} von {{person_name}} gibt es offene Rotationsaufgaben mit überschrittenem Fälligkeitstermin.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.',
    FALSE
)
ON CONFLICT (template_key) DO UPDATE
SET
    display_name = EXCLUDED.display_name,
    trigger_description = EXCLUDED.trigger_description,
    subject_template = COALESCE(notification_templates.subject_template, EXCLUDED.subject_template),
    body_template = COALESCE(notification_templates.body_template, EXCLUDED.body_template),
    is_system_locked = EXCLUDED.is_system_locked,
    updated_at = NOW();
