using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace API;

// Aggregator fuer die Read-only-Sicht der aktiven Laufzeit-Konfiguration.
// Reflektiert was im DI-Container steht — d.h. die Werte, mit denen die API
// tatsaechlich laeuft, nicht nur das was in der .env.prod-Datei steht.
//
// Wichtig zur DI-Form (siehe LifecycleServiceCollectionExtensions.cs):
// - LifecycleRuntimeSettings ist als direkter Singleton registriert (Zeile 93)
// - WorkflowAutomationRetrySettings ebenfalls direkter Singleton (Zeile 152)
// - WorkerLeaseSettings ebenfalls direkter Singleton (Zeile 153)
// - NotificationEmailOptions ist als IOptions<T> via GetSection gebunden (Zeile 182)
// Konfig-Werte wie KAUTH_VAULT_KEY werden direkt aus IConfiguration gelesen,
// damit der Endpoint die echte Env-Var-Sicht spiegelt.
internal sealed class SystemConfigSnapshotService(
    LifecycleRuntimeSettings runtimeSettings,
    IOptions<NotificationEmailOptions> emailOptions,
    WorkflowAutomationRetrySettings retrySettings,
    WorkerLeaseSettings workerLeaseSettings,
    IConfiguration configuration) : ISystemConfigSnapshotService
{
    public SystemConfigSnapshotDto BuildSnapshot()
    {
        var email = emailOptions.Value;

        return new SystemConfigSnapshotDto
        {
            Environment = runtimeSettings.EnvironmentName,
            IsProduction = runtimeSettings.IsProduction,
            AuthMode = runtimeSettings.AuthMode,
            SwaggerEnabled = runtimeSettings.SwaggerEnabled,
            PublicBaseUrl = runtimeSettings.PublicBaseUrl,
            Entra = new SystemConfigEntraDto
            {
                TenantId = runtimeSettings.EntraTenantId,
                ClientId = runtimeSettings.EntraClientId,
                Audience = runtimeSettings.EntraAudience,
                ClientSecretStatus = RedactPresence(runtimeSettings.EntraClientSecret),
                GraphClientSecretStatus = RedactPresence(runtimeSettings.GraphClientSecret)
            },
            Directory = new SystemConfigDirectoryDto
            {
                SyncScheduled = runtimeSettings.DirectorySyncScheduled,
                SyncIntervalMinutes = runtimeSettings.DirectorySyncIntervalMinutes,
                GroupPrefix = runtimeSettings.DirectoryGroupPrefix,
                ExplicitGroupIds = runtimeSettings.DirectoryExplicitGroupIds,
                AutoProvisionDefaultRoleKey = runtimeSettings.AutoProvisionDefaultRoleKey
            },
            Email = new SystemConfigEmailDto
            {
                Enabled = email.Enabled,
                Provider = email.Provider,
                SenderEmail = email.SenderEmail,
                FrontendBaseUrl = email.FrontendBaseUrl,
                SaveToSentItems = email.SaveToSentItems
            },
            Vault = new SystemConfigVaultDto
            {
                KeyStatus = RedactPresence(configuration["KAUTH_VAULT_KEY"], presentLabel: "present", missingLabel: "missing")
            },
            Retry = new SystemConfigRetryDto
            {
                MaxAttempts = retrySettings.MaxAttempts,
                FirstRetryDelaySeconds = (int)retrySettings.FirstRetryDelay.TotalSeconds,
                SubsequentRetryDelaySeconds = (int)retrySettings.SubsequentRetryDelay.TotalSeconds
            },
            WorkerLease = new SystemConfigWorkerLeaseDto
            {
                StaleClaimTimeoutMinutes = (int)workerLeaseSettings.StaleClaimTimeout.TotalMinutes
            },
            HostHealth = new SystemConfigHostHealthDto
            {
                Enabled = runtimeSettings.HostRuntimeHealthEnabled,
                ProcfsPath = runtimeSettings.HostRuntimeProcfsPath,
                RootPath = runtimeSettings.HostRuntimeRootPath,
                StoragePaths = runtimeSettings.RuntimeHealthStoragePaths
            }
        };
    }

    private static string RedactPresence(string? value, string presentLabel = "set", string missingLabel = "unset")
    {
        return string.IsNullOrWhiteSpace(value) ? missingLabel : presentLabel;
    }
}
