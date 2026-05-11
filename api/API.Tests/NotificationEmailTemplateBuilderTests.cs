using Xunit;

namespace API.Tests;

public sealed class NotificationEmailTemplateBuilderTests
{
    [Fact]
    public void ResolveWorkflowDefinitionEmailContext_UsesConfiguredWorkflowDefinitionName()
    {
        var context = NotificationEmailTemplateBuilder.ResolveWorkflowDefinitionEmailContext(
            "offboarding",
            "Offboarding");

        Assert.Equal("Offboarding", context.Name);
        Assert.Equal("Offboarding-Workflow", context.WorkflowLabel);
        Assert.Equal("Offboarding-Prozess", context.ProcessLabel);
    }

    [Fact]
    public void Build_RendersWorkflowTemplateWithGreetingAndLink()
    {
        var template = NotificationEmailTemplateBuilder.Build(
            "{{workflow_label}} gestartet",
            "Ein neuer {{workflow_label}} wurde gestartet.\n\nBitte öffnen Sie den Vorgang.",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["recipient_name"] = "Max Mustermann",
                ["workflow_label"] = "Offboarding-Workflow",
                ["workflow_url"] = "https://example.test/workflows/123"
            },
            "Max Mustermann",
            "https://example.test/workflows/123",
            "Workflow öffnen");

        Assert.Equal("Offboarding-Workflow gestartet", template.Subject);
        Assert.Contains("Hallo Max Mustermann", template.TextBody);
        Assert.Contains("Workflow öffnen: https://example.test/workflows/123", template.TextBody);
        Assert.Contains("Offboarding-Workflow", template.HtmlBody);
        Assert.Contains("https://example.test/workflows/123", template.HtmlBody);
    }

    [Fact]
    public void Build_RendersBulletListsInHtml()
    {
        var template = NotificationEmailTemplateBuilder.Build(
            "Neue Aufgaben",
            "Für Sie wurden Aufgaben vorbereitet.\n\n- Notebook vorbereiten\n- Konto anlegen",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["recipient_name"] = "Max Mustermann"
            },
            "Max Mustermann",
            "https://example.test/tasks/my",
            "Aufgabe öffnen");

        Assert.Contains("<ul>", template.HtmlBody);
        Assert.Contains("Notebook vorbereiten", template.HtmlBody);
        Assert.Contains("Konto anlegen", template.HtmlBody);
    }

    [Fact]
    public void ExtractPlaceholderKeys_ReturnsNormalizedKeys()
    {
        var keys = NotificationEmailTemplateBuilder.ExtractPlaceholderKeys(
            "{{Recipient_Name}} {{ process_name }} {{workflow_url}}");

        Assert.Contains("recipient_name", keys);
        Assert.Contains("process_name", keys);
        Assert.Contains("workflow_url", keys);
    }
}
