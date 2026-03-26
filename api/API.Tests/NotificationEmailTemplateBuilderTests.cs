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
}
