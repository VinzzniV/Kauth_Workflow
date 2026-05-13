using System.Text.Json;
using Xunit;

namespace API.Tests;

// Etappe 9a Schritt 7 Sub-C: Tests fuer den CreateMailboxGraphHandler. Provisioner ist gefaket;
// die echten Graph-SDK-Calls sind in den GraphMailboxProvisionerOutcomeTests fuer Helper-Pfade
// + dem manuellen E2E gegen Test-Tenant abgedeckt.
public sealed class CreateMailboxGraphHandlerTests
{
    private const string ValidUpn = "john.doe@example.com";
    private const string ValidSkuIdString = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid ValidSkuId = new(ValidSkuIdString);
    private const string ValidPayloadJson = """
{
  "userPrincipalName": "john.doe@example.com",
  "skuId": "11111111-2222-3333-4444-555555555555"
}
""";

    [Fact]
    public async Task ExecuteAsync_Provisioned_ReturnsSuccessWithOutput()
    {
        var sentAt = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);
        var provisioner = new FakeGraphMailboxProvisioner
        {
            OutcomeFactory = _ => new GraphMailboxProvisionOutcome.Provisioned("john.doe@example.com", ValidSkuId, sentAt),
        };
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal("john.doe@example.com", output.GetProperty("primarySmtpAddress").GetString());
        Assert.Equal(ValidSkuIdString, output.GetProperty("licenseSkuId").GetString());
        Assert.Equal(sentAt, output.GetProperty("assignedAtUtc").GetDateTime());

        Assert.Single(provisioner.Calls);
        Assert.Equal(ValidUpn, provisioner.Calls[0].UserPrincipalName);
        Assert.Equal(ValidSkuId, provisioner.Calls[0].SkuId);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotInDirectoryYet_ReturnsTransientWithSyncInfoLog()
    {
        var provisioner = new FakeGraphMailboxProvisioner
        {
            OutcomeFactory = _ => new GraphMailboxProvisionOutcome.UserNotInDirectoryYet("User not yet in Entra"),
        };
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindTransient, result.FailureKind);
        Assert.Contains(result.Logs, log => log.Level == "info" && log.Message.Contains("Awaiting Entra Connect sync"));
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("User not yet in Entra"));
    }

    [Fact]
    public async Task ExecuteAsync_MailboxProvisioningInProgress_ReturnsTransientWithProvisioningInfoLog()
    {
        var provisioner = new FakeGraphMailboxProvisioner
        {
            OutcomeFactory = _ => new GraphMailboxProvisionOutcome.MailboxProvisioningInProgress("SMTP not published yet"),
        };
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindTransient, result.FailureKind);
        Assert.Contains(result.Logs, log =>
            log.Level == "info" && log.Message.Contains("Exchange Online provisioning still pending"));
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsFailurePermanent()
    {
        var provisioner = new FakeGraphMailboxProvisioner
        {
            OutcomeFactory = _ => new GraphMailboxProvisionOutcome.PermanentFailure("Unknown skuId", 400),
        };
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("Graph HTTP 400", result.ErrorMessage);
        Assert.Contains("Unknown skuId", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsFailureTransient()
    {
        var provisioner = new FakeGraphMailboxProvisioner
        {
            OutcomeFactory = _ => new GraphMailboxProvisionOutcome.TransientFailure("Service Unavailable", 503),
        };
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindTransient, result.FailureKind);
        Assert.Contains("Graph HTTP 503", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUserPrincipalName_PermanentValidationFailure()
    {
        var provisioner = new FakeGraphMailboxProvisioner();
        var handler = new CreateMailboxGraphHandler(provisioner);
        var payload = """{"skuId":"11111111-2222-3333-4444-555555555555"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("userPrincipalName", result.ErrorMessage);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UpnWithoutAtSign_PermanentValidationFailure()
    {
        var provisioner = new FakeGraphMailboxProvisioner();
        var handler = new CreateMailboxGraphHandler(provisioner);
        var payload = """{"userPrincipalName":"not-a-upn","skuId":"11111111-2222-3333-4444-555555555555"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("missing '@'", result.ErrorMessage);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSkuId_PermanentValidationFailure()
    {
        var provisioner = new FakeGraphMailboxProvisioner();
        var handler = new CreateMailboxGraphHandler(provisioner);
        var payload = """{"userPrincipalName":"x@y.z"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("skuId", result.ErrorMessage);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSkuId_PermanentValidationFailure()
    {
        var provisioner = new FakeGraphMailboxProvisioner();
        var handler = new CreateMailboxGraphHandler(provisioner);
        var payload = """{"userPrincipalName":"x@y.z","skuId":"not-a-uuid"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("must be a UUID", result.ErrorMessage);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_PermanentFailure()
    {
        var provisioner = new FakeGraphMailboxProvisioner();
        var handler = new CreateMailboxGraphHandler(provisioner);

        var result = await handler.ExecuteAsync(BuildContext("\"a-string\""));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("JSON object", result.ErrorMessage);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerKey_IsCreateMailboxGraph()
    {
        var handler = new CreateMailboxGraphHandler(new FakeGraphMailboxProvisioner());
        Assert.Equal("CreateMailboxGraph", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkflowAutomationHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        ActionKey = "CreateMailboxGraph",
        AttemptNumber = 1,
        Payload = JsonDocument.Parse(payloadJson).RootElement,
    };

    private sealed class FakeGraphMailboxProvisioner : IGraphMailboxProvisioner
    {
        public Func<GraphMailboxProvisionRequest, GraphMailboxProvisionOutcome> OutcomeFactory { get; set; }
            = _ => new GraphMailboxProvisionOutcome.Provisioned("x@y.z", Guid.NewGuid(), DateTime.UtcNow);

        public List<GraphMailboxProvisionRequest> Calls { get; } = new();

        public Task<GraphMailboxProvisionOutcome> AssignExchangeLicenseAsync(GraphMailboxProvisionRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(OutcomeFactory(request));
        }
    }
}
