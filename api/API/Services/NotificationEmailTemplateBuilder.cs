using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace API;

internal static class NotificationEmailTemplateBuilder
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{\s*([a-z0-9_]+)\s*\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal sealed record EmailTemplate(
        string Subject,
        string TextBody,
        string HtmlBody,
        IReadOnlyDictionary<string, string> PlaceholderValues);

    internal sealed record ProcessTypeEmailContext(string Key, string Name, string WorkflowLabel, string ProcessLabel);

    public static ProcessTypeEmailContext ResolveProcessTypeEmailContext(string processTypeKey, string processTypeName)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(processTypeKey)
            ? "generic"
            : processTypeKey.Trim().ToLowerInvariant();
        var normalizedName = string.IsNullOrWhiteSpace(processTypeName)
            ? "Workflow"
            : processTypeName.Trim();

        return new ProcessTypeEmailContext(
            normalizedKey,
            normalizedName,
            $"{normalizedName}-Workflow",
            $"{normalizedName}-Prozess");
    }

    public static IReadOnlySet<string> ExtractPlaceholderKeys(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return PlaceholderPattern
            .Matches(template)
            .Select(match => match.Groups[1].Value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static EmailTemplate Build(
        string subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string> placeholderValues,
        string recipientName,
        string actionUrl,
        string actionLabel)
    {
        var normalizedPlaceholderValues = placeholderValues
            .ToDictionary(
                entry => entry.Key.Trim().ToLowerInvariant(),
                entry => entry.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var effectiveRecipientName = string.IsNullOrWhiteSpace(recipientName)
            ? normalizedPlaceholderValues.GetValueOrDefault("recipient_name", string.Empty)
            : recipientName.Trim();
        var renderedSubject = ReplacePlaceholders(subjectTemplate, normalizedPlaceholderValues).Trim();
        var renderedBody = ReplacePlaceholders(bodyTemplate, normalizedPlaceholderValues).Trim();
        var normalizedActionUrl = actionUrl.Trim();
        var textBody = BuildTextBody(effectiveRecipientName, renderedBody, normalizedActionUrl, actionLabel);
        var htmlBody = BuildHtmlBody(effectiveRecipientName, renderedBody, normalizedActionUrl, actionLabel);

        return new EmailTemplate(
            renderedSubject,
            textBody,
            htmlBody,
            normalizedPlaceholderValues);
    }

    private static string ReplacePlaceholders(
        string template,
        IReadOnlyDictionary<string, string> placeholderValues)
    {
        return PlaceholderPattern.Replace(template ?? string.Empty, match =>
        {
            var key = match.Groups[1].Value.Trim().ToLowerInvariant();
            return placeholderValues.TryGetValue(key, out var value)
                ? value
                : string.Empty;
        });
    }

    private static string BuildTextBody(
        string recipientName,
        string renderedBody,
        string actionUrl,
        string actionLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Hallo {recipientName},");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(renderedBody))
        {
            builder.AppendLine(renderedBody.Trim());
            builder.AppendLine();
        }

        builder.AppendLine($"{actionLabel}: {actionUrl}");
        builder.AppendLine();
        builder.AppendLine("Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:");
        builder.Append(actionUrl);
        return builder.ToString().Trim();
    }

    private static string BuildHtmlBody(
        string recipientName,
        string renderedBody,
        string actionUrl,
        string actionLabel)
    {
        var encodedRecipient = WebUtility.HtmlEncode(recipientName);
        var encodedActionUrl = WebUtility.HtmlEncode(actionUrl);
        var bodyContent = RenderBodyTextAsHtml(renderedBody);

        return $@"
<p>Hallo {encodedRecipient},</p>
{bodyContent}
{BuildActionButton(encodedActionUrl, actionLabel)}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedActionUrl}"">{encodedActionUrl}</a></p>".Trim();
    }

    private static string RenderBodyTextAsHtml(string renderedBody)
    {
        if (string.IsNullOrWhiteSpace(renderedBody))
        {
            return string.Empty;
        }

        var paragraphs = Regex.Split(renderedBody.Trim(), @"(?:\r?\n){2,}")
            .Select(block => block.Trim())
            .Where(block => block.Length > 0)
            .ToList();
        var segments = new List<string>(paragraphs.Count);

        foreach (var paragraph in paragraphs)
        {
            var lines = paragraph
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            if (lines.Count > 0 && lines.All(line => line.StartsWith("- ", StringComparison.Ordinal)))
            {
                var items = string.Join(
                    string.Empty,
                    lines.Select(line => $"<li>{WebUtility.HtmlEncode(line[2..])}</li>"));
                segments.Add($"<ul>{items}</ul>");
                continue;
            }

            var encodedParagraph = string.Join(
                "<br/>",
                lines.Select(WebUtility.HtmlEncode));
            segments.Add($"<p>{encodedParagraph}</p>");
        }

        return string.Join(Environment.NewLine, segments);
    }

    private static string BuildActionButton(string encodedUrl, string label)
    {
        return $@"
<p>
    <a href=""{encodedUrl}""
       style=""display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;text-decoration:none;border-radius:8px;font-family:Arial,sans-serif;"">
        {WebUtility.HtmlEncode(label)}
    </a>
</p>";
    }
}
