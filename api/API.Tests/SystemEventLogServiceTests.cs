using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class SystemEventLogServiceTests
{
    // ── Redaction ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("token")]
    [InlineData("Token")]
    [InlineData("ACCESS_TOKEN")]
    [InlineData("secret")]
    [InlineData("clientSecret")]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("apiKey")]
    [InlineData("refresh")]
    [InlineData("authorization")]
    [InlineData("accessKey")]
    public void BuildRedactedDetailsJson_RedactsKeyContainingFragment(string keyName)
    {
        var input = new { };
        var node = System.Text.Json.Nodes.JsonObject.Parse($"{{\"{keyName}\": \"sensitive-value\"}}");
        var details = JsonSerializer.Deserialize<JsonElement>(node!.ToJsonString());

        var json = SystemEventLogService.BuildRedactedDetailsJson(details);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        var value = doc.RootElement.GetProperty(keyName).GetString();
        Assert.Equal("[REDACTED]", value);
    }

    [Theory]
    [InlineData("htmlBody")]
    [InlineData("textBody")]
    [InlineData("mailBody")]
    [InlineData("messageBody")]
    [InlineData("HtmlBody")]
    public void BuildRedactedDetailsJson_OmitsBodyKeys(string keyName)
    {
        var node = System.Text.Json.Nodes.JsonObject.Parse($"{{\"{keyName}\": \"<html>some content</html>\"}}");
        var details = JsonSerializer.Deserialize<JsonElement>(node!.ToJsonString());

        var json = SystemEventLogService.BuildRedactedDetailsJson(details);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        var value = doc.RootElement.GetProperty(keyName).GetString();
        Assert.Equal("[OMITTED]", value);
    }

    [Fact]
    public void BuildRedactedDetailsJson_PreservesNonSensitiveKeys()
    {
        var details = new { action = "login", userId = 42, success = true };

        var json = SystemEventLogService.BuildRedactedDetailsJson(details);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("login", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal(42, doc.RootElement.GetProperty("userId").GetInt32());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void BuildRedactedDetailsJson_RedactsNestedSensitiveKeys()
    {
        var details = new { user = new { name = "Alice", password = "secret123" } };

        var json = SystemEventLogService.BuildRedactedDetailsJson(details);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Alice", doc.RootElement.GetProperty("user").GetProperty("name").GetString());
        Assert.Equal("[REDACTED]", doc.RootElement.GetProperty("user").GetProperty("password").GetString());
    }

    [Fact]
    public void BuildRedactedDetailsJson_ReturnsNullForNullInput()
    {
        var result = SystemEventLogService.BuildRedactedDetailsJson(null);
        Assert.Null(result);
    }

    [Fact]
    public void BuildRedactedDetailsJson_HandlesJsonElementInput()
    {
        var element = JsonSerializer.Deserialize<JsonElement>("{\"token\": \"abc\", \"name\": \"test\"}");

        var json = SystemEventLogService.BuildRedactedDetailsJson(element);

        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("[REDACTED]", doc.RootElement.GetProperty("token").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("name").GetString());
    }

    // ── Normalization ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("warning", "warning")]
    [InlineData("WARNING", "warning")]
    [InlineData("error", "error")]
    [InlineData("info", "info")]
    [InlineData(null, "info")]
    [InlineData("", "info")]
    [InlineData("unknown_severity", "info")]
    public void NormalizeSeverity_NormalizesCorrectly(string? input, string expected)
    {
        Assert.Equal(expected, SystemEventLogService.NormalizeSeverity(input));
    }

    [Theory]
    [InlineData("frontend", "frontend")]
    [InlineData("AUTOMATION", "automation")]
    [InlineData("api", "api")]
    [InlineData(null, "system")]
    [InlineData("", "system")]
    [InlineData("unknown_source", "system")]
    public void NormalizeSource_NormalizesCorrectly(string? input, string expected)
    {
        Assert.Equal(expected, SystemEventLogService.NormalizeSource(input));
    }

    [Theory]
    [InlineData(null, "Systemereignis protokolliert.")]
    [InlineData("", "Systemereignis protokolliert.")]
    [InlineData("  ", "Systemereignis protokolliert.")]
    [InlineData("Custom message", "Custom message")]
    [InlineData("  Trimmed  ", "Trimmed")]
    public void NormalizeMessage_NormalizesCorrectly(string? input, string expected)
    {
        Assert.Equal(expected, SystemEventLogService.NormalizeMessage(input));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("hello", "hello")]
    [InlineData("  hello  ", "hello")]
    public void NormalizeOptionalText_NormalizesCorrectly(string? input, string? expected)
    {
        Assert.Equal(expected, SystemEventLogService.NormalizeOptionalText(input));
    }

    // ── Filter / Query Keys ───────────────────────────────────────────────────

    [Theory]
    [InlineData("token", true)]
    [InlineData("ACCESS_TOKEN", true)]
    [InlineData("secret", true)]
    [InlineData("password", true)]
    [InlineData("authorization", true)]
    [InlineData("apiKey", true)]
    [InlineData("refresh", true)]
    [InlineData("clientSecret", true)]
    [InlineData("accessKey", true)]
    [InlineData("action", false)]
    [InlineData("userId", false)]
    [InlineData("message", false)]
    public void ShouldRedact_IdentifiesSensitiveKeys(string key, bool expected)
    {
        Assert.Equal(expected, SystemEventLogService.ShouldRedact(key));
    }

    [Theory]
    [InlineData("htmlBody", true)]
    [InlineData("textBody", true)]
    [InlineData("mailBody", true)]
    [InlineData("messageBody", true)]
    [InlineData("HtmlBody", true)]
    [InlineData("action", false)]
    [InlineData("message", false)]
    public void ShouldRedactBody_IdentifiesBodyKeys(string key, bool expected)
    {
        Assert.Equal(expected, SystemEventLogService.ShouldRedactBody(key));
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    [Fact]
    public void DecodeCursor_ReturnsNullForNullCursor()
    {
        var result = SystemEventLogService.DecodeCursor(null);
        Assert.Null(result);
    }

    [Fact]
    public void DecodeCursor_ReturnsNullForEmptyCursor()
    {
        var result = SystemEventLogService.DecodeCursor("");
        Assert.Null(result);
    }

    [Fact]
    public void DecodeCursor_DecodesCursorEncodedByCursorPageQuery()
    {
        var createdAt = DateTime.UtcNow.AddSeconds(-1);
        var id = 42L;
        var encoded = CursorPageQuery.EncodeCursor(createdAt, id);

        var decoded = SystemEventLogService.DecodeCursor(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(id, decoded.Value.Id);
        Assert.Equal(createdAt.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond,
            decoded.Value.CreatedAt.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void DecodeCursor_ReturnsNullForInvalidBase64()
    {
        var result = SystemEventLogService.DecodeCursor("not-valid-base64!!!");
        Assert.Null(result);
    }
}
