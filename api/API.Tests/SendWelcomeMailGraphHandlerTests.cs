using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class SendWelcomeMailGraphHandlerTests
{
    private const string ValidVaultId = "11111111-2222-3333-4444-555555555555";
    private const string ValidPayloadJson = """
{
  "toAddress": "john.doe@example.local",
  "recipientName": "John Doe",
  "firstName": "John",
  "lastName": "Doe",
  "userPrincipalName": "john.doe@example.local",
  "credentialVaultId": "11111111-2222-3333-4444-555555555555"
}
""";

    [Fact]
    public async Task ExecuteAsync_SendSucceeds_ReturnsSuccessWithOutputAndNoSecretLeak()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.Sent("msg-123", new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc)),
        };
        var repo = new FakeTemporaryCredentialRepository { Plain = "InitialPW-9384" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal("msg-123", output.GetProperty("messageId").GetString());
        Assert.Equal("john.doe@example.local", output.GetProperty("sentTo").GetString());

        // Sicherheitscheck: das Plain-Passwort darf in keinem Output-Feld auftauchen.
        Assert.DoesNotContain("InitialPW-9384", output.GetRawText());
        // Plus: keine credentialVaultId im Output (sie kommt nur als Payload-Input).
        Assert.False(output.TryGetProperty("credentialVaultId", out _));
        Assert.False(output.TryGetProperty("temporaryPassword", out _));

        Assert.Single(sender.Calls);
        Assert.Equal("john.doe@example.local", sender.Calls[0].ToAddress);
        Assert.Equal(new Guid(ValidVaultId), repo.LastVaultId);
    }

    [Fact]
    public async Task ExecuteAsync_TemplateRendersTemporaryPasswordPlaceholder()
    {
        var resolver = new FakeNotificationTemplateResolver
        {
            Template = new StoredNotificationTemplate
            {
                TemplateKey = NotificationTemplateKeys.WelcomeMail,
                DisplayName = "Welcome",
                TriggerDescription = "test",
                SubjectTemplate = "Hi {{first_name}}",
                BodyTemplate = "UPN={{user_principal_name}}; PW={{temporary_password}}",
                IsSystemLocked = false,
                UpdatedAt = null,
            }
        };
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.Sent(null, DateTime.UtcNow),
        };
        var repo = new FakeTemporaryCredentialRepository { Plain = "secret-from-vault-XYZ" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);

        await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.Equal("Hi John", sender.Calls[0].Subject);
        Assert.Contains("UPN=john.doe@example.local", sender.Calls[0].HtmlBody);
        Assert.Contains("PW=secret-from-vault-XYZ", sender.Calls[0].HtmlBody);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentSendFailure_MarksFailurePermanent()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.PermanentFailure("MailboxNotEnabled", 400),
        };
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("Graph HTTP 400", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_TransientSendFailure_MarksFailureTransient()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.TransientFailure("TooManyRequests", 429),
        };
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindTransient, result.FailureKind);
        Assert.Contains("Graph HTTP 429", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingToAddress_PermanentValidationFailure()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);
        var payload = """{"recipientName":"X","firstName":"X","lastName":"X","userPrincipalName":"x@x","credentialVaultId":"11111111-2222-3333-4444-555555555555"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("toAddress", result.ErrorMessage);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCredentialVaultId_PermanentWithHelpdeskHint()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);
        var payload = """{"toAddress":"a@b.c","recipientName":"X","firstName":"X","lastName":"X","userPrincipalName":"x@x"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("credentialVaultId", result.ErrorMessage);
        Assert.Contains("AlreadyExists", result.ErrorMessage);
        Assert.Empty(sender.Calls);
        Assert.Equal(0, repo.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCredentialVaultId_PermanentFailure()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);
        var payload = ValidPayloadJson.Replace(ValidVaultId, "not-a-uuid");

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("must be a UUID", result.ErrorMessage);
        Assert.Empty(sender.Calls);
        Assert.Equal(0, repo.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_VaultRepoThrows_PermanentWithMessage()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var repo = new FakeTemporaryCredentialRepository
        {
            Plain = "x",
            ThrowOnRead = new InvalidOperationException("Vault entry expired."),
        };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("Vault entry expired", result.ErrorMessage);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidEmailFormat_PermanentValidationFailure()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var repo = new FakeTemporaryCredentialRepository { Plain = "x" };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender, repo);
        var payload = ValidPayloadJson.Replace("john.doe@example.local\",\n  \"recipientName", "not-an-email\",\n  \"recipientName");

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("Invalid 'toAddress'", result.ErrorMessage);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerKey_IsSendWelcomeMailGraph()
    {
        var handler = new SendWelcomeMailGraphHandler(
            new FakeNotificationTemplateResolver(),
            new FakeGraphMailSender(),
            new FakeTemporaryCredentialRepository { Plain = "x" });
        Assert.Equal("SendWelcomeMailGraph", handler.ActionKey);
        await Task.CompletedTask;
    }

    private static WorkflowAutomationHandlerContext BuildContext(string payloadJson) => new()
    {
        JobId = 1,
        WorkflowUid = Guid.NewGuid(),
        ActionKey = "SendWelcomeMailGraph",
        AttemptNumber = 1,
        Payload = JsonDocument.Parse(payloadJson).RootElement,
    };

    private sealed class FakeNotificationTemplateResolver : INotificationTemplateResolver
    {
        public StoredNotificationTemplate Template { get; set; } = new()
        {
            TemplateKey = NotificationTemplateKeys.WelcomeMail,
            DisplayName = "Welcome",
            TriggerDescription = "test",
            SubjectTemplate = "Willkommen, {{first_name}}",
            BodyTemplate = "Hallo {{recipient_name}}, UPN: {{user_principal_name}}, PW: {{temporary_password}}",
            IsSystemLocked = false,
            UpdatedAt = null,
        };

        public Task<StoredNotificationTemplate> ResolveAsync(string templateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Template);
    }

    private sealed class FakeGraphMailSender : IGraphMailSender
    {
        public Func<GraphMailSendRequest, GraphMailSendOutcome> OutcomeFactory { get; set; } = _ =>
            new GraphMailSendOutcome.Sent("fake-id", DateTime.UtcNow);

        public List<GraphMailSendRequest> Calls { get; } = new();

        public Task<GraphMailSendOutcome> SendAsync(GraphMailSendRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(OutcomeFactory(request));
        }
    }

    private sealed class FakeTemporaryCredentialRepository : ITemporaryCredentialRepository
    {
        public string Plain { get; set; } = string.Empty;
        public Exception? ThrowOnRead { get; set; }
        public Guid? LastVaultId { get; private set; }
        public int CallCount { get; private set; }

        public Task<string> ReadAdInitialPasswordByVaultIdAsync(Guid credentialVaultId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastVaultId = credentialVaultId;
            if (ThrowOnRead is not null)
            {
                throw ThrowOnRead;
            }
            return Task.FromResult(Plain);
        }
    }
}
