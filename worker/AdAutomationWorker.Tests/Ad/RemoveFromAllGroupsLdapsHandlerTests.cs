using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class RemoveFromAllGroupsLdapsHandlerTests
{
    private const string ValidDn = "CN=Jane Doe,OU=Users,DC=example,DC=local";
    private const string ValidPayloadJson = """{"userDistinguishedName": "CN=Jane Doe,OU=Users,DC=example,DC=local"}""";

    private static readonly string[] SampleGroups = new[]
    {
        "CN=Sales,OU=Groups,DC=example,DC=local",
        "CN=AllStaff,OU=Groups,DC=example,DC=local",
    };

    [Fact]
    public async Task ExecuteAsync_AllRemovedOutcome_ReturnsSuccess()
    {
        var remover = new FakeAdGroupMembershipRemover
        {
            OutcomeFactory = _ => new AdRemoveMembershipsOutcome.AllRemoved(SampleGroups, Array.Empty<string>()),
        };
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = result.Output!.Value;
        Assert.Equal(ValidDn, output.GetProperty("userDistinguishedName").GetString());
        Assert.Equal(2, output.GetProperty("removed").GetArrayLength());
        Assert.Equal(0, output.GetProperty("alreadyRemoved").GetArrayLength());
        Assert.Equal(0, output.GetProperty("failed").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_AllRemovedIdempotent_ReturnsSuccess()
    {
        var remover = new FakeAdGroupMembershipRemover
        {
            OutcomeFactory = _ => new AdRemoveMembershipsOutcome.AllRemoved(Array.Empty<string>(), SampleGroups),
        };
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Output!.Value.GetProperty("removed").GetArrayLength());
        Assert.Equal(2, result.Output.Value.GetProperty("alreadyRemoved").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyRemovedWithPermanentFailure_ReturnsPermanentKind()
    {
        var failures = new[] { new GroupRemoveFailure(SampleGroups[1], 50, "InsufficientAccessRights", IsPermanent: true) };
        var remover = new FakeAdGroupMembershipRemover
        {
            OutcomeFactory = _ => new AdRemoveMembershipsOutcome.PartiallyRemoved(
                new[] { SampleGroups[0] }, Array.Empty<string>(), failures),
        };
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.NotNull(result.Output);
        Assert.Equal(1, result.Output!.Value.GetProperty("removed").GetArrayLength());
        Assert.Equal(1, result.Output.Value.GetProperty("failed").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyRemovedWithTransientFailure_ReturnsTransientKind()
    {
        var failures = new[] { new GroupRemoveFailure(SampleGroups[1], 81, "Server unavailable", IsPermanent: false) };
        var remover = new FakeAdGroupMembershipRemover
        {
            OutcomeFactory = _ => new AdRemoveMembershipsOutcome.PartiallyRemoved(
                new[] { SampleGroups[0] }, Array.Empty<string>(), failures),
        };
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsPermanentKind()
    {
        var remover = new FakeAdGroupMembershipRemover
        {
            OutcomeFactory = _ => new AdRemoveMembershipsOutcome.PermanentFailure("InvalidCredentials", 49),
        };
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("LDAP 49", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDn_ReturnsFailureWithPermanentKind()
    {
        var remover = new FakeAdGroupMembershipRemover();
        var handler = new RemoveFromAllGroupsLdapsHandler(remover);

        var result = await handler.ExecuteAsync(BuildContext("{}"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Empty(remover.RemoveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsFailure()
    {
        var handler = new RemoveFromAllGroupsLdapsHandler(new FakeAdGroupMembershipRemover());

        var result = await handler.ExecuteAsync(BuildContext("42"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("JSON object", result.ErrorMessage);
    }

    [Fact]
    public async Task ActionKey_IsRemoveFromAllGroupsLdaps()
    {
        var handler = new RemoveFromAllGroupsLdapsHandler(new FakeAdGroupMembershipRemover());
        Assert.Equal("RemoveFromAllGroupsLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "RemoveFromAllGroupsLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
