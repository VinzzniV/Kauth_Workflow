using Microsoft.Extensions.Options;

namespace API;

internal sealed class NotificationEmailConfigurationService : INotificationEmailConfigurationService
{
    private readonly INotificationEmailConfigurationRepository repository;
    private readonly NotificationEmailOptions defaults;
    private readonly IGraphApplicationConfigurationService graphApplicationConfigurationService;

    public NotificationEmailConfigurationService(
        INotificationEmailConfigurationRepository repository,
        IOptions<NotificationEmailOptions> defaults,
        IGraphApplicationConfigurationService graphApplicationConfigurationService)
    {
        this.repository = repository;
        this.defaults = defaults.Value;
        this.graphApplicationConfigurationService = graphApplicationConfigurationService;
    }

    public async Task<AdminNotificationEmailConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<AdminNotificationEmailConfigurationDto> SaveAdminConfiguration(
        AdminNotificationEmailConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingSettings = await repository.GetSettings(cancellationToken);
        var senderEmail = Normalize(request.SenderEmail);
        var frontendBaseUrl = Normalize(request.FrontendBaseUrl) ?? defaults.FrontendBaseUrl;
        var testRecipientEmail = Normalize(request.TestRecipientEmail);
        var sandboxRedirectEmail = Normalize(request.SandboxRedirectEmail);

        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Frontend-Basis-URL muss eine absolute URL sein.");
        }

        if (senderEmail is not null && !NotificationEmailConfigurationValidator.IsValidEmail(senderEmail))
        {
            throw new InvalidOperationException("Sender-Mailadresse ist ungültig.");
        }

        if (testRecipientEmail is not null && !NotificationEmailConfigurationValidator.IsValidEmail(testRecipientEmail))
        {
            throw new InvalidOperationException("Testempfänger-Mailadresse ist ungültig.");
        }

        if (sandboxRedirectEmail is not null && !NotificationEmailConfigurationValidator.IsValidEmail(sandboxRedirectEmail))
        {
            throw new InvalidOperationException("Sandbox-Weiterleitungsadresse ist ungültig.");
        }

        var graphConfiguration =
            await graphApplicationConfigurationService.GetRuntimeConfiguration(cancellationToken);
        var runtimeCandidate = new NotificationEmailRuntimeConfiguration
        {
            Enabled = request.Enabled,
            Provider = defaults.Provider,
            TenantId = graphConfiguration.TenantId,
            ClientId = graphConfiguration.ClientId,
            ClientSecret = graphConfiguration.ClientSecret,
            SenderEmail = senderEmail,
            FrontendBaseUrl = frontendBaseUrl,
            TestRecipientEmail = testRecipientEmail,
            SandboxRedirectEmail = sandboxRedirectEmail,
            NotifyOnWorkflowCreated = request.NotifyOnWorkflowCreated,
            NotifyOnTaskReady = request.NotifyOnTaskReady,
            NotifyOnWorkflowCompleted = request.NotifyOnWorkflowCompleted,
            SaveToSentItems = defaults.SaveToSentItems,
            HasClientSecret = graphConfiguration.HasClientSecret,
            LastTestStatus = existingSettings?.LastTestStatus ?? "never",
            LastTestAt = existingSettings?.LastTestAt,
            LastError = existingSettings?.LastError,
            UpdatedAt = existingSettings?.UpdatedAt
        };

        var enableValidation = NotificationEmailConfigurationValidator.ValidateForSending(runtimeCandidate);
        if (request.Enabled && !enableValidation.IsReady)
        {
            throw new InvalidOperationException(
                $"Mailversand kann nicht aktiviert werden: {enableValidation.Message}");
        }

