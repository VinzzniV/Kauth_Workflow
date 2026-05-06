using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Npgsql;

namespace API;

internal sealed class AdminRuntimeHealthService : IAdminRuntimeHealthService
{
    private readonly LifecycleRuntimeSettings _runtimeSettings;
    private readonly INotificationEmailConfigurationService _mailConfigService;
    private readonly IDirectorySyncService _directorySyncService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminRuntimeHealthService(
        LifecycleRuntimeSettings runtimeSettings,
        INotificationEmailConfigurationService mailConfigService,
        IDirectorySyncService directorySyncService,
        IHttpClientFactory httpClientFactory)
    {
        _runtimeSettings = runtimeSettings;
        _mailConfigService = mailConfigService;
        _directorySyncService = directorySyncService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AdminRuntimeHealthDto> GetRuntimeHealthAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;

        var dbCheckTask = CheckDatabaseAsync(cancellationToken);
        var authCheckTask = CheckAuthAsync(cancellationToken);
        var mailConfigTask = _mailConfigService.GetAdminConfiguration(cancellationToken);
        var syncStatusTask = _directorySyncService.GetSyncStatusAsync(cancellationToken);
        var pendingImportsTask = _directorySyncService.GetPendingImportsAsync(cancellationToken);

        await Task.WhenAll(dbCheckTask, authCheckTask, mailConfigTask, syncStatusTask, pendingImportsTask);

        var hostTask = BuildHostHealthAsync(cancellationToken);

        await Task.WhenAll(dbCheckTask, authCheckTask, mailConfigTask, syncStatusTask, pendingImportsTask, hostTask);

        var application = BuildApplicationHealth();
        var database = BuildDatabaseHealth(await dbCheckTask);
        var auth = BuildAuthHealth(await authCheckTask);
        var mail = BuildMailHealth(await mailConfigTask);
        var dependencies = BuildDependenciesHealth(database, auth, mail);
        var directory = BuildDirectoryHealth(await syncStatusTask, (await pendingImportsTask).TotalCount);
        var storage = BuildStorageHealth();
        var host = await hostTask;

        var overallSeverity = ComputeOverallSeverity(application.Severity, dependencies.Severity, directory.Severity, storage, auth.Mode, host);

        return new AdminRuntimeHealthDto
        {
            GeneratedAt = generatedAt,
            OverallSeverity = overallSeverity,
            Application = application,
            Dependencies = dependencies,
            Directory = directory,
            Storage = storage,
            Host = host
        };
    }

    private static ApplicationHealthDto BuildApplicationHealth()
    {
        var process = Process.GetCurrentProcess();
        var startedAt = process.StartTime.ToUniversalTime();
        var uptimeSeconds = (long)(DateTime.UtcNow - startedAt).TotalSeconds;

        var managedHeapBytes = GC.GetTotalMemory(false);
        var gcInfo = GC.GetGCMemoryInfo();
        long? highThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes > 0
            ? gcInfo.HighMemoryLoadThresholdBytes
            : null;

        var workingSetBytes = process.WorkingSet64;

        ThreadPool.GetAvailableThreads(out var workerAvail, out var completionAvail);
        var threadPool = new ThreadPoolHealthDto
        {
            WorkerThreadsAvailable = workerAvail,
            CompletionPortThreadsAvailable = completionAvail
        };

        var severity = ComputeHeapSeverity(managedHeapBytes, highThresholdBytes);

        return new ApplicationHealthDto
        {
            Severity = severity,
            ProcessStartedAt = startedAt,
            UptimeSeconds = uptimeSeconds,
            ManagedHeapBytes = managedHeapBytes,
            ManagedHeapHighThresholdBytes = highThresholdBytes,
            WorkingSetBytes = workingSetBytes,
            ThreadPool = threadPool
        };
    }

