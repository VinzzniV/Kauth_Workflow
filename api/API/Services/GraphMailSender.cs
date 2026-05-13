using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Kiota.Abstractions;

namespace API;

// Etappe 9a Schritt 5 Sub-C: produktive IGraphMailSender-Implementierung. App-only Auth via
// ClientSecretCredential (gleicher Pfad wie GraphWorkflowEmailNotificationSender — App-only-Apps
// haben KEINEN `Me`-Kontext, daher Users[senderEmail].SendMail).
//
// Error-Mapping fuer den failure_kind-Vertrag:
//   - 400/401/403/404 → PermanentFailure (Konfiguration, Berechtigung, ungueltiger Empfaenger)
//   - 429, 5xx, Timeouts, IO-Exceptions → TransientFailure
//   - Andere → konservativ TransientFailure
internal sealed class GraphMailSender : IGraphMailSender
{
    private readonly INotificationEmailConfigurationService configurationService;

    public GraphMailSender(INotificationEmailConfigurationService configurationService)
    {
        this.configurationService = configurationService;
    }

    public async Task<GraphMailSendOutcome> SendAsync(GraphMailSendRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return new GraphMailSendOutcome.PermanentFailure("Request must not be null.", null);
        }
        if (string.IsNullOrWhiteSpace(request.ToAddress))
        {
            return new GraphMailSendOutcome.PermanentFailure("ToAddress is required.", null);
        }

        NotificationEmailRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Misconfiguration: Tenant/Client/Secret oder Sender-Mailbox fehlt. Aus Sicht des
            // Handlers permanent — Retry wuerde nichts aendern, bis die Konfig korrigiert ist.
            return new GraphMailSendOutcome.PermanentFailure(ex.Message, null);
        }

        if (string.IsNullOrWhiteSpace(configuration.SenderEmail))
        {
            return new GraphMailSendOutcome.PermanentFailure("SenderEmail is not configured.", null);
        }

        var client = CreateGraphClient(configuration);
        var body = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = request.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = request.HtmlBody,
                },
                ToRecipients =
                [
                    new Recipient { EmailAddress = new EmailAddress { Address = request.ToAddress } }
                ]
            },
            SaveToSentItems = configuration.SaveToSentItems,
        };

        try
        {
            await client
                .Users[configuration.SenderEmail!]
                .SendMail
                .PostAsync(body, cancellationToken: cancellationToken);

            // Graph SendMail liefert kein MessageId zurueck. Wir markieren den Sent-Status mit
            // SentAtUtc; MessageId bleibt null. Folge-Slice mit Idempotenz-Check kann via Graph-
            // SentItems den realen Id-Lookup nachziehen.
            return new GraphMailSendOutcome.Sent(MessageId: null, SentAtUtc: DateTime.UtcNow);
        }
        catch (ApiException ex)
        {
            // Microsoft.Kiota.Abstractions.ApiException traegt ResponseStatusCode.
            var http = ex.ResponseStatusCode;
            if (http == 400 || http == 401 || http == 403 || http == 404)
            {
                return new GraphMailSendOutcome.PermanentFailure(ex.Message, http);
            }
            return new GraphMailSendOutcome.TransientFailure(ex.Message, http);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GraphMailSendOutcome.TransientFailure("Operation cancelled.", null);
        }
        catch (TaskCanceledException ex)
        {
            return new GraphMailSendOutcome.TransientFailure("Graph SendMail timed out: " + ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            return new GraphMailSendOutcome.TransientFailure("Graph SendMail HTTP failure: " + ex.Message, null);
        }
        catch (IOException ex)
        {
            return new GraphMailSendOutcome.TransientFailure("Graph SendMail IO failure: " + ex.Message, null);
        }
    }

    private static GraphServiceClient CreateGraphClient(NotificationEmailRuntimeConfiguration configuration)
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var credentialOptions = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var credential = new ClientSecretCredential(
            configuration.TenantId!,
            configuration.ClientId!,
            configuration.ClientSecret!,
            credentialOptions);

        return new GraphServiceClient(credential, scopes);
    }
}
