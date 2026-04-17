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

    public static EmailTemplate BuildRotationUpcomingChange(
        string recipientName,
        string workflowUrl,
        RotationNotificationPayload payload)
    {
        var subjectDepartment = string.IsNullOrWhiteSpace(payload.NextDepartmentName)
            ? payload.PersonDisplayName
            : $"{payload.PersonDisplayName} -> {payload.NextDepartmentName}";

        return BuildRotationTemplate(
            recipientName,
            workflowUrl,
            payload,
            $"Bevorstehender Wechsel: {subjectDepartment}",
            "ein Bereichswechsel im Rotationsdurchlauf steht bevor.");
    }

    public static EmailTemplate BuildRotationReminder(
        string recipientName,
        string workflowUrl,
        RotationNotificationPayload payload)
    {
        return BuildRotationTemplate(
            recipientName,
            workflowUrl,
            payload,
            $"Faellige Rotationsaufgaben fuer {payload.PersonDisplayName}",
            "offene Rotationsaufgaben haben heute ihren Faelligkeitstermin erreicht.");
    }

    public static EmailTemplate BuildRotationOverdue(
        string recipientName,
        string workflowUrl,
        RotationNotificationPayload payload)
    {
        return BuildRotationTemplate(
            recipientName,
            workflowUrl,
            payload,
            $"Ueberfaellige Rotationsaufgaben fuer {payload.PersonDisplayName}",
            "es gibt offene Rotationsaufgaben mit ueberschrittenem Faelligkeitstermin.");
    }

    private static ProcessTypeEmailContext ResolveProcessTypeEmailContext(string processTypeKey, string processTypeName)
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

    private static EmailTemplate BuildRotationTemplate(
        string recipientName,
        string workflowUrl,
        RotationNotificationPayload payload,
        string subject,
        string introText)
    {
        var encodedName = System.Net.WebUtility.HtmlEncode(recipientName);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(workflowUrl);
        var encodedPerson = System.Net.WebUtility.HtmlEncode(payload.PersonDisplayName);
        var encodedCurrentDepartment = System.Net.WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(payload.CurrentDepartmentName) ? "n. a." : payload.CurrentDepartmentName);
        var encodedNextDepartment = System.Net.WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(payload.NextDepartmentName) ? "n. a." : payload.NextDepartmentName);
        var encodedPlanTitle = System.Net.WebUtility.HtmlEncode(payload.PlanTitle);
        var encodedChangeDate = payload.ChangeDate.HasValue
            ? System.Net.WebUtility.HtmlEncode(payload.ChangeDate.Value.ToString("dd.MM.yyyy"))
            : "n. a.";

        var taskList = payload.Tasks.Count == 0
            ? "<li>Keine offenen Aufgaben im Payload.</li>"
            : string.Join(
                string.Empty,
                payload.Tasks.Select(task =>
                {
                    var dueLabel = task.DueDate.HasValue
                        ? $" (faellig: {task.DueDate.Value:dd.MM.yyyy})"
                        : string.Empty;
                    return $"<li>{System.Net.WebUtility.HtmlEncode(task.Title + dueLabel)}</li>";
                }));

        return new EmailTemplate(
            subject,
            $@"
<p>Hallo {encodedName},</p>
<p>fuer den Durchlaufplan <strong>{encodedPlanTitle}</strong> von <strong>{encodedPerson}</strong> gilt: {System.Net.WebUtility.HtmlEncode(introText)}</p>
<ul>
    <li><strong>Aktueller Bereich:</strong> {encodedCurrentDepartment}</li>
    <li><strong>Naechster Bereich:</strong> {encodedNextDepartment}</li>
    <li><strong>Wechseltermin:</strong> {encodedChangeDate}</li>
</ul>
<p><strong>Offene relevante Aufgaben:</strong></p>
<ul>{taskList}</ul>
<p>Bitte oeffnen Sie die Anwendung ueber den folgenden Link.</p>
{BuildActionButton(encodedUrl, "Aufgabe oeffnen")}
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