    private async Task<(bool Reachable, long? LatencyMs, string? Error)> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        var connectionString = _runtimeSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, null, "Connection string not configured.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            sw.Stop();
            return (true, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private async Task<(string Reachability, long? LatencyMs, string? Error)> CheckAuthAsync(CancellationToken cancellationToken)
    {
        if (!_runtimeSettings.EntraAuthEnabled)
        {
            return ("not_applicable", null, null);
        }

        var tenantId = _runtimeSettings.EntraTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return ("not_applicable", null, "Entra Tenant ID not configured.");
        }

        var openIdConfigUrl = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("health");
            using var response = await client.GetAsync(openIdConfigUrl, cancellationToken);
            sw.Stop();
            if (response.IsSuccessStatusCode)
            {
                return ("reachable", sw.ElapsedMilliseconds, null);
            }

            return ("unreachable", sw.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ("unreachable", sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private static DependencyHealthDto BuildDatabaseHealth((bool Reachable, long? LatencyMs, string? Error) result)
    {
        var checkedAt = DateTime.UtcNow;
        var severity = ComputeDatabaseSeverity(result.Reachable, result.LatencyMs);
        return new DependencyHealthDto
        {
            Severity = severity,
            Reachable = result.Reachable,
            LastCheckedAt = checkedAt,
            LatencyMs = result.LatencyMs,
            LastError = result.Error
        };
    }

    private AuthDependencyHealthDto BuildAuthHealth((string Reachability, long? LatencyMs, string? Error) result)
    {
        var mode = _runtimeSettings.EntraAuthEnabled ? "entra"
            : _runtimeSettings.DevSimulationEnabled ? "dev-sim"
            : "none";

        var checkedAt = DateTime.UtcNow;
        string severity;
        if (mode == "entra")
        {
            severity = ComputeAuthSeverity(result.Reachability, result.LatencyMs);
        }
        else if (mode == "dev-sim")
        {
            severity = "ok";
        }
        else
        {
            severity = "unknown";
        }

        return new AuthDependencyHealthDto
        {
            Severity = severity,
            Mode = mode,
            Reachability = result.Reachability,
            LastCheckedAt = checkedAt,
            LatencyMs = result.LatencyMs,
            LastError = result.Error
        };
    }

    private static MailDependencyHealthDto BuildMailHealth(AdminNotificationEmailConfigurationDto config)
    {
        var mode = config.Enabled
            ? (config.Mode ?? "enabled")
            : "disabled";

        var severity = ComputeMailSeverity(mode, config.ConfigurationStatus, config.SenderEmail);

        return new MailDependencyHealthDto
        {
            Severity = severity,
            Mode = mode,
            ConfigurationStatus = config.ConfigurationStatus,
            LastProbeAt = config.LastTestAt,
            LastProbeStatus = MapProbeStatus(config.LastTestStatus)
        };
    }

    private static DependenciesHealthDto BuildDependenciesHealth(
        DependencyHealthDto database,
        AuthDependencyHealthDto auth,
        MailDependencyHealthDto mail)
    {
        var authSeverityForAggregate = (auth.Mode is "dev-sim" or "none")
            ? "ok"
            : auth.Severity;

        var severity = AggregateSeverities(database.Severity, authSeverityForAggregate, mail.Severity);

        return new DependenciesHealthDto
        {
            Severity = severity,
            Database = database,
            Auth = auth,
            Mail = mail
        };
    }

    private DirectoryHealthDto BuildDirectoryHealth(DirectorySyncStatusDto syncStatus, int pendingImportsCount)
    {
        var lastSyncAt = syncStatus.LastSyncAt;
        var lastSyncStatusRaw = syncStatus.LastSyncStatus;
        var lastSyncStatus = string.IsNullOrWhiteSpace(lastSyncStatusRaw) ? "never_run" : lastSyncStatusRaw;

        DateTime? nextScheduledSyncAt = null;
        if (_runtimeSettings.DirectorySyncScheduled && lastSyncAt.HasValue)
        {
            nextScheduledSyncAt = lastSyncAt.Value.AddMinutes(_runtimeSettings.DirectorySyncIntervalMinutes);
        }

        var intervalMinutes = _runtimeSettings.DirectorySyncIntervalMinutes;
        var severity = ComputeDirectorySeverity(lastSyncAt, lastSyncStatus, intervalMinutes);

        return new DirectoryHealthDto
        {
            Severity = severity,
            LastSyncAt = lastSyncAt,
            LastSyncStatus = lastSyncStatus,
            LastError = syncStatus.LastError,
            NextScheduledSyncAt = nextScheduledSyncAt,
            PendingImportsCount = pendingImportsCount
        };
    }

    private List<StorageHealthDto> BuildStorageHealth()
    {
        var pathsRaw = _runtimeSettings.RuntimeHealthStoragePaths;
        if (string.IsNullOrWhiteSpace(pathsRaw))
        {
            return [];
        }

        var result = new List<StorageHealthDto>();
        foreach (var entry in pathsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIdx = entry.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            var label = entry[..eqIdx].Trim();
            var path = entry[(eqIdx + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            result.Add(MeasureStoragePath(label, path));
        }

        return result;
    }

    private static StorageHealthDto MeasureStoragePath(string label, string path)
    {
        try
        {
            var drive = new System.IO.DriveInfo(path);
            if (!drive.IsReady)
            {
                return new StorageHealthDto
                {
                    Label = label,
                    Path = path,
                    TotalBytes = 0,
                    FreeBytes = 0,
                    UsedPercent = 0,
                    Severity = "unknown"
                };
            }

            var totalBytes = drive.TotalSize;
            var freeBytes = drive.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usedPercent = totalBytes > 0 ? (double)usedBytes / totalBytes * 100.0 : 0.0;
            var severity = ComputeStorageSeverity(usedPercent);

            return new StorageHealthDto
            {
                Label = label,
                Path = path,
                TotalBytes = totalBytes,
                FreeBytes = freeBytes,
                UsedPercent = Math.Round(usedPercent, 2),
                Severity = severity
            };
        }
        catch
        {
            return new StorageHealthDto
            {
                Label = label,
                Path = path,
                TotalBytes = 0,
                FreeBytes = 0,
                UsedPercent = 0,
                Severity = "unknown"
            };
        }
    }

    private async Task<HostHealthDto?> BuildHostHealthAsync(CancellationToken cancellationToken)
    {
        if (!_runtimeSettings.HostRuntimeHealthEnabled)
        {
            return null;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        try
        {
            return await ReadLinuxHostHealthAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<HostHealthDto?> ReadLinuxHostHealthAsync(CancellationToken cancellationToken)
    {
        var procfsPath = _runtimeSettings.HostRuntimeProcfsPath ?? "/proc";
        var rootPath = _runtimeSettings.HostRuntimeRootPath ?? "/";

        var uptimeText = await File.ReadAllTextAsync(Path.Combine(procfsPath, "uptime"), cancellationToken);
        var loadavgText = await File.ReadAllTextAsync(Path.Combine(procfsPath, "loadavg"), cancellationToken);
        var meminfoText = await File.ReadAllTextAsync(Path.Combine(procfsPath, "meminfo"), cancellationToken);

        var uptimeParts = uptimeText.Trim().Split(' ');
        if (!double.TryParse(uptimeParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uptimeSecs))
        {
            return null;
        }

        var loadavgParts = loadavgText.Trim().Split(' ');
        if (!double.TryParse(loadavgParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var loadAvg1m))
        {
            return null;
        }

        long memTotalKb = 0;
        long memAvailableKb = 0;
        foreach (var line in meminfoText.Split('\n'))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                memTotalKb = ParseMemInfoKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                memAvailableKb = ParseMemInfoKb(line);
            }
        }

        if (memTotalKb == 0)
        {
            return null;
        }

        var memTotalBytes = memTotalKb * 1024L;
        var memAvailableBytes = memAvailableKb * 1024L;
        var memUsedBytes = memTotalBytes - memAvailableBytes;
        var memUsedPercent = memTotalBytes > 0 ? (double)memUsedBytes / memTotalBytes * 100.0 : 0.0;

        long rootFsTotalBytes = 0;
        long rootFsFreeBytes = 0;
        double rootFsUsedPercent = 0.0;

        try
        {
            var drive = new System.IO.DriveInfo(rootPath);
            if (drive.IsReady)
            {
                rootFsTotalBytes = drive.TotalSize;
                rootFsFreeBytes = drive.AvailableFreeSpace;
                var rootFsUsedBytes = rootFsTotalBytes - rootFsFreeBytes;
                rootFsUsedPercent = rootFsTotalBytes > 0
                    ? (double)rootFsUsedBytes / rootFsTotalBytes * 100.0
                    : 0.0;
            }
        }
        catch
        {
            // Root FS not measurable — still return other metrics with severity based on mem only
        }

        var memSeverity = ComputeHostMemorySeverity(memUsedPercent);
        var fsSeverity = rootFsTotalBytes > 0 ? ComputeStorageSeverity(rootFsUsedPercent) : "unknown";
        var severity = AggregateSeverities(memSeverity, fsSeverity);

        return new HostHealthDto
        {
            Severity = severity,
            UptimeSeconds = (long)uptimeSecs,
            LoadAverage1m = Math.Round(loadAvg1m, 2),
            MemTotalBytes = memTotalBytes,
            MemAvailableBytes = memAvailableBytes,
            MemUsedPercent = Math.Round(memUsedPercent, 2),
            RootFsTotalBytes = rootFsTotalBytes,
            RootFsFreeBytes = rootFsFreeBytes,
            RootFsUsedPercent = Math.Round(rootFsUsedPercent, 2)
        };
    }

    private static long ParseMemInfoKb(string line)
    {
        var colonIdx = line.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return 0;
        }

        var valueStr = line[(colonIdx + 1)..].Trim().Split(' ')[0];
        return long.TryParse(valueStr, out var value) ? value : 0;
    }

    internal static string ComputeHostMemorySeverity(double usedPercent)
    {
        if (usedPercent > 95.0) return "critical";
        if (usedPercent >= 85.0) return "warning";
        return "ok";
    }

    internal static string ComputeOverallSeverity(
        string applicationSeverity,
        string dependenciesSeverity,
        string directorySeverity,
        IReadOnlyList<StorageHealthDto> storage,
        string authMode,
        HostHealthDto? host = null)
    {
        var severities = new List<string> { applicationSeverity, dependenciesSeverity, directorySeverity };
        foreach (var s in storage)
        {
            severities.Add(s.Severity);
        }

        if (host != null)
        {
            severities.Add(host.Severity);
        }

        return AggregateSeverities([.. severities]);
    }

    internal static string AggregateSeverities(params string[] severities)
    {
        if (severities.Any(s => s == "critical"))
        {
            return "critical";
        }

        if (severities.Any(s => s == "warning"))
        {
            return "warning";
        }

        if (severities.All(s => s is "ok" or "unknown"))
        {
            return severities.Any(s => s == "ok") ? "ok" : "unknown";
        }

        return "unknown";
    }

    internal static string ComputeHeapSeverity(long managedHeapBytes, long? highThresholdBytes)
    {
        if (!highThresholdBytes.HasValue || highThresholdBytes.Value <= 0)
        {
            return "unknown";
        }

        var pct = (double)managedHeapBytes / highThresholdBytes.Value;
        if (pct > 0.90) return "critical";
        if (pct >= 0.75) return "warning";
        return "ok";
    }

    internal static string ComputeDatabaseSeverity(bool reachable, long? latencyMs)
    {
        if (!reachable)
        {
            return "critical";
        }

        if (!latencyMs.HasValue)
        {
            return "ok";
        }

        if (latencyMs.Value > 3000) return "critical";
        if (latencyMs.Value >= 1000) return "warning";
        return "ok";
    }

    internal static string ComputeAuthSeverity(string reachability, long? latencyMs)
    {
        if (reachability != "reachable")
        {
            return "critical";
        }

        if (!latencyMs.HasValue)
        {
            return "ok";
        }

        if (latencyMs.Value > 5000) return "critical";
        if (latencyMs.Value >= 2000) return "warning";
        return "ok";
    }

    internal static string ComputeMailSeverity(string mode, string configurationStatus, string? senderEmail)
    {
        if (mode == "disabled")
        {
            return "ok";
        }

        if (mode == "sandbox")
        {
            return "warning";
        }

        if (mode == "enabled")
        {
            if (configurationStatus == "complete")
            {
                return "ok";
            }

            // incomplete: critical if sender email missing (no viable sending possible)
            return string.IsNullOrWhiteSpace(senderEmail) ? "critical" : "warning";
        }

        return "unknown";
    }

    internal static string ComputeDirectorySeverity(DateTime? lastSyncAt, string lastSyncStatus, int intervalMinutes)
    {
        if (!lastSyncAt.HasValue || lastSyncStatus == "never_run")
        {
            return "unknown";
        }

        if (lastSyncStatus == "failed")
        {
            return "critical";
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var age = DateTime.UtcNow - lastSyncAt.Value;

        if (age > interval * 5)
        {
            return "critical";
        }

        if (lastSyncStatus == "partial" || age >= interval * 2)
        {
            return "warning";
        }

        return "ok";
    }

    internal static string ComputeStorageSeverity(double usedPercent)
    {
        if (usedPercent > 90.0) return "critical";
        if (usedPercent >= 80.0) return "warning";
        return "ok";
    }

    private static string MapProbeStatus(string? lastTestStatus)
    {
        return lastTestStatus switch
        {
            "success" => "success",
            "failure" => "failure",
            null or "" or "never" => "never_run",
            _ => "never_run"
        };
    }
}