        var stored = await repository.UpsertSettings(
            new NotificationEmailSettingsUpsertModel
            {
                Enabled = request.Enabled,
                SenderEmail = senderEmail,
                FrontendBaseUrl = frontendBaseUrl,
                TestRecipientEmail = testRecipientEmail,
                SandboxRedirectEmail = sandboxRedirectEmail,
                NotifyOnWorkflowCreated = request.NotifyOnWorkflowCreated,
                NotifyOnTaskReady = request.NotifyOnTaskReady,
                NotifyOnWorkflowCompleted = request.NotifyOnWorkflowCompleted
            },
            cancellationToken);

        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<AdminNotificationEmailConfigurationDto> UpdateTestStatus(
        string lastTestStatus,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var stored = await repository.UpdateTestStatus(
            lastTestStatus,
            Normalize(lastError),
            defaults.FrontendBaseUrl,
            cancellationToken);
        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<NotificationEmailRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        var graphConfiguration = await graphApplicationConfigurationService.GetRuntimeConfiguration(cancellationToken);
        return Merge(stored, graphConfiguration);
    }

    private async Task<AdminNotificationEmailConfigurationDto> BuildAdminConfiguration(
        StoredNotificationEmailSettings? stored,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var graphConfiguration = await graphApplicationConfigurationService.GetRuntimeConfiguration(cancellationToken);
        var runtime = Merge(stored, graphConfiguration);
        var validation = NotificationEmailConfigurationValidator.ValidateForSending(runtime);

        return new AdminNotificationEmailConfigurationDto
        {
            Enabled = runtime.Enabled,
            Mode = !runtime.Enabled
                ? "disabled"
                : !string.IsNullOrWhiteSpace(runtime.SandboxRedirectEmail)
                    ? "sandbox"
                    : "enabled",
            SenderEmail = runtime.SenderEmail,
            FrontendBaseUrl = runtime.FrontendBaseUrl,
            TestRecipientEmail = runtime.TestRecipientEmail,
            SandboxRedirectEmail = runtime.SandboxRedirectEmail,
            NotifyOnWorkflowCreated = runtime.NotifyOnWorkflowCreated,
            NotifyOnTaskReady = runtime.NotifyOnTaskReady,
            NotifyOnWorkflowCompleted = runtime.NotifyOnWorkflowCompleted,
            LastTestStatus = runtime.LastTestStatus,
            LastTestAt = runtime.LastTestAt,
            LastError = runtime.LastError,
            UpdatedAt = runtime.UpdatedAt,
            HasClientSecret = runtime.HasClientSecret,
            ConfigurationStatus = validation.Status,
            ConfigurationMessage = validation.Message
        };
    }

    private NotificationEmailRuntimeConfiguration Merge(
        StoredNotificationEmailSettings? stored,
        GraphApplicationRuntimeConfiguration graphConfiguration)
    {
        var enabled = stored?.Enabled ?? defaults.Enabled;
        var senderEmail = stored?.SenderEmail ?? Normalize(defaults.SenderEmail);
        var frontendBaseUrl = stored?.FrontendBaseUrl ?? defaults.FrontendBaseUrl;
        var testRecipientEmail = stored?.TestRecipientEmail;
        var sandboxRedirectEmail = Normalize(stored?.SandboxRedirectEmail);
        var notifyOnWorkflowCreated = stored?.NotifyOnWorkflowCreated ?? true;
        var notifyOnTaskReady = stored?.NotifyOnTaskReady ?? true;
        var notifyOnWorkflowCompleted = stored?.NotifyOnWorkflowCompleted ?? true;

        return new NotificationEmailRuntimeConfiguration
        {
            Enabled = enabled,
            Provider = defaults.Provider,
            TenantId = graphConfiguration.TenantId,
            ClientId = graphConfiguration.ClientId,
            ClientSecret = graphConfiguration.ClientSecret,
            SenderEmail = senderEmail,
            FrontendBaseUrl = frontendBaseUrl,
            TestRecipientEmail = testRecipientEmail,
            SandboxRedirectEmail = sandboxRedirectEmail,
            NotifyOnWorkflowCreated = notifyOnWorkflowCreated,
            NotifyOnTaskReady = notifyOnTaskReady,
            NotifyOnWorkflowCompleted = notifyOnWorkflowCompleted,
            SaveToSentItems = defaults.SaveToSentItems,
            HasClientSecret = graphConfiguration.HasClientSecret,
            LastTestStatus = stored?.LastTestStatus ?? "never",
            LastTestAt = stored?.LastTestAt,
            LastError = stored?.LastError,
            UpdatedAt = stored?.UpdatedAt
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
