using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace API;

internal sealed class GraphWorkflowEmailNotificationSender : IWorkflowEmailNotificationSender, INotificationEmailTestSender
{
    private readonly INotificationEmailConfigurationService configurationService;
    private readonly INotificationTemplateService notificationTemplateService;
    private readonly ISystemEventLogService systemEventLogService;
    private readonly ILogger<GraphWorkflowEmailNotificationSender> logger;

    public GraphWorkflowEmailNotificationSender(
        INotificationEmailConfigurationService configurationService,
        INotificationTemplateService notificationTemplateService,
        ISystemEventLogService systemEventLogService,
        ILogger<GraphWorkflowEmailNotificationSender> logger)
    {
        this.configurationService = configurationService;
        this.notificationTemplateService = notificationTemplateService;
        this.systemEventLogService = systemEventLogService;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        var validation = NotificationEmailConfigurationValidator.ValidateForSending(configuration);
        if (string.Equals(validation.Status, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDispatchResults(targets, "disabled", success: false, attempted: false, errorMessage: null);
        }

        if (!validation.CanSend)
        {
            logger.LogWarning("Notification email sending is enabled but not configured correctly: {Error}", validation.Message);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "warning",
                Source = "mail",
                Category = "configuration",
                EventKey = "notification_dispatch_blocked",
                Message = $"Workflow notification dispatch blocked by incomplete mail configuration: {validation.Message}",
                Details = new { validation.Message, targetCount = targets.Count }
            }, cancellationToken);
            return CreateDispatchResults(targets, "failed", success: false, attempted: false, errorMessage: validation.Message);
        }

        var client = CreateGraphClient(configuration);
        var disabledByType = targets
            .Where(target => !IsNotificationTypeEnabled(configuration, target.NotificationType))
            .ToList();
        var disabledNotificationIds = disabledByType
            .Select(target => target.NotificationId)
            .ToHashSet();
        var enabledTargets = targets
            .Where(target => !disabledNotificationIds.Contains(target.NotificationId))
            .ToList();
        var results = new List<NotificationDispatchResult>(targets.Count);

        results.AddRange(CreateDispatchResults(disabledByType, "disabled", success: false, attempted: false, errorMessage: null));

