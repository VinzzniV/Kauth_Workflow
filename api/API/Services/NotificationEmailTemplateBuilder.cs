namespace API;

internal static class NotificationEmailTemplateBuilder
{
    internal sealed record EmailTemplate(string Subject, string HtmlBody);

    public static EmailTemplate BuildWorkflowCreated(string recipientName, string workflowUrl)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);

        return new EmailTemplate(
            "Onboarding-Workflow gestartet",
            $@"
<p>Hallo {encodedName},</p>
<p>Ein neuer Onboarding-Workflow wurde gestartet und wartet auf Ihre Bearbeitung.</p>
<p>Bitte öffnen Sie den Vorgang über den folgenden Link.</p>
{BuildActionButton(encodedUrl, "Workflow öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    public static EmailTemplate BuildTaskReady(
        string recipientName,
        string workflowUrl,
        IReadOnlyList<string> taskTitles)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);
        var effectiveTaskTitles = taskTitles.Count > 0
            ? taskTitles
            : ["Aufgaben im Onboarding"];
        var encodedTaskList = string.Join(
            string.Empty,
            effectiveTaskTitles.Select(taskTitle =>
                $"<li>{System.Net.WebUtility.HtmlEncode(taskTitle)}</li>"));
        var subject = effectiveTaskTitles.Count == 1
            ? $"Neue Aufgabe für {recipientName}"
            : $"Neue Aufgaben für {recipientName}";

        return new EmailTemplate(
            subject,
            $@"
<p>Hallo {encodedName},</p>
<p>für Sie wurden im Onboarding-Prozess Aufgaben vorbereitet.</p>
<p><strong>Aufgaben:</strong></p>
<ul>{encodedTaskList}</ul>
<p>Bitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.</p>
{BuildActionButton(encodedUrl, "Aufgabe öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    public static EmailTemplate BuildWorkflowCompleted(string recipientName, string workflowUrl)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);

        return new EmailTemplate(
            "Onboarding abgeschlossen",
            $@"
<p>Hallo {encodedName},</p>
<p>der von Ihnen gestartete Onboarding-Workflow wurde abgeschlossen.</p>
<p>Sie können den Vorgang bei Bedarf über den folgenden Link öffnen.</p>
{BuildActionButton(encodedUrl, "Workflow öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    private static string BuildActionButton(string encodedUrl, string label)
    {
        return $@"
<p>
    <a href=""{encodedUrl}""
       style=""display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;text-decoration:none;border-radius:8px;font-family:Arial,sans-serif;"">
        {label}
    </a>
</p>";
    }
}
