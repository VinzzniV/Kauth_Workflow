using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class SendWelcomeMailGraphHandlerTests
{
    private const string ValidPayloadJson = """
{
  "toAddress": "john.doe@example.local",
  "recipientName": "John Doe",
  "firstName": "John",
  "lastName": "Doe",
  "userPrincipalName": "john.doe@example.local"
}
""";

    [Fact]
    public async Task ExecuteAsync_SendSucceeds_ReturnsSuccessWithOutput()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.Sent("msg-123", new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc)),
        };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);

        var result = await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = result.Output!.Value;
        Assert.Equal("msg-123", output.GetProperty("messageId").GetString());
        Assert.Equal("john.doe@example.local", output.GetProperty("sentTo").GetString());

        Assert.Single(sender.Calls);
        var sentRequest = sender.Calls[0];
        Assert.Equal("john.doe@example.local", sentRequest.ToAddress);
        Assert.Contains("John", sentRequest.Subject);
    }

    [Fact]
    public async Task ExecuteAsync_PermanentSendFailure_MarksFailurePermanent()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.PermanentFailure("MailboxNotEnabled", 400),
        };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);

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
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);

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
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);
        var payload = """{"recipientName":"X","firstName":"X","lastName":"X","userPrincipalName":"x@x"}""";

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("toAddress", result.ErrorMessage);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidEmailFormat_PermanentValidationFailure()
    {
        var resolver = new FakeNotificationTemplateResolver();
        var sender = new FakeGraphMailSender();
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);
        var payload = ValidPayloadJson.Replace("john.doe@example.local\",\n  \"recipientName", "not-an-email\",\n  \"recipientName");

        var result = await handler.ExecuteAsync(BuildContext(payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowAutomationRetryPolicy.FailureKindPermanent, result.FailureKind);
        Assert.Contains("Invalid 'toAddress'", result.ErrorMessage);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_TemplatePlaceholdersGetReplaced()
    {
        var resolver = new FakeNotificationTemplateResolver
        {
            Template = new StoredNotificationTemplate
            {
                TemplateKey = NotificationTemplateKeys.WelcomeMail,
                DisplayName = "Welcome",
                TriggerDescription = "test",
                SubjectTemplate = "Hi {{first_name}}",
                BodyTemplate = "UPN: {{user_principal_name}}",
                IsSystemLocked = false,
                UpdatedAt = null,
            }
        };
        var sender = new FakeGraphMailSender
        {
            OutcomeFactory = _ => new GraphMailSendOutcome.Sent(null, DateTime.UtcNow),
        };
        var handler = new SendWelcomeMailGraphHandler(resolver, sender);

        await handler.ExecuteAsync(BuildContext(ValidPayloadJson));

        Assert.Equal("Hi John", sender.Calls[0].Subject);
        Assert.Contains("UPN: john.doe@example.local", sender.Calls[0].HtmlBody);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerKey_IsSendWelcomeMailGraph()
    {
        var handler = new SendWelcomeMailGraphHandler(new FakeNotificationTemplateResolver(), new FakeGraphMailSender());
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
            BodyTemplate = "Hallo {{recipient_name}}, UPN: {{user_principal_name}}",
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
}