        foreach (var batch in GroupTargetsForDispatch(enabledTargets))
        {
            var isSandbox = !string.IsNullOrWhiteSpace(configuration.SandboxRedirectEmail);
            var effectiveRecipientEmail = isSandbox
                ? configuration.SandboxRedirectEmail!
                : batch.PrimaryTarget.TargetEmail;
            var effectiveRecipientName = isSandbox
                ? $"[SANDBOX] {batch.PrimaryTarget.TargetName}"
                : batch.PrimaryTarget.TargetName;
            var subjectPrefix = isSandbox ? $"[TEST -> {batch.PrimaryTarget.TargetEmail}] " : string.Empty;

            try
            {
                var workflowUrl = BuildWorkflowAccessUrl(
                    configuration.FrontendBaseUrl,
                    workflowUid,
                    batch);
                await client
                    .Users[configuration.SenderEmail!]
                    .SendMail
                    .PostAsync(
                        await BuildNotificationMail(
                            batch,
                            workflowUrl,
                            configuration.SaveToSentItems,
                            effectiveRecipientEmail,
                            effectiveRecipientName,
                            subjectPrefix,
                            cancellationToken),
                        cancellationToken: cancellationToken);

                results.AddRange(batch.Targets.Select(target => new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "sent",
                    Success = true,
                    Attempted = true,
                    ErrorMessage = null
                }));

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "mail",
                    Category = "dispatch",
                    EventKey = isSandbox ? "workflow_notifications_redirected" : "workflow_notifications_sent",
                    Message = isSandbox
                        ? $"Workflow notifications redirected from {batch.PrimaryTarget.TargetEmail} to {effectiveRecipientEmail}."
                        : $"Workflow notifications sent to {batch.PrimaryTarget.TargetEmail}.",
                    WorkflowUid = workflowUid,
                    Details = new
                    {
                        notificationType = batch.NotificationType,
                        recipient = effectiveRecipientEmail,
                        originalRecipient = batch.PrimaryTarget.TargetEmail,
                        redirected = isSandbox,
                        targetCount = batch.Targets.Count,
                        notificationIds = batch.Targets.Select(target => target.NotificationId).ToArray()
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Notification email dispatch failed for workflow {WorkflowUid} and notifications {NotificationIds}.",
                    workflowUid,
                    string.Join(", ", batch.Targets.Select(target => target.NotificationId)));

                results.AddRange(batch.Targets.Select(target => new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "failed",
                    Success = false,
                        Attempted = true,
                        ErrorMessage = ex.Message
                    }));

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "error",
                    Source = "mail",
                    Category = "dispatch",
                    EventKey = isSandbox ? "workflow_notifications_redirect_failed" : "workflow_notifications_failed",
                    Message = isSandbox
                        ? $"Workflow notification redirect failed for {batch.PrimaryTarget.TargetEmail} via {effectiveRecipientEmail}: {ex.Message}"
                        : $"Workflow notification dispatch failed for {batch.PrimaryTarget.TargetEmail}: {ex.Message}",
                    WorkflowUid = workflowUid,
                    Details = new
                    {
                        notificationType = batch.NotificationType,
                        recipient = effectiveRecipientEmail,
                        originalRecipient = batch.PrimaryTarget.TargetEmail,
                        redirected = isSandbox,
                        targetCount = batch.Targets.Count,
                        notificationIds = batch.Targets.Select(target => target.NotificationId).ToArray(),
                        error = ex.Message
                    }
                }, cancellationToken);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> SendRotationNotificationsAsync(
        IReadOnlyList<RotationNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        var validation = NotificationEmailConfigurationValidator.ValidateForSending(configuration);
        if (string.Equals(validation.Status, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRotationDispatchResults(targets, "disabled", success: false, attempted: false, errorMessage: null);
        }

        if (!validation.CanSend)
        {
            logger.LogWarning("Notification email sending is enabled but not configured correctly: {Error}", validation.Message);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "warning",
                Source = "mail",
                Category = "configuration",
                EventKey = "rotation_notification_dispatch_blocked",
                Message = $"Rotation notification dispatch blocked by incomplete mail configuration: {validation.Message}",
                Details = new { validation.Message, targetCount = targets.Count }
            }, cancellationToken);
            return CreateRotationDispatchResults(targets, "failed", success: false, attempted: false, errorMessage: validation.Message);
        }

        var client = CreateGraphClient(configuration);
        var disabledByType = targets
            .Where(target => !IsNotificationTypeEnabled(configuration, target.NotificationType))
            .ToList();
        var disabledNotificationIds = disabledByType
            .Select(target => target.NotificationId)
            .ToHashSet();
        var enabledTargets = targets
            .Where(target => !disabledNotificationIds.Contains(target.NotificationId))
            .ToList();
        var results = new List<NotificationDispatchResult>(targets.Count);

        results.AddRange(CreateRotationDispatchResults(disabledByType, "disabled", success: false, attempted: false, errorMessage: null));

        foreach (var target in enabledTargets)
        {
            var isSandbox = !string.IsNullOrWhiteSpace(configuration.SandboxRedirectEmail);
            var effectiveRecipientEmail = isSandbox
                ? configuration.SandboxRedirectEmail!
                : target.TargetEmail;
            var effectiveRecipientName = isSandbox
                ? $"[SANDBOX] {target.TargetName}"
                : target.TargetName;
            var subjectPrefix = isSandbox ? $"[TEST -> {target.TargetEmail}] " : string.Empty;

            try
            {
                var appUrl = BuildRotationAccessUrl(configuration.FrontendBaseUrl, target.Payload.LinkPath);
                await client
                    .Users[configuration.SenderEmail!]
                    .SendMail
                    .PostAsync(
                        await BuildRotationNotificationMail(
                            target,
                            appUrl,
                            configuration.SaveToSentItems,
                            effectiveRecipientEmail,
                            effectiveRecipientName,
                            subjectPrefix,
                            cancellationToken),
                        cancellationToken: cancellationToken);

                results.Add(new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "sent",
                    Success = true,
                    Attempted = true
                });

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "mail",
                    Category = "dispatch",
                    EventKey = isSandbox ? "rotation_notification_redirected" : "rotation_notification_sent",
                    Message = isSandbox
                        ? $"Rotation notification redirected from {target.TargetEmail} to {effectiveRecipientEmail}."
                        : $"Rotation notification sent to {target.TargetEmail}.",
                    RotationPlanId = target.RotationPlanId,
                    TaskRef = target.Payload.LinkPath,
                    Details = new
                    {
                        notificationId = target.NotificationId,
                        notificationType = target.NotificationType,
                        recipient = effectiveRecipientEmail,
                        originalRecipient = target.TargetEmail,
                        redirected = isSandbox
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Rotation notification dispatch failed for plan {RotationPlanId} and notification {NotificationId}.",
                    target.RotationPlanId,
                    target.NotificationId);

                results.Add(new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "failed",
                    Success = false,
                    Attempted = true,
                    ErrorMessage = ex.Message
                });

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "error",
                    Source = "mail",
                    Category = "dispatch",
                    EventKey = isSandbox ? "rotation_notification_redirect_failed" : "rotation_notification_failed",
                    Message = isSandbox
                        ? $"Rotation notification redirect failed for {target.TargetEmail} via {effectiveRecipientEmail}: {ex.Message}"
                        : $"Rotation notification dispatch failed for {target.TargetEmail}: {ex.Message}",
                    RotationPlanId = target.RotationPlanId,
                    Details = new
                    {
                        notificationId = target.NotificationId,
                        notificationType = target.NotificationType,
                        recipient = effectiveRecipientEmail,
                        originalRecipient = target.TargetEmail,
                        redirected = isSandbox,
                        error = ex.Message
                    }
                }, cancellationToken);
            }
        }

        return results;
    }

    public async Task<NotificationEmailTestSendResult> SendTestEmailAsync(
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetRuntimeConfiguration(cancellationToken);
        var validation = NotificationEmailConfigurationValidator.ValidateForSending(configuration);
        if (string.Equals(validation.Status, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return new NotificationEmailTestSendResult
            {
                Success = false,
                Status = "disabled",
                Message = validation.Message ?? "Mailversand ist deaktiviert.",
                RecipientEmail = recipientEmail,
                ErrorMessage = null
            };
        }

        if (!validation.CanSend)
        {
            logger.LogWarning("Notification email test failed because configuration is incomplete: {Error}", validation.Message);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "warning",
                Source = "mail",
                Category = "configuration",
                EventKey = "notification_test_blocked",
                Message = $"Notification email test blocked: {validation.Message}",
                Details = new { recipientEmail }
            }, cancellationToken);
            return new NotificationEmailTestSendResult
            {
                Success = false,
                Status = "failed",
                Message = validation.Message ?? "Mail-Konfiguration ist unvollständig.",
                RecipientEmail = recipientEmail,
                ErrorMessage = validation.Message
            };
        }

        if (!NotificationEmailConfigurationValidator.IsValidEmail(recipientEmail))
        {
            return new NotificationEmailTestSendResult
            {
                Success = false,
                Status = "failed",
                Message = "Testempfänger-Mailadresse ist ungültig.",
                RecipientEmail = recipientEmail,
                ErrorMessage = "Testempfänger-Mailadresse ist ungültig."
            };
        }

        try
        {
            var client = CreateGraphClient(configuration);
            await client
                .Users[configuration.SenderEmail!]
                .SendMail
                .PostAsync(
                    BuildTestMail(recipientEmail, configuration.FrontendBaseUrl, configuration.SaveToSentItems),
                    cancellationToken: cancellationToken);

            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "mail",
                Category = "test",
                EventKey = "notification_test_sent",
                Message = $"Notification test email sent to {recipientEmail}.",
                Details = new { recipientEmail }
            }, cancellationToken);

            return new NotificationEmailTestSendResult
            {
                Success = true,
                Status = "succeeded",
                Message = "Testmail wurde erfolgreich versendet.",
                RecipientEmail = recipientEmail,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification email test dispatch failed for recipient {RecipientEmail}.", recipientEmail);
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "mail",
                Category = "test",
                EventKey = "notification_test_failed",
                Message = $"Notification email test failed for {recipientEmail}: {ex.Message}",
                Details = new { recipientEmail, error = ex.Message }
            }, cancellationToken);
            return new NotificationEmailTestSendResult
            {
                Success = false,
                Status = "failed",
                Message = ex.Message,
                RecipientEmail = recipientEmail,
                ErrorMessage = ex.Message
            };
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

    private static IReadOnlyList<NotificationDispatchResult> CreateDispatchResults(
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        string status,
        bool success,
        bool attempted,
        string? errorMessage)
    {
        return targets
            .Select(target => new NotificationDispatchResult
            {
                NotificationId = target.NotificationId,
                Status = status,
                Success = success,
                Attempted = attempted,
                ErrorMessage = errorMessage
            })
            .ToList();
    }

    private static IReadOnlyList<NotificationDispatchResult> CreateRotationDispatchResults(
        IReadOnlyList<RotationNotificationDispatchTarget> targets,
        string status,
        bool success,
        bool attempted,
        string? errorMessage)
    {
        return targets
            .Select(target => new NotificationDispatchResult
            {
                NotificationId = target.NotificationId,
                Status = status,
                Success = success,
                Attempted = attempted,
                ErrorMessage = errorMessage
            })
            .ToList();
    }

    private async Task<SendMailPostRequestBody> BuildNotificationMail(
        NotificationDispatchBatch batch,
        string workflowUrl,
        bool saveToSentItems,
        string recipientEmail,
        string recipientName,
        string subjectPrefix,
        CancellationToken cancellationToken)
    {
        var template = await notificationTemplateService.RenderWorkflowNotification(
            new WorkflowNotificationRenderContext
            {
                TemplateKey = batch.NotificationType,
                RecipientName = recipientName,
                WorkflowUrl = workflowUrl,
                ProcessTypeKey = batch.PrimaryTarget.ProcessTypeKey,
                ProcessTypeName = batch.PrimaryTarget.ProcessTypeName,
                TaskTitles = batch.TaskTitles
            },
            cancellationToken);

        return new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = $"{subjectPrefix}{template.Subject}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = template.HtmlBody
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = recipientEmail
                        }
                    }
                ]
            },
            SaveToSentItems = saveToSentItems
        };
    }

    private async Task<SendMailPostRequestBody> BuildRotationNotificationMail(
        RotationNotificationDispatchTarget target,
        string appUrl,
        bool saveToSentItems,
        string recipientEmail,
        string recipientName,
        string subjectPrefix,
        CancellationToken cancellationToken)
    {
        var template = await notificationTemplateService.RenderRotationNotification(
            new RotationNotificationRenderContext
            {
                TemplateKey = target.NotificationType,
                RecipientName = recipientName,
                AppUrl = appUrl,
                Payload = target.Payload
            },
            cancellationToken);

        return new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = $"{subjectPrefix}{template.Subject}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = template.HtmlBody
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = recipientEmail
                        }
                    }
                ]
            },
            SaveToSentItems = saveToSentItems
        };
    }

    private static SendMailPostRequestBody BuildTestMail(
        string recipientEmail,
        string frontendBaseUrl,
        bool saveToSentItems)
    {
        var encodedRecipient = System.Net.WebUtility.HtmlEncode(recipientEmail);
        var encodedBaseUrl = System.Net.WebUtility.HtmlEncode(frontendBaseUrl.TrimEnd('/'));

        return new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = "Employee Lifecycle Testmail",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = $@"
<p>Dies ist eine Testmail aus dem Admin-Bereich der Employee-Lifecycle-Anwendung.</p>
<p>Empfänger: {encodedRecipient}</p>
<p>Hinterlegte Frontend-Basis-URL: {encodedBaseUrl}</p>"
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = recipientEmail
                        }
                    }
                ]
            },
            SaveToSentItems = saveToSentItems
        };
    }

    private string BuildWorkflowAccessUrl(
        string frontendBaseUrl,
        Guid workflowUid,
        NotificationDispatchBatch batch)
    {
        var normalizedBaseUrl = frontendBaseUrl.TrimEnd('/');
        var target = batch.PrimaryTarget;
        var redirectPath = string.Equals(batch.NotificationType, "task_ready", StringComparison.OrdinalIgnoreCase)
            ? "/tasks/my"
            : target.PreferredPath switch
        {
            "/supervisor" => "/supervisor",
            "/tasks/my" => "/tasks/my",
            "/workflows" => $"/workflows/{workflowUid}",
            _ => $"/workflows/{workflowUid}"
        };

        return $"{normalizedBaseUrl}{redirectPath}";
    }

    private static string BuildRotationAccessUrl(string frontendBaseUrl, string linkPath)
    {
        var normalizedBaseUrl = frontendBaseUrl.TrimEnd('/');
        var normalizedPath = string.IsNullOrWhiteSpace(linkPath)
            ? "/tasks/my"
            : linkPath.Trim();

        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        return $"{normalizedBaseUrl}{normalizedPath}";
    }

    private static IReadOnlyList<NotificationDispatchBatch> GroupTargetsForDispatch(
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets)
    {
        return targets
            .GroupBy(target =>
                string.Join(
                    "\u001f",
                    target.NotificationType.Trim(),
                    target.TargetEmail.Trim(),
                    target.RecipientUserId?.ToString() ?? string.Empty,
                    target.PreferredPath ?? string.Empty),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedTargets = group
                    .OrderBy(target => target.WorkflowTaskId ?? long.MaxValue)
                    .ThenBy(target => target.NotificationId)
                    .ToList();

                return new NotificationDispatchBatch
                {
                    NotificationType = orderedTargets[0].NotificationType,
                    PrimaryTarget = orderedTargets[0],
                    Targets = orderedTargets,
                    TaskTitles = orderedTargets
                        .Select(target => target.TaskTitle)
                        .Where(taskTitle => !string.IsNullOrWhiteSpace(taskTitle))
                        .Select(taskTitle => taskTitle!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(taskTitle => taskTitle, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                };
            })
            .ToList();
    }

    private static bool IsNotificationTypeEnabled(
        NotificationEmailRuntimeConfiguration configuration,
        string notificationType)
    {
        return notificationType.Trim().ToLowerInvariant() switch
        {
            "workflow_created" => configuration.NotifyOnWorkflowCreated,
            "task_ready" => configuration.NotifyOnTaskReady,
            "workflow_completed" => configuration.NotifyOnWorkflowCompleted,
            _ => true
        };
    }

    private sealed class NotificationDispatchBatch
    {
        public required string NotificationType { get; init; }
        public required WorkflowNotificationDispatchTarget PrimaryTarget { get; init; }
        public required List<WorkflowNotificationDispatchTarget> Targets { get; init; }
        public required List<string> TaskTitles { get; init; }
    }
}
