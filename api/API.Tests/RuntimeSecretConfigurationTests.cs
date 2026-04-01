using Microsoft.Extensions.Options;
using Xunit;

namespace API.Tests;

public sealed class RuntimeSecretConfigurationTests
{
    [Fact]
    public async Task GraphConfigurationService_PrefersGraphSecretOverride()
    {
        var service = new GraphApplicationConfigurationService(
            CreateRuntimeSettings(entraClientSecret: "entra-secret", graphClientSecret: "graph-secret"));

        var runtime = await service.GetRuntimeConfiguration();

        Assert.Equal("tenant-id", runtime.TenantId);
        Assert.Equal("client-id", runtime.ClientId);
        Assert.Equal("graph-secret", runtime.ClientSecret);
        Assert.True(runtime.HasClientSecret);
    }

    [Fact]
    public async Task GraphConfigurationService_FallsBackToEntraSecret()
    {
        var service = new GraphApplicationConfigurationService(
            CreateRuntimeSettings(entraClientSecret: "entra-secret", graphClientSecret: null));

        var runtime = await service.GetRuntimeConfiguration();

        Assert.Equal("entra-secret", runtime.ClientSecret);
        Assert.True(runtime.HasClientSecret);
    }

    [Fact]
    public async Task GraphConfigurationService_ReturnsReadOnlyIncompleteAdminConfiguration_WhenSecretMissing()
    {
        var service = new GraphApplicationConfigurationService(
            CreateRuntimeSettings(entraClientSecret: null, graphClientSecret: null));

        var configuration = await service.GetAdminConfiguration();

        Assert.Equal("tenant-id", configuration.TenantId);
        Assert.Equal("client-id", configuration.ClientId);
        Assert.False(configuration.HasClientSecret);
        Assert.Null(configuration.UpdatedAt);
        Assert.Equal("runtime", configuration.ConfigurationSource);
        Assert.Equal("incomplete", configuration.ConfigurationStatus);
        Assert.Equal("Client Secret ist erforderlich.", configuration.ConfigurationMessage);
        Assert.DoesNotContain(
            typeof(AdminGraphApplicationConfigurationDto).GetProperties(),
            property => string.Equals(property.Name, "ClientSecret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NotificationEmailConfigurationService_ReportsReady_WhenRuntimeSecretExists()
    {
        var service = new NotificationEmailConfigurationService(
            new StubNotificationEmailConfigurationRepository
            {
                Stored = CreateStoredNotificationSettings(enabled: true)
            },
            Options.Create(new NotificationEmailOptions
            {
                Enabled = false,
                Provider = "MicrosoftGraph",
                FrontendBaseUrl = "https://onboarding.example.local",
                SaveToSentItems = true
            }),
            new GraphApplicationConfigurationService(
                CreateRuntimeSettings(entraClientSecret: "entra-secret", graphClientSecret: null)));

        var configuration = await service.GetAdminConfiguration();

        Assert.True(configuration.HasClientSecret);
        Assert.Equal("ready", configuration.ConfigurationStatus);
        Assert.Null(configuration.ConfigurationMessage);
    }

    [Fact]
    public async Task NotificationEmailConfigurationService_RejectsEnable_WhenRuntimeSecretMissing()
    {
        var repository = new StubNotificationEmailConfigurationRepository();
        var service = new NotificationEmailConfigurationService(
            repository,
            Options.Create(new NotificationEmailOptions
            {
                Enabled = false,
                Provider = "MicrosoftGraph",
                FrontendBaseUrl = "https://onboarding.example.local",
                SaveToSentItems = true
            }),
            new GraphApplicationConfigurationService(
                CreateRuntimeSettings(entraClientSecret: null, graphClientSecret: null)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAdminConfiguration(
            new AdminNotificationEmailConfigurationUpdateRequest
            {
                Enabled = true,
                SenderEmail = "noreply@example.local",
                FrontendBaseUrl = "https://onboarding.example.local",
                TestRecipientEmail = null,
                SandboxRedirectEmail = null,
                NotifyOnWorkflowCreated = true,
                NotifyOnTaskReady = true,
                NotifyOnWorkflowCompleted = true
            }));

        Assert.Contains("Client Secret ist erforderlich.", ex.Message);
        Assert.Null(repository.LastUpsert);
    }

    private static LifecycleRuntimeSettings CreateRuntimeSettings(
        string? entraClientSecret,
        string? graphClientSecret)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Development",
            IsProduction = false,
            AuthMode = "entra",
            DevSimulationEnabled = false,
            EntraAuthEnabled = true,
            SwaggerEnabled = true,
            DirectorySyncEnabled = true,
            ConnectionString = null,
            PublicBaseUrl = "https://onboarding.example.local",
            EntraTenantId = "tenant-id",
            EntraClientId = "client-id",
            EntraAudience = "api://client-id",
            EntraClientSecret = entraClientSecret,
            GraphClientSecret = graphClientSecret,
            DirectoryGroupPrefix = null,
            DirectoryExplicitGroupIds = null,
            DirectorySyncScheduled = true,
            DirectorySyncIntervalMinutes = 15,
            AutoProvisionDefaultRoleKey = null
        };
    }

    private static StoredNotificationEmailSettings CreateStoredNotificationSettings(bool enabled)
    {
        return new StoredNotificationEmailSettings
        {
            Enabled = enabled,
            SenderEmail = "noreply@example.local",
            FrontendBaseUrl = "https://onboarding.example.local",
            TestRecipientEmail = null,
            SandboxRedirectEmail = null,
            NotifyOnWorkflowCreated = true,
            NotifyOnTaskReady = true,
            NotifyOnWorkflowCompleted = true,
            LastTestStatus = "never",
            LastTestAt = null,
            LastError = null,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class StubNotificationEmailConfigurationRepository : INotificationEmailConfigurationRepository
    {
        public StoredNotificationEmailSettings? Stored { get; set; }
        public NotificationEmailSettingsUpsertModel? LastUpsert { get; private set; }

        public Task<StoredNotificationEmailSettings?> GetSettings(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Stored);
        }

        public Task<StoredNotificationEmailSettings> UpsertSettings(
            NotificationEmailSettingsUpsertModel settings,
            CancellationToken cancellationToken = default)
        {
            LastUpsert = settings;
            Stored = new StoredNotificationEmailSettings
            {
                Enabled = settings.Enabled,
                SenderEmail = settings.SenderEmail,
                FrontendBaseUrl = settings.FrontendBaseUrl,
                TestRecipientEmail = settings.TestRecipientEmail,
                SandboxRedirectEmail = settings.SandboxRedirectEmail,
                NotifyOnWorkflowCreated = settings.NotifyOnWorkflowCreated,
                NotifyOnTaskReady = settings.NotifyOnTaskReady,
                NotifyOnWorkflowCompleted = settings.NotifyOnWorkflowCompleted,
                LastTestStatus = "never",
                LastTestAt = null,
                LastError = null,
                UpdatedAt = DateTime.UtcNow
            };

            return Task.FromResult(Stored);
        }

        public Task<StoredNotificationEmailSettings> UpdateTestStatus(
            string lastTestStatus,
            string? lastError,
            string frontendBaseUrl,
            CancellationToken cancellationToken = default)
        {
            Stored = new StoredNotificationEmailSettings
            {
                Enabled = Stored?.Enabled ?? false,
                SenderEmail = Stored?.SenderEmail,
                FrontendBaseUrl = frontendBaseUrl,
                TestRecipientEmail = Stored?.TestRecipientEmail,
                SandboxRedirectEmail = Stored?.SandboxRedirectEmail,
                NotifyOnWorkflowCreated = Stored?.NotifyOnWorkflowCreated ?? true,
                NotifyOnTaskReady = Stored?.NotifyOnTaskReady ?? true,
                NotifyOnWorkflowCompleted = Stored?.NotifyOnWorkflowCompleted ?? true,
                LastTestStatus = lastTestStatus,
                LastTestAt = DateTime.UtcNow,
                LastError = lastError,
                UpdatedAt = DateTime.UtcNow
            };

            return Task.FromResult(Stored);
        }
    }
}
