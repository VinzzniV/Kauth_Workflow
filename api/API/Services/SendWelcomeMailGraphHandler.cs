using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace API;

// Erster echter Linux-side Action-Handler (Etappe 9a Schritt 5 Sub-C, mit Vault-Anbindung
// aus Schritt 6 Sub-C). Sendet eine Willkommens-Mail an einen frisch angelegten User via
// Graph App-only.
//
// Pflicht-Payload (Schritt 6 Sub-C): zusaetzlich zu den Adressfeldern ist `credentialVaultId`
// (UUID) Pflicht. Der Handler loest den Vault zur Run-time im eigenen Prozess auf und packt
// das entschluesselte Passwort als `{{temporary_password}}`-Placeholder ins Template.
// Plain-Passwort lebt nur Mikrosekunden im Handler-Heap; landet NIE in payload_json oder
// output_json.
//
// Vault-Grenze: das Plain-Passwort wird durch keine Mapping-Source exponiert. Der einzige
// Lese-Pfad ist ITemporaryCredentialRepository.ReadAdInitialPasswordByVaultIdAsync (UUID-
// only), in genau einem Handler.
//
// FailureKind-Vertrag:
//   - Payload-Validation (inkl. Missing credentialVaultId, invalide UUID) → Failure permanent
//   - Vault-Lese-Fehler (TTL abgelaufen, unbekannte UUID, falscher Vault-Key) → Failure permanent
//   - Ungueltige E-Mail-Adresse → Failure permanent
//   - GraphMailSendOutcome.PermanentFailure → Failure permanent
//   - GraphMailSendOutcome.TransientFailure → Failure transient
//   - GraphMailSendOutcome.Sent → Success mit Output (messageId nullable, sentAtUtc gesetzt)
internal sealed class SendWelcomeMailGraphHandler : IWorkflowAutomationActionHandler
{
    // Kompakter RFC-5322-naher E-Mail-Check (kein RFC-Compliance-Anspruch — nur Plausibilitaet
    // gegen Tippfehler). Ungueltige Adresse → Permanent, weil ein Retry sie nicht reparieren wird.
    private static readonly Regex EmailRegex = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly INotificationTemplateResolver templateResolver;
    private readonly IGraphMailSender mailSender;
    private readonly ITemporaryCredentialRepository credentialRepository;

    public SendWelcomeMailGraphHandler(
        INotificationTemplateResolver templateResolver,
        IGraphMailSender mailSender,
        ITemporaryCredentialRepository credentialRepository)
    {
        this.templateResolver = templateResolver;
        this.mailSender = mailSender;
        this.credentialRepository = credentialRepository;
    }

    public string ActionKey => "SendWelcomeMailGraph";

