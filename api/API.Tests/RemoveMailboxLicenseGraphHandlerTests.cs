using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class RemoveMailboxLicenseGraphHandlerTests
{
    private const string ValidUpn = "jane.doe@example.com";
    private const string ValidSkuIdString = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid ValidSkuId = new(ValidSkuIdString);
    private const string ValidPayloadJson = """
{
  "userPrincipalName": "jane.doe@example.com",
  "skuId": "11111111-2222-3333-4444-555555555555"
}
""";

    [Fact]
    public async Task ExecuteAsync_LicenseRemoved_ReturnsSuccessWithOutput()
    {
        var removedAt = new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);
        var deprovisioner = new FakeGraphMailboxDeprovisioner
        {
            OutcomeFactory = _ => new GraphMailboxDeprovisionOutcome.LicenseRemoved(ValidUpn, ValidSkuId, removedAt),
        };
        var handler = new RemoveMailboxLicenseGraphHandler(deprovisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal(ValidUpn, output.GetProperty("userPrincipalName").GetString());
        Assert.Equal(ValidSkuIdString, output.GetProperty("licenseSkuId").GetString());
        Assert.False(output.GetProperty("licenseNotAssigned").GetBoolean());
        Assert.Equal(removedAt, output.GetProperty("removedAtUtc").GetDateTime());

        Assert.Single(deprovisioner.Calls);
        Assert.Equal(ValidUpn, deprovisioner.Calls[0].UserPrincipalName);
        Assert.Equal(ValidSkuId, deprovisioner.Calls[0].SkuId);
    }

    [Fact]
    public async Task ExecuteAsync_LicenseNotAssigned_ReturnsSuccessIdempotent()
    {
        var deprovisioner = new FakeGraphMailboxDeprovisioner
        {
            OutcomeFactory = _ => new GraphMailboxDeprovisionOutcome.LicenseNotAssigned("SKU not assigned (idempotent)."),
        };
        var handler = new RemoveMailboxLicenseGraphHandler(deprovisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.True(result.IsSuccess);
        var output = result.Output!.Value;
        Assert.True(output.GetProperty("licenseNotAssigned").GetBoolean());
        Assert.Equal(JsonValueKind.Null, output.GetProperty("removedAtUtc").ValueKind);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsPermanentFailure()
    {
        var deprovisioner = new FakeGraphMailboxDeprovisioner
        {
            OutcomeFactory = _ => new GraphMailboxDeprovisionOutcome.UserNotFound("User not in Entra."),
        };
        var handler = new RemoveMailboxLicenseGraphHandler(deprovisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsPermanentKind()
    {
        var deprovisioner = new FakeGraphMailboxDeprovisioner
        {
            OutcomeFactory = _ => new GraphMailboxDeprovisionOutcome.PermanentFailure("Permission denied.", 403),
        };
        var handler = new RemoveMailboxLicenseGraphHandler(deprovisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("403", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsTransientKind()
    {
        var deprovisioner = new FakeGraphMailboxDeprovisioner
        {
            OutcomeFactory = _ => new GraphMailboxDeprovisionOutcome.TransientFailure("Service unavailable.", 503),
        };
        var handler = new RemoveMailboxLicenseGraphHandler(deprovisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindTransient, result.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUpn_ReturnsPermanentFailure()
    {
        var handler = new RemoveMailboxLicenseGraphHandler(new FakeGraphMailboxDeprovisioner());
        var payload = """{"skuId":"11111111-2222-3333-4444-555555555555"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("userPrincipalName", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSkuId_ReturnsPermanentFailure()
    {
        var handler = new RemoveMailboxLicenseGraphHandler(new FakeGraphMailboxDeprovisioner());
        var payload = """{"userPrincipalName":"x@y.z","skuId":"not-a-uuid"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("UUID", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsPermanentFailure()
    {
        var handler = new RemoveMailboxLicenseGraphHandler(new FakeGraphMailboxDeprovisioner());

        var result = await handler.ExecuteAsync(BuildContext("42"));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
    }

    [Fact]
    public async Task ActionKey_IsRemoveMailboxLicense()
    {
        var handler = new RemoveMailboxLicenseGraphHandler(new FakeGraphMailboxDeprovisioner());
        Assert.Equal("RemoveMailboxLicense", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkflowAutomationHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        ActionKey = "RemoveMailboxLicense",
        AttemptNumber = 1,
        Payload = JsonDocument.Parse(payloadJson).RootElement,
    };

    private sealed class FakeGraphMailboxDeprovisioner : IGraphMailboxDeprovisioner
    {
        public Func<GraphMailboxDeprovisionRequest, GraphMailboxDeprovisionOutcome> OutcomeFactory { get; set; }
            = _ => new GraphMailboxDeprovisionOutcome.LicenseRemoved("x@y.z", Guid.NewGuid(), DateTime.UtcNow);

        public List<GraphMailboxDeprovisionRequest> Calls { get; } = new();

        public Task<GraphMailboxDeprovisionOutcome> RemoveExchangeLicenseAsync(
            GraphMailboxDeprovisionRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(OutcomeFactory(request));
        }
    }
}
