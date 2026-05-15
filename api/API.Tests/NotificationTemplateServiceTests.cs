using Xunit;

namespace API.Tests;

public sealed class NotificationTemplateServiceTests
{
    [Fact]
    public async Task UpdateAdminTemplate_RejectsUnknownPlaceholder()
    {
        var repository = new StubNotificationTemplateRepository();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAdminTemplate(
            NotificationTemplateKeys.WorkflowCreated,
            new AdminNotificationTemplateUpdateRequest
            {
                SubjectTemplate = "Neuer Vorgang fuer {{person_name}}",
                BodyTemplate = "Bitte oeffnen: {{workflow_url}}"
            }));

        Assert.Contains("Unbekannte Platzhalter", ex.Message);
        Assert.Contains("{{person_name}}", ex.Message);
        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public async Task UpdateAdminTemplate_AllowsKnownPlaceholdersAndPersistsNormalizedTemplate()
    {
        var repository = new StubNotificationTemplateRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAdminTemplate(
            NotificationTemplateKeys.WorkflowCreated,
            new AdminNotificationTemplateUpdateRequest
            {
                SubjectTemplate = "  {{workflow_label}} gestartet  ",
                BodyTemplate = "Hallo {{recipient_name}},\r\nbitte oeffnen: {{workflow_url}}"
            });

        Assert.Equal(NotificationTemplateKeys.WorkflowCreated, result.TemplateKey);
        Assert.NotNull(repository.LastUpsert);
        Assert.Equal("{{workflow_label}} gestartet", repository.LastUpsert.SubjectTemplate);
        Assert.Equal("Hallo {{recipient_name}},\nbitte oeffnen: {{workflow_url}}", repository.LastUpsert.BodyTemplate);
    }

    private static NotificationTemplateService CreateService(StubNotificationTemplateRepository repository)
    {
        return new NotificationTemplateService(
            repository,
            previewRepository: null!,
            rotationPreviewRepository: null!,
            workflowRepository: null!,
            rotationRepository: null!,
            notificationEmailConfigurationService: null!,
            templateResolver: null!);
    }

    private sealed class StubNotificationTemplateRepository : INotificationTemplateRepository
    {
        public NotificationTemplateUpsertModel? LastUpsert { get; private set; }

        public Task<List<StoredNotificationTemplate>> GetTemplates(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<StoredNotificationTemplate>());

        public Task<StoredNotificationTemplate?> GetTemplate(string templateKey, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredNotificationTemplate?>(null);

        public Task<StoredNotificationTemplate> UpsertTemplate(
            NotificationTemplateUpsertModel model,
            CancellationToken cancellationToken = default)
        {
            LastUpsert = model;
            return Task.FromResult(new StoredNotificationTemplate
            {
                TemplateKey = model.TemplateKey,
                DisplayName = model.DisplayName,
                TriggerDescription = model.TriggerDescription,
                SubjectTemplate = model.SubjectTemplate,
                BodyTemplate = model.BodyTemplate,
                IsSystemLocked = model.IsSystemLocked,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