    public async Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = context.Payload;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return Failure("Payload must be a JSON object.", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        var toAddress = ReadRequiredString(payload.Value, "toAddress");
        var recipientName = ReadRequiredString(payload.Value, "recipientName");
        var firstName = ReadRequiredString(payload.Value, "firstName");
        var lastName = ReadRequiredString(payload.Value, "lastName");
        var userPrincipalName = ReadRequiredString(payload.Value, "userPrincipalName");
        var credentialVaultIdRaw = ReadRequiredString(payload.Value, "credentialVaultId");

        var firstMissing = FirstMissing(
            ("toAddress", toAddress),
            ("recipientName", recipientName),
            ("firstName", firstName),
            ("lastName", lastName),
            ("userPrincipalName", userPrincipalName));
        if (firstMissing is not null)
        {
            return Failure($"Missing payload field: {firstMissing}", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        if (string.IsNullOrWhiteSpace(credentialVaultIdRaw))
        {
            return Failure(
                "Missing payload field: credentialVaultId — predecessor CreateAdUserLdaps did not produce a Vault entry " +
                "(likely an AlreadyExists case; workflow requires a fresh AD account or an alreadyExisted branch).",
                WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        if (!Guid.TryParse(credentialVaultIdRaw, out var credentialVaultId))
        {
            return Failure(
                $"credentialVaultId must be a UUID (got '{credentialVaultIdRaw}').",
                WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        if (!EmailRegex.IsMatch(toAddress!))
        {
            return Failure($"Invalid 'toAddress' email format: '{toAddress}'.", WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        string temporaryPassword;
        try
        {
            temporaryPassword = await credentialRepository.ReadAdInitialPasswordByVaultIdAsync(credentialVaultId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(
                $"Vault read failed for credentialVaultId '{credentialVaultId}': {ex.Message}",
                WorkflowAutomationRetryPolicy.FailureKindPermanent);
        }

        var template = await templateResolver.ResolveAsync(NotificationTemplateKeys.WelcomeMail, cancellationToken);
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipient_name"] = recipientName!,
            ["first_name"] = firstName!,
            ["last_name"] = lastName!,
            ["user_principal_name"] = userPrincipalName!,
            ["temporary_password"] = temporaryPassword,
        };

        var rendered = NotificationEmailTemplateBuilder.Build(
            template.SubjectTemplate,
            template.BodyTemplate,
            placeholders,
            recipientName: recipientName!,
            actionUrl: string.Empty,
            actionLabel: string.Empty);

        var sendOutcome = await mailSender.SendAsync(
            new GraphMailSendRequest
            {
                ToAddress = toAddress!,
                Subject = rendered.Subject,
                HtmlBody = rendered.HtmlBody,
                TextBody = rendered.TextBody,
            },
            cancellationToken);

        return sendOutcome switch
        {
            GraphMailSendOutcome.Sent sent => BuildSuccessResult(sent, toAddress!),
            GraphMailSendOutcome.PermanentFailure perm => BuildFailureFromOutcome(perm.Reason, perm.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindPermanent),
            GraphMailSendOutcome.TransientFailure trans => BuildFailureFromOutcome(trans.Reason, trans.HttpStatus, WorkflowAutomationRetryPolicy.FailureKindTransient),
            _ => Failure("Unknown GraphMailSendOutcome variant.", WorkflowAutomationRetryPolicy.FailureKindTransient),
        };
    }

    public async Task<AutomationLinuxPlanResult> PlanAsync(
        AutomationPlanContext ctx,
        CancellationToken ct = default)
    {
        var root = ctx.Payload.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return AutomationLinuxPlanResult.Failure("Payload must be a JSON object.");

        var toAddress = ReadRequiredString(root, "toAddress");
        var recipientName = ReadRequiredString(root, "recipientName");
        var firstName = ReadRequiredString(root, "firstName");
        var lastName = ReadRequiredString(root, "lastName");
        var userPrincipalName = ReadRequiredString(root, "userPrincipalName");
        var credentialVaultIdRaw = ReadRawString(root, "credentialVaultId");

        var recipientKnown = !string.IsNullOrWhiteSpace(toAddress) && !PlanSentinels.IsSentinel(toAddress);

        var passwordAvailability = MapPasswordAvailability(credentialVaultIdRaw);

        string? subject = null;
        string? bodyPreview = null;
        if (recipientKnown)
        {
            try
            {
                var template = await templateResolver.ResolveAsync(NotificationTemplateKeys.WelcomeMail, ct);
                var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["recipient_name"] = recipientName ?? string.Empty,
                    ["first_name"] = firstName ?? string.Empty,
                    ["last_name"] = lastName ?? string.Empty,
                    ["user_principal_name"] = userPrincipalName ?? string.Empty,
                    ["temporary_password"] = "[Passwort aus Vault]",
                };
                var rendered = NotificationEmailTemplateBuilder.Build(
                    template.SubjectTemplate,
                    template.BodyTemplate,
                    placeholders,
                    recipientName: recipientName ?? string.Empty,
                    actionUrl: string.Empty,
                    actionLabel: string.Empty);
                subject = rendered.Subject;
                bodyPreview = rendered.TextBody?.Length > 300
                    ? rendered.TextBody[..297] + "..."
                    : rendered.TextBody;
            }
            catch
            {
                // Template-Fehler blockieren die Plan-Vorschau nicht.
            }
        }

        var note = passwordAvailability switch
        {
            PasswordAvailability.NotAvailable =>
                "Kein Vault-Eintrag — Vorgänger CreateAdUserLdaps ist im AlreadyExists-Pfad gelaufen. Mail würde zur Ausführungszeit permanent fehlschlagen.",
            PasswordAvailability.Unknown =>
                "Passwort-Vault-Status unbekannt — AD-Vorgänger noch nicht geplant.",
            PasswordAvailability.WillBeGenerated =>
                "AD-Plan bestätigt Neu-Anlage — Passwort wird zur Ausführungszeit im Vault abgelegt.",
            PasswordAvailability.PresentInVault =>
                "Vault-Eintrag bereits vorhanden (Vorgänger bereits succeeded).",
            _ => string.Empty,
        };

        var plan = new WelcomeMailPlan(
            RecipientKnown: recipientKnown,
            Recipient: recipientKnown ? toAddress : null,
            Subject: subject,
            BodyPreview: bodyPreview,
            PasswordAvailability: passwordAvailability,
            Note: note);

        return AutomationLinuxPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
    }

    private static PasswordAvailability MapPasswordAvailability(string? raw)
    {
        if (raw is null) return PasswordAvailability.NotAvailable;
        if (raw == PlanSentinels.CredentialStatusUnknown) return PasswordAvailability.Unknown;
        if (raw == PlanSentinels.CredentialWillBeGenerated) return PasswordAvailability.WillBeGenerated;
        if (PlanSentinels.IsSentinel(raw)) return PasswordAvailability.Unknown;
        if (Guid.TryParse(raw, out _)) return PasswordAvailability.PresentInVault;
        return PasswordAvailability.NotAvailable;
    }

    private static string? ReadRawString(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    private static WorkflowAutomationHandlerResult BuildSuccessResult(GraphMailSendOutcome.Sent sent, string toAddress)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            messageId = sent.MessageId,
            sentTo = toAddress,
            sentAtUtc = sent.SentAtUtc,
        });
        var log = new WorkflowAutomationLogEntry
        {
            Level = "info",
            Message = $"Welcome mail sent to {toAddress}.",
        };
        return WorkflowAutomationHandlerResult.Success(output, new[] { log });
    }

    private static WorkflowAutomationHandlerResult BuildFailureFromOutcome(string reason, int? httpStatus, string failureKind)
    {
        var prefix = httpStatus.HasValue ? $"Graph HTTP {httpStatus.Value}: " : string.Empty;
        var errorMessage = $"{prefix}{reason}";
        var log = new WorkflowAutomationLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new { httpStatus, failureKind }),
        };
        return WorkflowAutomationHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static WorkflowAutomationHandlerResult Failure(string errorMessage, string failureKind)
    {
        var log = new WorkflowAutomationLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new { failureKind }),
        };
        return WorkflowAutomationHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static string? ReadRequiredString(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        var raw = value.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static string? FirstMissing(params (string Name, string? Value)[] fields)
    {
        foreach (var (name, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(value)) return name;
        }
        return null;
    }
}
