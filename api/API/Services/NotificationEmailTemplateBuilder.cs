namespace API;

internal static class NotificationEmailTemplateBuilder
{
    internal sealed record EmailTemplate(string Subject, string HtmlBody);
    private sealed record ProcessTypeEmailContext(string Key, string Name, string WorkflowLabel, string ProcessLabel);

    public static EmailTemplate BuildWorkflowCreated(
        string recipientName,
        string workflowUrl,
        string processTypeKey,
        string processTypeName)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);
        var processType = ResolveProcessTypeEmailContext(processTypeKey, processTypeName);
        var encodedWorkflowLabel = System.Net.WebUtility.HtmlEncode(processType.WorkflowLabel);

        return new EmailTemplate(
            $"{processType.WorkflowLabel} gestartet",
            $@"
<p>Hallo {encodedName},</p>
<p>Ein neuer {encodedWorkflowLabel} wurde gestartet und wartet auf Ihre Bearbeitung.</p>
<p>Bitte öffnen Sie den Vorgang über den folgenden Link.</p>
{BuildActionButton(encodedUrl, "Workflow öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    public static EmailTemplate BuildTaskReady(
        string recipientName,
        string workflowUrl,
        IReadOnlyList<string> taskTitles,
        string processTypeKey,
        string processTypeName)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);
        var processType = ResolveProcessTypeEmailContext(processTypeKey, processTypeName);
        var encodedProcessLabel = System.Net.WebUtility.HtmlEncode(processType.ProcessLabel);
        var effectiveTaskTitles = taskTitles.Count > 0
            ? taskTitles
            : [$"Aufgaben im {processType.ProcessLabel}"];
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
<p>für Sie wurden im {encodedProcessLabel} Aufgaben vorbereitet.</p>
<p><strong>Aufgaben:</strong></p>
<ul>{encodedTaskList}</ul>
<p>Bitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.</p>
{BuildActionButton(encodedUrl, "Aufgabe öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    public static EmailTemplate BuildWorkflowCompleted(
        string recipientName,
        string workflowUrl,
        string processTypeKey,
        string processTypeName)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);
        var processType = ResolveProcessTypeEmailContext(processTypeKey, processTypeName);
        var encodedWorkflowLabel = System.Net.WebUtility.HtmlEncode(processType.WorkflowLabel);

        return new EmailTemplate(
            $"{processType.Name} abgeschlossen",
            $@"
<p>Hallo {encodedName},</p>
<p>der von Ihnen gestartete {encodedWorkflowLabel} wurde abgeschlossen.</p>
<p>Sie können den Vorgang bei Bedarf über den folgenden Link öffnen.</p>
{BuildActionButton(encodedUrl, "Workflow öffnen")}
<p>Falls der Button nicht funktioniert, verwenden Sie bitte diesen Link:</p>
<p><a href=""{encodedUrl}"">{encodedUrl}</a></p>");
    }

    private static ProcessTypeEmailContext ResolveProcessTypeEmailContext(string processTypeKey, string processTypeName)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(processTypeKey)
            ? "generic"
            : processTypeKey.Trim().ToLowerInvariant();
        var normalizedName = string.IsNullOrWhiteSpace(processTypeName)
            ? "Workflow"
            : processTypeName.Trim();

        return normalizedKey switch
        {
            "onboarding" => new ProcessTypeEmailContext(normalizedKey, normalizedName, "Onboarding-Workflow", "Onboarding-Prozess"),
            "offboarding" => new ProcessTypeEmailContext(normalizedKey, normalizedName, "Offboarding-Workflow", "Offboarding-Prozess"),
            "department_change" => new ProcessTypeEmailContext(normalizedKey, normalizedName, normalizedName, $"Prozess {normalizedName}"),
            "name_change" => new ProcessTypeEmailContext(normalizedKey, normalizedName, normalizedName, $"Prozess {normalizedName}"),
            "position_change" => new ProcessTypeEmailContext(normalizedKey, normalizedName, normalizedName, $"Prozess {normalizedName}"),
            "role_change" => new ProcessTypeEmailContext(normalizedKey, normalizedName, normalizedName, $"Prozess {normalizedName}"),
            _ => new ProcessTypeEmailContext(normalizedKey, normalizedName, $"{normalizedName}-Workflow", $"Prozess {normalizedName}")
        };
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
