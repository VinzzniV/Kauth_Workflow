using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class AssignGroupsLdapsHandlerTests
{
    private const string ValidPayloadJson = """
{
  "userDistinguishedName": "CN=John Doe,OU=Sales,DC=test,DC=local",
  "groupDistinguishedNames": [
    "CN=SalesTeam,OU=Groups,DC=test,DC=local",
    "CN=AllStaff,OU=Groups,DC=test,DC=local"
  ]
}
""";

    [Fact]
    public async Task ExecuteAsync_AllAdded_ReturnsSuccessWithBothBuckets()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = spec => new AdGroupMembershipOutcome.AllAdded(
                NewlyAddedGroups: new[] { spec.GroupDistinguishedNames[0] },
                AlreadyMemberGroups: new[] { spec.GroupDistinguishedNames[1] }),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal(1, output.GetProperty("newlyAdded").GetArrayLength());
        Assert.Equal(1, output.GetProperty("alreadyMember").GetArrayLength());
        Assert.Equal(0, output.GetProperty("failed").GetArrayLength());
        Assert.Null(result.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_AllAlreadyMember_StillSuccess()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = spec => new AdGroupMembershipOutcome.AllAdded(
                NewlyAddedGroups: Array.Empty<string>(),
                AlreadyMemberGroups: spec.GroupDistinguishedNames.ToArray()),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("already member", result.Logs.Single().Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyAdded_AnyPermanent_ReturnsFailurePermanentWithDetailOutput()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = spec => new AdGroupMembershipOutcome.PartiallyAdded(
                NewlyAddedGroups: new[] { spec.GroupDistinguishedNames[0] },
                AlreadyMemberGroups: Array.Empty<string>(),
                FailedGroups: new[]
                {
                    new GroupFailure(spec.GroupDistinguishedNames[1], 32, "NoSuchObject", IsPermanent: true)
                }),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal(1, output.GetProperty("newlyAdded").GetArrayLength());
        Assert.Equal(1, output.GetProperty("failed").GetArrayLength());
        Assert.Equal(32, output.GetProperty("failed")[0].GetProperty("ldapResultCode").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyAdded_OnlyTransient_ReturnsFailureTransientWithDetailOutput()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = spec => new AdGroupMembershipOutcome.PartiallyAdded(
                NewlyAddedGroups: new[] { spec.GroupDistinguishedNames[0] },
                AlreadyMemberGroups: Array.Empty<string>(),
                FailedGroups: new[]
                {
                    new GroupFailure(spec.GroupDistinguishedNames[1], 81, "ServerDown", IsPermanent: false)
                }),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
        Assert.NotNull(result.Output);
        Assert.Equal(1, result.Output!.Value.GetProperty("failed").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_TopLevelPermanentFailure_NoOutput()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = _ => new AdGroupMembershipOutcome.PermanentFailure("invalid user dn", 32),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("LDAP 32", result.ErrorMessage);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_TopLevelTransientFailure_NoOutput()
    {
        var writer = new FakeAdGroupMembershipWriter
        {
            OutcomeFactory = _ => new AdGroupMembershipOutcome.TransientFailure("server down", 81),
        };
        var handler = new AssignGroupsLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUserDn_PermanentValidation()
    {
        var writer = new FakeAdGroupMembershipWriter();
        var handler = new AssignGroupsLdapsHandler(writer);
        var payload = """{"groupDistinguishedNames":["CN=X,DC=t,DC=local"]}""";

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("userDistinguishedName", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyGroupArray_PermanentValidation()
    {
        var writer = new FakeAdGroupMembershipWriter();
        var handler = new AssignGroupsLdapsHandler(writer);
        var payload = """{"userDistinguishedName":"CN=u","groupDistinguishedNames":[]}""";

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("groupDistinguishedNames", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultActionKey_IsAssignGroupsLdaps()
    {
        var handler = new AssignGroupsLdapsHandler(new FakeAdGroupMembershipWriter());
        Assert.Equal("AssignGroupsLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 99,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "AssignGroupsLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
