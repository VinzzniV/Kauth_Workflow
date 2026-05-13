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
    public async Task ExecuteAsync_CreatedOutcome_ReturnsSuccessWithVaultWriteAndCredentialVaultIdPlaceholder()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = spec => new AdWriteOutcome.Created(spec.BuildDistinguishedName()),
        };
        var handler = new CreateAdUserLdapsHandler(writer);
        var context = BuildContext(ValidPayloadJson, workflowNodeInstanceId: 4711);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal("CN=John Doe,OU=Sales,OU=Users,DC=example,DC=local", output.GetProperty("distinguishedName").GetString());
        Assert.Equal("jdoe", output.GetProperty("samAccountName").GetString());
        Assert.False(output.GetProperty("alreadyExisted").GetBoolean());
        Assert.True(output.GetProperty("mustChangePasswordAtNextLogon").GetBoolean());
        Assert.Equal("E12345", output.GetProperty("employeeNumber").GetString());

        // Vault-Pointer ist Platzhalter (null) — der JobStore patcht den Wert in derselben Tx.
        Assert.True(output.TryGetProperty("credentialVaultId", out var vaultIdProperty));
        Assert.Equal(JsonValueKind.Null, vaultIdProperty.ValueKind);
        // Plain-Passwort verlaesst den Output -- nur ueber den VaultWrite-Pfad.
        Assert.False(output.TryGetProperty("temporaryPassword", out _));

        Assert.NotNull(result.VaultWrite);
        Assert.Equal(writer.Calls.Single().Password, result.VaultWrite!.PlainSecret);
        Assert.Equal(4711, result.VaultWrite.WorkflowNodeInstanceId);
        Assert.Equal("ad_initial_password", result.VaultWrite.CredentialType);
    }

    [Fact]
    public async Task ExecuteAsync_CreatedOutcome_DoesNotLeakPasswordInLogsOrOutput()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = spec => new AdWriteOutcome.Created(spec.BuildDistinguishedName()),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        var plainSecret = result.VaultWrite!.PlainSecret;
        Assert.DoesNotContain(plainSecret, result.Output!.Value.GetRawText());
        foreach (var log in result.Logs)
        {
            Assert.DoesNotContain(plainSecret, log.Message);
            if (log.Details.HasValue)
            {
                Assert.DoesNotContain(plainSecret, log.Details.Value.GetRawText());
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyExistsOutcome_NoVaultWriteAndCredentialVaultIdNull()
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
        Assert.True(output.TryGetProperty("credentialVaultId", out var vaultId));
        Assert.Equal(JsonValueKind.Null, vaultId.ValueKind);
        Assert.Null(result.VaultWrite);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentFailure_ReturnsFailureWithLdapCodePrefixAndPermanentKind()
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
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
        Assert.Single(result.Logs);
        Assert.Equal("error", result.Logs[0].Level);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailure_ReturnsFailureWithLdapCodePrefixAndTransientKind()
    {
        var writer = new FakeAdUserWriter
        {
            OutcomeFactory = _ => new AdWriteOutcome.TransientFailure("Server unavailable", LdapResultCode: 81),
        };
        var handler = new CreateAdUserLdapsHandler(writer);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("LDAP 81", result.ErrorMessage);
        Assert.Equal(WorkerFailureKinds.Transient, result.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_PayloadValidationFailure_MarksPermanent()
    {
        var writer = new FakeAdUserWriter();
        var handler = new CreateAdUserLdapsHandler(writer);
        var payload = """
{
  "samAccountName": "jdoe"
}
""";

        var result = await handler.ExecuteAsync(BuildContext(payload), CancellationToken.None);

        Assert.False(result.IsSuccess);
        // Payload-Validierung ist klar permanent — gleiche Eingabe wird durch Retry nicht besser.
        // Linux-Retry-Policy mappt das in FinalFail unabhaengig von is_idempotent.
        Assert.Equal(WorkerFailureKinds.Permanent, result.FailureKind);
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

    private static WorkerHandlerContext BuildContext(string payloadJson, long workflowNodeInstanceId = 1) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = workflowNodeInstanceId,
        ActionKey = "CreateAdUserLdaps",
        Payload = JsonDocument.Parse(payloadJson).RootElement,
        AttemptNumber = 1,
    };
}
