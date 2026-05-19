using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class DisableAdUserLdapsHandlerTests
{
    private const string ValidDn = "CN=Jane Doe,OU=Users,DC=example,DC=local";
    private const string ValidPayloadJson = """{"userDistinguishedName": "CN=Jane Doe,OU=Users,DC=example,DC=local"}""";

    [Fact]
    public async Task ExecuteAsync_DisabledOutcome_ReturnsSuccessWithAlreadyDisabledFalse()
    {
        var disabler = new FakeAdUserDisabler
        {
            OutcomeFactory = dn => new AdDisableOutcome.Disabled(dn),
        };
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(ValidDn, result.Output!.Value.GetProperty("distinguishedName").GetString());
        Assert.False(result.Output.Value.GetProperty("alreadyDisabled").GetBoolean());
        Assert.Single(result.Logs);
        Assert.Equal("info", result.Logs[0].Level);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyDisabledOutcome_ReturnsSuccessWithAlreadyDisabledTrue()
    {
        var disabler = new FakeAdUserDisabler
        {
            OutcomeFactory = dn => new AdDisableOutcome.AlreadyDisabled(dn),
        };
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Output!.Value.GetProperty("alreadyDisabled").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundOutcome_ReturnsFailureWithPermanentKind()
    {
        var disabler = new FakeAdUserDisabler
        {
            OutcomeFactory = dn => new AdDisableOutcome.NotFound(dn),
        };
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains(ValidDn, result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsPermanentKind()
    {
        var disabler = new FakeAdUserDisabler
        {
            OutcomeFactory = _ => new AdDisableOutcome.PermanentFailure("InsufficientAccessRights", 50),
        };
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("LDAP 50", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsTransientKind()
    {
        var disabler = new FakeAdUserDisabler
        {
            OutcomeFactory = _ => new AdDisableOutcome.TransientFailure("Server busy", 51),
        };
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
        Assert.Contains("LDAP 51", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDn_ReturnsFailureWithPermanentKind()
    {
        var disabler = new FakeAdUserDisabler();
        var handler = new DisableAdUserLdapsHandler(disabler);

        var result = await handler.ExecuteAsync(BuildContext("{}"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("userDistinguishedName", result.ErrorMessage);
        Assert.Empty(disabler.DisableCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsFailure()
    {
        var handler = new DisableAdUserLdapsHandler(new FakeAdUserDisabler());

        var result = await handler.ExecuteAsync(BuildContext("\"not-an-object\""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("JSON object", result.ErrorMessage);
    }

    [Fact]
    public async Task ActionKey_IsDisableAdUserLdaps()
    {
        var handler = new DisableAdUserLdapsHandler(new FakeAdUserDisabler());
        Assert.Equal("DisableAdUserLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "DisableAdUserLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
