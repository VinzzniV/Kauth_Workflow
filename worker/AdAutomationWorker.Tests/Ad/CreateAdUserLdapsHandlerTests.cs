using System.Text.Json;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class CreateAdUserLdapsHandlerTests
{
    private const string ValidPayloadJson = """
{
  "samAccountName": "jdoe",
  "userPrincipalName": "jdoe@example.local",
  "displayName": "John Doe",
  "givenName": "John",
  "surname": "Doe",
  "mail": "jdoe@example.local",
  "targetOu": "OU=Sales,OU=Users,DC=example,DC=local",
  "employeeNumber": "E12345"
}
""";

    [Fact]
    public async Task ExecuteAsync_CreatedOutcome_ReturnsSuccessWithPasswordAndAlreadyExistedFalse()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = spec => new AdWriteOutcome.Created(spec.BuildDistinguishedName()),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal("CN=John Doe,OU=Sales,OU=Users,DC=example,DC=local", output.GetProperty("distinguishedName").GetString());
        Assert.Equal("jdoe", output.GetProperty("samAccountName").GetString());
        Assert.False(output.GetProperty("alreadyExisted").GetBoolean());
        Assert.True(output.GetProperty("mustChangePasswordAtNextLogon").GetBoolean());
        Assert.NotNull(output.GetProperty("temporaryPassword").GetString());
        Assert.Equal("E12345", output.GetProperty("employeeNumber").GetString());

        var temporaryPassword = output.GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(writer.Calls.Single().Password, temporaryPassword);
    }

    [Fact]
    public async Task ExecuteAsync_CreatedOutcome_DoesNotLeakPasswordInLogs()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = spec => new AdWriteOutcome.Created(spec.BuildDistinguishedName()),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        var temporaryPassword = result.Output!.Value.GetProperty("temporaryPassword").GetString()!;
        foreach (var log in result.Logs)
        {
            Assert.DoesNotContain(temporaryPassword, log.Message);
            if (log.Details.HasValue)
            {
                Assert.DoesNotContain(temporaryPassword, log.Details.Value.GetRawText());
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyExistsOutcome_ReturnsSuccessWithAlreadyExistedTrueAndNoPassword()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = _ => new AdWriteOutcome.AlreadyExists("CN=John Doe,OU=Sales,DC=example,DC=local"),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = result.Output!.Value;
        Assert.True(output.GetProperty("alreadyExisted").GetBoolean());
        Assert.False(output.TryGetProperty("temporaryPassword", out _));
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsFailureWithLdapCodePrefix()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = _ => new AdWriteOutcome.PermanentFailure("No such object", LdapResultCode: 32),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("LDAP 32", result.ErrorMessage);
        Assert.Contains("No such object", result.ErrorMessage);
        Assert.Single(result.Logs);
        Assert.Equal("error", result.Logs[0].Level);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsFailureWithLdapCodePrefix()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = _ => new AdWriteOutcome.TransientFailure("Server unavailable", LdapResultCode: 81),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("LDAP 81", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPayloadField_ReturnsFailure()
    {
        var writer = new FakeAdUserWriter();
        var handler = new CreateAdUserLdapsHandler(writer);
        var payload = """
{
  "samAccountName": "jdoe",
  "userPrincipalName": "jdoe@example.local"
}
""";

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Missing payload field", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonObjectPayload_ReturnsFailure()
    {
        var writer = new FakeAdUserWriter();
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext("\"plain-string\""), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("must be a JSON object", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SamAccountNameTooLong_ReturnsFailure()
    {
        var writer = new FakeAdUserWriter();
        var handler = new CreateAdUserLdapsHandler(writer);
        var payload = ValidPayloadJson.Replace("\"jdoe\"", "\"" + new string('x', 21) + "\"");

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("exceeds 20 characters", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SamAccountNameInvalidChars_ReturnsFailure()
    {
        var writer = new FakeAdUserWriter();
        var handler = new CreateAdUserLdapsHandler(writer);
        var payload = ValidPayloadJson.Replace("\"jdoe\"", "\"j doe!\"");

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("disallowed characters", result.ErrorMessage);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultActionKey_IsCreateAdUserLdaps()
    {
        var handler = new CreateAdUserLdapsHandler(new FakeAdUserWriter());
        Assert.Equal("CreateAdUserLdaps", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkerHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 1,
        ActionKey = "CreateAdUserLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
