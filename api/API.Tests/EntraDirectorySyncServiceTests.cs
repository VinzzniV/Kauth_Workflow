using API.Services.Directory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class EntraDirectorySyncServiceTests
{
    [Fact]
    public async Task SyncAllAsync_MissingConnectionString_FailsBeforeGraphInit()
    {
        var graphClient = new RecordingGraphClient();
        var operations = new RecordingOperations();
        var systemEventLog = new RecordingSystemEventLog();
        var settings = BuildSettings(connectionString: null);

        var service = new EntraDirectorySyncService(
            graphClient,
            operations,
            settings,
            systemEventLog,
            NullLogger<EntraDirectorySyncService>.Instance);

        var result = await service.SyncAllAsync();

        Assert.Equal("failed", result.Status);
        Assert.Equal("ConnectionStrings:Default not configured.", result.ErrorMessage);
        Assert.Equal(0, graphClient.InitializeCalls);
        Assert.Equal(0, operations.UpsertCalls);
        Assert.Contains(systemEventLog.Writes, w => w.EventKey == "directory_sync_missing_connection_string");
    }

    [Fact]
    public async Task SyncAllAsync_GraphMissingCredentials_FailsAndLogsEvent()
    {
        var graphClient = new RecordingGraphClient
        {
            InitResult = new EntraGraphInitResult(EntraGraphInitStatus.MissingCredentials)
        };
        var operations = new RecordingOperations();
        var systemEventLog = new RecordingSystemEventLog();
        var settings = BuildSettings(connectionString: "Host=nope");

        var service = new EntraDirectorySyncService(
            graphClient,
            operations,
            settings,
            systemEventLog,
            NullLogger<EntraDirectorySyncService>.Instance);

        var result = await service.SyncAllAsync(groupPrefixOverride: "P-");

        Assert.Equal("failed", result.Status);
        Assert.Equal(1, graphClient.InitializeCalls);
        Assert.Equal(0, graphClient.LoadGroupsCalls);
        Assert.Equal(0, operations.UpsertCalls);
        Assert.Equal("P-", result.AppliedGroupPrefix);
        Assert.Contains(systemEventLog.Writes, w => w.EventKey == "directory_sync_missing_graph_credentials");
    }

    [Fact]
    public async Task SyncAllAsync_GraphFailed_FailsAndLogsErrorWithDetails()
    {
        var graphClient = new RecordingGraphClient
        {
            InitResult = new EntraGraphInitResult(
                EntraGraphInitStatus.Failed,
                ErrorMessage: "boom",
                ExceptionType: "System.InvalidOperationException")
        };
        var operations = new RecordingOperations();
        var systemEventLog = new RecordingSystemEventLog();
        var settings = BuildSettings(connectionString: "Host=nope");

        var service = new EntraDirectorySyncService(
            graphClient,
            operations,
            settings,
            systemEventLog,
            NullLogger<EntraDirectorySyncService>.Instance);

        var result = await service.SyncAllAsync();

        Assert.Equal("failed", result.Status);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Equal(0, graphClient.LoadGroupsCalls);
        Assert.Equal(0, operations.UpsertCalls);
        Assert.Contains(systemEventLog.Writes, w => w.EventKey == "directory_sync_graph_client_failed");
    }

    private static LifecycleRuntimeSettings BuildSettings(string? connectionString)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Test",
            IsProduction = false,
            AuthMode = "dev-sim",
            DevSimulationEnabled = false,
            EntraAuthEnabled = false,
            SwaggerEnabled = false,
            DirectorySyncEnabled = true,
            ConnectionString = connectionString,
            DirectorySyncScheduled = false,
            DirectorySyncIntervalMinutes = 60
        };
    }

    private sealed class RecordingGraphClient : IEntraGraphClient
    {
        public EntraGraphInitResult InitResult { get; set; } = new(EntraGraphInitStatus.Ready);
        public int InitializeCalls { get; private set; }
        public int LoadGroupsCalls { get; private set; }
        public int LoadMembersCalls { get; private set; }

        public Task<EntraGraphInitResult> InitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCalls++;
            return Task.FromResult(InitResult);
        }

        public Task<IReadOnlyList<EntraSecurityGroup>> LoadSecurityGroupsAsync(CancellationToken cancellationToken)
        {
            LoadGroupsCalls++;
            return Task.FromResult<IReadOnlyList<EntraSecurityGroup>>([]);
        }

        public Task<IReadOnlyList<EntraDirectoryUser>> LoadGroupMembersAsync(string groupId, CancellationToken cancellationToken)
        {
            LoadMembersCalls++;
            return Task.FromResult<IReadOnlyList<EntraDirectoryUser>>([]);
        }
    }

    private sealed class RecordingOperations : IEntraDirectorySyncOperations
    {
        public int UpsertCalls { get; private set; }
        public int InsertMembershipCalls { get; private set; }

        public Task EnsureDirectoryProjectionUserColumnsAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<Dictionary<Guid, long>> UpsertDirectoryIdentitiesBatchAsync(
            Npgsql.NpgsqlConnection connection,
            IReadOnlyList<(Guid EntraObjectId, EntraDirectoryUser User)> validUsers,
            CancellationToken cancellationToken)
        {
            UpsertCalls++;
            return Task.FromResult(new Dictionary<Guid, long>());
        }

        public Task InsertGroupMembershipsBatchAsync(Npgsql.NpgsqlConnection connection, int directoryGroupId, long[] directoryIdentityIds, CancellationToken cancellationToken)
        {
            InsertMembershipCalls++;
            return Task.CompletedTask;
        }

        public Task AutoLinkIdentitiesToAppUsersAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<DirectoryDepartmentSyncResult> EnsureDirectoryDepartmentsExistAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
            => Task.FromResult(new DirectoryDepartmentSyncResult(0, [], 0, []));

        public Task<DirectoryUserProjectionResult> UpdateExistingAppUsersFromDirectoryAsync(Npgsql.NpgsqlConnection connection, DateTime startedAt, CancellationToken cancellationToken)
            => Task.FromResult(new DirectoryUserProjectionResult(0, 0, 0, 0, 0, []));

        public Task EnsureDevelopmentDefaultGroupMappingsAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class RecordingSystemEventLog : ISystemEventLogService
    {
        public List<SystemEventLogWriteModel> Writes { get; } = new();

        public Task<IReadOnlyList<AdminSystemLogEntryDto>> GetAdminLogsAsync(SystemEventLogQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminSystemLogEntryDto>>([]);

        public Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(SystemEventLogQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminSystemLogSummaryDto { TotalCount = 0, InfoCount = 0, WarningCount = 0, ErrorCount = 0, Sources = [] });

        public Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default)
        {
            Writes.Add(model);
            return Task.CompletedTask;
        }
    }
}
