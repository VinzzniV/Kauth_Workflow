using Xunit;

namespace API.Tests;

public sealed class NotificationEmailTemplateBuilderTests
{
    [Fact]
    public void BuildWorkflowCreated_UsesConfiguredProcessTypeName()
    {
        var template = NotificationEmailTemplateBuilder.BuildWorkflowCreated(
            "Max Mustermann",
            "https://example.test/workflows/123",
            "offboarding",
            "Offboarding");

        Assert.Contains("Offboarding-Workflow gestartet", template.Subject);
        Assert.Contains("Offboarding-Workflow", template.HtmlBody);
        Assert.DoesNotContain("Onboarding-Workflow", template.HtmlBody);
    }

    [Fact]
    public void BuildTaskReady_UsesConfiguredProcessTypeName()
    {
        var template = NotificationEmailTemplateBuilder.BuildTaskReady(
            "Max Mustermann",
            "https://example.test/tasks/my",
            [],
            "department_change",
            "Abteilungswechsel");

        Assert.Contains("Abteilungswechsel", template.HtmlBody);
        Assert.DoesNotContain("Onboarding-Prozess", template.HtmlBody);
    }

    [Fact]
    public void BuildWorkflowCompleted_UsesConfiguredProcessTypeName()
    {
        var template = NotificationEmailTemplateBuilder.BuildWorkflowCompleted(
            "Max Mustermann",
            "https://example.test/workflows/123",
            "role_change",
            "Rollenwechsel");

        Assert.Equal("Rollenwechsel abgeschlossen", template.Subject);
        Assert.Contains("Rollenwechsel", template.HtmlBody);
        Assert.DoesNotContain("Onboarding-Workflow", template.HtmlBody);
    }

    [Fact]
    public void BuildRotationUpcomingChange_IncludesDepartmentContextAndTasks()
    {
        var template = NotificationEmailTemplateBuilder.BuildRotationUpcomingChange(
            "Julia Verantwortlich",
            "https://example.test/tasks/my?taskRef=rot%3A44",
            CreateRotationPayload());

        Assert.Contains("Bevorstehender Wechsel", template.Subject);
        Assert.Contains("Aktueller Bereich", template.HtmlBody);
        Assert.Contains("Naechster Bereich", template.HtmlBody);
        Assert.Contains("Notebook vorbereiten", template.HtmlBody);
    }

    [Fact]
    public void BuildRotationOverdue_UsesOverdueSubject()
    {
        var template = NotificationEmailTemplateBuilder.BuildRotationOverdue(
            "Julia Verantwortlich",
            "https://example.test/tasks/my?taskRef=rot%3A44",
            CreateRotationPayload());

        Assert.Contains("Ueberfaellige Rotationsaufgaben", template.Subject);
        Assert.Contains("Anika Sattler", template.HtmlBody);
        Assert.Contains("31.05.2026", template.HtmlBody);
    }

    private static RotationNotificationPayload CreateRotationPayload()
    {
        return new RotationNotificationPayload
        {
            DedupeKey = "demo",
            RecipientName = "Julia Verantwortlich",
            PlanTitle = "Anika - Durchlauf",
            SourceWorkflowUid = Guid.NewGuid(),
            PersonId = 10,
            PersonDisplayName = "Anika Sattler",
            CurrentDepartmentName = "HR",
            NextDepartmentName = "IT",
            ChangeDate = new DateOnly(2026, 6, 1),
            LinkPath = "/tasks/my?taskRef=rot%3A44",
            Tasks =
            [
                new RotationNotificationTaskMailItem
                {
                    GeneratedTaskId = 44,
                    TaskRef = "rot:44",
                    Title = "Notebook vorbereiten",
                    Status = "open",
                    DueDate = new DateOnly(2026, 5, 31),
                    DepartmentName = "IT"
                }
            ]
        };
    }
}
