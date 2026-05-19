using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class MoveAdUserOuLdapsHandlerTests
{
    private const string ValidDn = "CN=Jane Doe,OU=OldOU,DC=example,DC=local";
    private const string ValidTargetOu = "OU=NewOU,DC=example,DC=local";
    private const string ValidPayloadJson = """{"userDistinguishedName":"CN=Jane Doe,OU=OldOU,DC=example,DC=local","targetOu":"OU=NewOU,DC=example,DC=local"}""";

    [Fact]
    public async Task ExecuteAsync_MovedOutcome_ReturnsSuccessWithAlreadyInTargetOuFalse()
    {
        var mover = new FakeAdUserMover
        {
            OutcomeFactory = (dn, targetOu) => new AdMoveOutcome.Moved($"CN=Jane Doe,{targetOu}", "OU=OldOU,DC=example,DC=local", targetOu),
        };
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.Value.GetProperty("alreadyInTargetOu").GetBoolean());
        Assert.Equal(ValidTargetOu, result.Output.Value.GetProperty("toOu").GetString());
        Assert.Single(result.Logs);
        Assert.Equal("info", result.Logs[0].Level);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyInTargetOuOutcome_ReturnsSuccessWithAlreadyInTargetOuTrue()
    {
        var mover = new FakeAdUserMover
        {
            OutcomeFactory = (dn, targetOu) => new AdMoveOutcome.AlreadyInTargetOu(dn, targetOu),
        };
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Output!.Value.GetProperty("alreadyInTargetOu").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundOutcome_ReturnsFailureWithPermanentKind()
    {
        var mover = new FakeAdUserMover
        {
            OutcomeFactory = (dn, _) => new AdMoveOutcome.NotFound(dn),
        };
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains(ValidDn, result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsPermanentKind()
    {
        var mover = new FakeAdUserMover
        {
            OutcomeFactory = (_, _) => new AdMoveOutcome.PermanentFailure("InsufficientAccessRights", 50),
        };
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("LDAP 50", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsTransientKind()
    {
        var mover = new FakeAdUserMover
        {
            OutcomeFactory = (_, _) => new AdMoveOutcome.TransientFailure("Server busy", 51),
        };
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
        Assert.Contains("LDAP 51", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUserDn_ReturnsFailureWithPermanentKind()
    {
        var mover = new FakeAdUserMover();
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext("""{"targetOu":"OU=Test,DC=example,DC=local"}"""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("userDistinguishedName", result.ErrorMessage);
        Assert.Empty(mover.MoveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_MissingTargetOu_ReturnsFailureWithPermanentKind()
    {
        var mover = new FakeAdUserMover();
        var handler = new MoveAdUserOuLdapsHandler(mover);

        var result = await handler.ExecuteAsync(BuildContext("""{"userDistinguishedName":"CN=Jane Doe,OU=Old,DC=example,DC=local"}"""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("targetOu", result.ErrorMessage);
        Assert.Empty(mover.MoveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsFailure()
    {
        var handler = new MoveAdUserOuLdapsHandler(new FakeAdUserMover());

        var result = await handler.ExecuteAsync(BuildContext("\"not-an-object\""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("JSON object", result.ErrorMessage);
    }

    [Fact]
    public async Task ActionKey_IsMoveAdUserOuLdaps()
    {
        var handler = new MoveAdUserOuLdapsHandler(new FakeAdUserMover());
        Assert.Equal("MoveAdUserOuLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "MoveAdUserOuLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
