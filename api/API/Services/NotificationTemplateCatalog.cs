namespace API;

internal static class NotificationTemplateCatalog
{
    private static readonly IReadOnlyList<NotificationTemplateDefinition> Definitions =
    [
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.WorkflowCreated,
            DisplayName = "Vorgang gestartet",
            TriggerDescription = "Wird ausgelöst, wenn ein neuer Vorgang gestartet wurde und Empfänger für den Startfall auflösbar sind.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.Workflow,
            ActionLabel = "Workflow öffnen",
            DefaultSubjectTemplate = "{{workflow_label}} gestartet",
            DefaultBodyTemplate = "Ein neuer {{workflow_label}} wurde gestartet und wartet auf Ihre Bearbeitung.\n\nBitte öffnen Sie den Vorgang über den folgenden Link.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("process_name", "Prozessname", "Name des Prozesstyps oder der Workflow-Definition."),
                CreatePlaceholder("workflow_label", "Workflow-Label", "Zusammengesetztes Workflow-Label auf Basis des Prozessnamens."),
                CreatePlaceholder("workflow_url", "Workflow-Link", "Direkter Link zum Vorgang im Frontend."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.TaskReady,
            DisplayName = "Aufgabe bereit",
            TriggerDescription = "Wird ausgelöst, wenn aktuell benachrichtigbare offene oder bereitstehende Aufgaben für einen Vorgang vorhanden sind.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.Workflow,
            ActionLabel = "Aufgabe öffnen",
            DefaultSubjectTemplate = "Neue Aufgaben für {{recipient_name}}",
            DefaultBodyTemplate = "Für Sie wurden im {{process_label}} Aufgaben vorbereitet.\n\n{{task_list_text}}\n\nBitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("process_name", "Prozessname", "Name des Prozesstyps oder der Workflow-Definition."),
                CreatePlaceholder("process_label", "Prozess-Label", "Lesbares Prozesslabel auf Basis des Prozessnamens."),
                CreatePlaceholder("task_list_text", "Aufgabenliste", "Vorgerenderte Aufgabenliste als Text."),
                CreatePlaceholder("workflow_url", "Workflow-Link", "Direkter Link zum Vorgang oder Aufgabenbereich."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.WorkflowCompleted,
            DisplayName = "Vorgang abgeschlossen",
            TriggerDescription = "Wird ausgelöst, wenn ein Vorgang abgeschlossen ist und der Initiator aktuell benachrichtigt werden kann.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.Workflow,
            ActionLabel = "Workflow öffnen",
            DefaultSubjectTemplate = "{{process_name}} abgeschlossen",
            DefaultBodyTemplate = "Der von Ihnen gestartete {{workflow_label}} wurde abgeschlossen.\n\nSie können den Vorgang bei Bedarf über den folgenden Link öffnen.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("process_name", "Prozessname", "Name des Prozesstyps oder der Workflow-Definition."),
                CreatePlaceholder("workflow_label", "Workflow-Label", "Zusammengesetztes Workflow-Label auf Basis des Prozessnamens."),
                CreatePlaceholder("workflow_url", "Workflow-Link", "Direkter Link zum Vorgang im Frontend."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.UpcomingChange,
            DisplayName = "Bevorstehender Wechsel",
            TriggerDescription = "Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell ein bevorstehender Bereichswechsel benachrichtigt werden soll.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.RotationPlan,
            ActionLabel = "Aufgabe öffnen",
            DefaultSubjectTemplate = "Bevorstehender Wechsel: {{person_name}}",
            DefaultBodyTemplate = "Für den Durchlaufplan {{plan_title}} von {{person_name}} steht ein Bereichswechsel bevor.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("person_name", "Person", "Betroffene Person des Durchlaufplans."),
                CreatePlaceholder("plan_title", "Durchlaufplan", "Titel des Durchlaufplans."),
                CreatePlaceholder("current_department_name", "Aktueller Bereich", "Aktuell relevanter Bereich."),
                CreatePlaceholder("next_department_name", "Nächster Bereich", "Nächster Bereich im Wechsel."),
                CreatePlaceholder("change_date", "Wechseltermin", "Geplanter Wechseltermin."),
                CreatePlaceholder("task_list_text", "Aufgabenliste", "Vorgerenderte Liste der relevanten offenen Aufgaben."),
                CreatePlaceholder("app_url", "App-Link", "Direkter Link in die Anwendung."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.Reminder,
            DisplayName = "Erinnerung fällige Aufgaben",
            TriggerDescription = "Wird ausgelöst, wenn für einen aktiven Durchlaufplan heute fällige Rotationsaufgaben benachrichtigt werden sollen.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.RotationPlan,
            ActionLabel = "Aufgabe öffnen",
            DefaultSubjectTemplate = "Fällige Rotationsaufgaben für {{person_name}}",
            DefaultBodyTemplate = "Für den Durchlaufplan {{plan_title}} von {{person_name}} haben offene Aufgaben heute ihren Fälligkeitstermin erreicht.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("person_name", "Person", "Betroffene Person des Durchlaufplans."),
                CreatePlaceholder("plan_title", "Durchlaufplan", "Titel des Durchlaufplans."),
                CreatePlaceholder("current_department_name", "Aktueller Bereich", "Aktuell relevanter Bereich."),
                CreatePlaceholder("next_department_name", "Nächster Bereich", "Nächster Bereich im Wechsel."),
                CreatePlaceholder("change_date", "Wechseltermin", "Geplanter Wechseltermin."),
                CreatePlaceholder("task_list_text", "Aufgabenliste", "Vorgerenderte Liste der relevanten offenen Aufgaben."),
                CreatePlaceholder("app_url", "App-Link", "Direkter Link in die Anwendung."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.WelcomeMail,
            DisplayName = "Willkommens-Mail",
            TriggerDescription = "Wird vom SendWelcomeMailGraph-Handler beim Onboarding eines neuen Mitarbeiters versendet (Etappe 9a Schritt 5).",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.Workflow,
            ActionLabel = "Konto-Infos beim Helpdesk abholen",
            DefaultSubjectTemplate = "Willkommen, {{first_name}}",
            DefaultBodyTemplate = "Hallo {{recipient_name}},\n\n" +
                "dein Benutzerkonto wurde angelegt.\n\n" +
                "- Anmelde-Name (UPN): {{user_principal_name}}\n" +
                "- Vorname: {{first_name}}\n" +
                "- Nachname: {{last_name}}\n\n" +
                "Bitte hole dein Initial-Passwort beim Helpdesk ab. Beim ersten Login wirst du gebeten, " +
                "ein eigenes Passwort zu setzen.\n\n" +
                "Viel Erfolg beim Start!",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfaengername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("first_name", "Vorname", "Vorname des neuen Mitarbeiters."),
                CreatePlaceholder("last_name", "Nachname", "Nachname des neuen Mitarbeiters."),
                CreatePlaceholder("user_principal_name", "UPN", "User Principal Name fuer die Anmeldung."),
            ]
        },
        new NotificationTemplateDefinition
        {
            TemplateKey = NotificationTemplateKeys.Overdue,
            DisplayName = "Überfällige Aufgaben",
            TriggerDescription = "Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell überfällige Rotationsaufgaben vorhanden sind.",
            PreviewTargetType = NotificationTemplatePreviewTargetTypes.RotationPlan,
            ActionLabel = "Aufgabe öffnen",
            DefaultSubjectTemplate = "Überfällige Rotationsaufgaben für {{person_name}}",
            DefaultBodyTemplate = "Für den Durchlaufplan {{plan_title}} von {{person_name}} gibt es offene Rotationsaufgaben mit überschrittenem Fälligkeitstermin.\n\n- Aktueller Bereich: {{current_department_name}}\n- Nächster Bereich: {{next_department_name}}\n- Wechseltermin: {{change_date}}\n\n{{task_list_text}}\n\nBitte öffnen Sie die Anwendung über den folgenden Link.",
            Placeholders =
            [
                CreatePlaceholder("recipient_name", "Empfängername", "Anzeigename der empfangenden Person."),
                CreatePlaceholder("person_name", "Person", "Betroffene Person des Durchlaufplans."),
                CreatePlaceholder("plan_title", "Durchlaufplan", "Titel des Durchlaufplans."),
                CreatePlaceholder("current_department_name", "Aktueller Bereich", "Aktuell relevanter Bereich."),
                CreatePlaceholder("next_department_name", "Nächster Bereich", "Nächster Bereich im Wechsel."),
                CreatePlaceholder("change_date", "Wechseltermin", "Geplanter Wechseltermin."),
                CreatePlaceholder("task_list_text", "Aufgabenliste", "Vorgerenderte Liste der relevanten offenen Aufgaben."),
                CreatePlaceholder("app_url", "App-Link", "Direkter Link in die Anwendung."),
            ]
        },
    ];

    public static IReadOnlyList<NotificationTemplateDefinition> GetDefinitions() => Definitions;

    public static NotificationTemplateDefinition GetDefinitionOrThrow(string templateKey)
    {
        return Definitions.FirstOrDefault(definition =>
            string.Equals(definition.TemplateKey, templateKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unbekannter Mail-Template-Key '{templateKey}'.");
    }

    private static AdminNotificationTemplatePlaceholderDto CreatePlaceholder(
        string key,
        string label,
        string description)
    {
        return new AdminNotificationTemplatePlaceholderDto
        {
            Key = key,
            Label = label,
            Description = description
        };
    }
}

internal static class NotificationTemplateKeys
{
    public const string WorkflowCreated = "workflow_created";
    public const string TaskReady = "task_ready";
    public const string WorkflowCompleted = "workflow_completed";
    public const string UpcomingChange = "upcoming_change";
    public const string Reminder = "reminder";
    public const string Overdue = "overdue";
    public const string WelcomeMail = "welcome_mail";
}

internal static class NotificationTemplatePreviewTargetTypes
{
    public const string Workflow = "workflow";
    public const string RotationPlan = "rotation_plan";
}

internal sealed class NotificationTemplateDefinition
{
    public required string TemplateKey { get; init; }
    public required string DisplayName { get; init; }
    public required string TriggerDescription { get; init; }
    public required string PreviewTargetType { get; init; }
    public required string ActionLabel { get; init; }
    public required string DefaultSubjectTemplate { get; init; }
    public required string DefaultBodyTemplate { get; init; }
    public required IReadOnlyList<AdminNotificationTemplatePlaceholderDto> Placeholders { get; init; }
}
