using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class UpdateAdUserAttributesLdapsHandlerTests
{
    private const string ValidDn = "CN=Jane Doe,OU=Users,DC=example,DC=local";

    private const string ValidPayloadJson = """
        {
          "userDistinguishedName": "CN=Jane Doe,OU=Users,DC=example,DC=local",
          "department": "Engineering",
          "title": "Senior Engineer"
        }
        """;

    [Fact]
    public async Task ExecuteAsync_UpdatedOutcome_ReturnsSuccessWithChangedAttributes()
    {
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = spec => new AdUpdateAttributesOutcome.Updated(
                spec.DistinguishedName,
                spec.Attributes.Keys.ToList()),
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.Value.GetProperty("noChangesNeeded").GetBoolean());
        Assert.True(result.Output.Value.GetProperty("changedAttributes").GetArrayLength() > 0);
        Assert.Single(result.Logs);
        Assert.Equal("info", result.Logs[0].Level);
    }

    [Fact]
    public async Task ExecuteAsync_NoChangesNeededOutcome_ReturnsSuccessWithNoChangesNeededTrue()
    {
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = spec => new AdUpdateAttributesOutcome.NoChangesNeeded(spec.DistinguishedName),
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Output!.Value.GetProperty("noChangesNeeded").GetBoolean());
        Assert.Equal(0, result.Output.Value.GetProperty("changedAttributes").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundOutcome_ReturnsFailureWithPermanentKind()
    {
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = spec => new AdUpdateAttributesOutcome.NotFound(spec.DistinguishedName),
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains(ValidDn, result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsPermanentKind()
    {
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = _ => new AdUpdateAttributesOutcome.PermanentFailure("InsufficientAccessRights", 50),
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("LDAP 50", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsTransientKind()
    {
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = _ => new AdUpdateAttributesOutcome.TransientFailure("Server busy", 51),
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
        Assert.Contains("LDAP 51", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUserDn_ReturnsFailureWithPermanentKind()
    {
        var updater = new FakeAdUserAttributeUpdater();
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var result = await handler.ExecuteAsync(BuildContext("""{"department":"Engineering"}"""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Contains("userDistinguishedName", result.ErrorMessage);
        Assert.Empty(updater.UpdateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NoWhitelistField_ReturnsFailureWithPermanentKind()
    {
        var updater = new FakeAdUserAttributeUpdater();
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        // Payload hat DN aber kein Whitelist-Feld (und auch kein UPN o. Ä.)
        var result = await handler.ExecuteAsync(
            BuildContext("""{"userDistinguishedName":"CN=Jane Doe,OU=Users,DC=example,DC=local"}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Empty(updater.UpdateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NullValueForWhitelistField_PassesClearSemantic()
    {
        var capturedSpec = (AdUserAttributeUpdateSpec?)null;
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = spec =>
            {
                capturedSpec = spec;
                return new AdUpdateAttributesOutcome.Updated(spec.DistinguishedName, spec.Attributes.Keys.ToList());
            },
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        var payload = """{"userDistinguishedName":"CN=Jane Doe,OU=Users,DC=example,DC=local","department":null}""";
        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedSpec);
        Assert.True(capturedSpec!.Attributes.ContainsKey("department"));
        Assert.Null(capturedSpec.Attributes["department"]);
    }

    [Fact]
    public async Task ExecuteAsync_ForbiddenField_IsIgnored()
    {
        var capturedSpec = (AdUserAttributeUpdateSpec?)null;
        var updater = new FakeAdUserAttributeUpdater
        {
            OutcomeFactory = spec =>
            {
                capturedSpec = spec;
                return new AdUpdateAttributesOutcome.Updated(spec.DistinguishedName, spec.Attributes.Keys.ToList());
            },
        };
        var handler = new UpdateAdUserAttributesLdapsHandler(updater);

        // userPrincipalName und sAMAccountName sind außerhalb der Whitelist — werden ignoriert.
        var payload = """
            {
              "userDistinguishedName": "CN=Jane Doe,OU=Users,DC=example,DC=local",
              "title": "Engineer",
              "userPrincipalName": "should.be.ignored@example.local",
              "sAMAccountName": "should_be_ignored"
            }
            """;
        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedSpec);
        Assert.True(capturedSpec!.Attributes.ContainsKey("title"));
        Assert.False(capturedSpec.Attributes.ContainsKey("userPrincipalName"));
        Assert.False(capturedSpec.Attributes.ContainsKey("sAMAccountName"));
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsFailure()
    {
        var handler = new UpdateAdUserAttributesLdapsHandler(new FakeAdUserAttributeUpdater());

        var result = await handler.ExecuteAsync(BuildContext("\"not-an-object\""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("JSON object", result.ErrorMessage);
    }

    [Fact]
    public async Task ActionKey_IsUpdateAdUserAttributesLdaps()
    {
        var handler = new UpdateAdUserAttributesLdapsHandler(new FakeAdUserAttributeUpdater());
        Assert.Equal("UpdateAdUserAttributesLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "UpdateAdUserAttributesLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
