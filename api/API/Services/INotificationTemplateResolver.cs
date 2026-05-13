namespace API;

// Etappe 9a Schritt 5 Sub-C: extrahiert die Catalog+DB-Override-Logik aus der frueher privaten
// NotificationTemplateService.GetEffectiveTemplate. Damit kann ein zweiter Konsument (z. B.
// SendWelcomeMailGraphHandler) das Template ohne Workflow-/Rotation-Kontext laden.
//
// Vertrag: Catalog-Eintrag (NotificationTemplateCatalog) ist die kanonische Default-Quelle.
// DB-Row (notification_templates) ist optionaler Override. Fehlt der Catalog-Eintrag →
// InvalidOperationException (Konfigurations-Bug; jeder Template-Key muss im Catalog stehen).
internal interface INotificationTemplateResolver
{
    Task<StoredNotificationTemplate> ResolveAsync(string templateKey, CancellationToken cancellationToken = default);
}

internal sealed class NotificationTemplateResolver : INotificationTemplateResolver
{
    private readonly INotificationTemplateRepository repository;

    public NotificationTemplateResolver(INotificationTemplateRepository repository)
    {
        this.repository = repository;
    }

    public async Task<StoredNotificationTemplate> ResolveAsync(string templateKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            throw new InvalidOperationException("Template key must not be empty.");
        }

        // Catalog-Eintrag ist Pflicht; fehlt er → Konfigurations-Bug.
        var definition = NotificationTemplateCatalog.GetDefinitionOrThrow(templateKey);

        var stored = await repository.GetTemplate(definition.TemplateKey, cancellationToken);
        if (stored is not null)
        {
            return stored;
        }

        return new StoredNotificationTemplate
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            SubjectTemplate = definition.DefaultSubjectTemplate,
            BodyTemplate = definition.DefaultBodyTemplate,
            IsSystemLocked = false,
            UpdatedAt = null
        };
    }
}
