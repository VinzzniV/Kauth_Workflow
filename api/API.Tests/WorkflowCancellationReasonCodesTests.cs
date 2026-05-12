using Xunit;

namespace API.Tests;

public sealed class WorkflowCancellationReasonCodesTests
{
    [Theory]
    [InlineData("entry_cancelled")]
    [InlineData("entry_postponed")]
    [InlineData("wrong_person")]
    [InlineData("started_by_mistake")]
    [InlineData("ENTRY_CANCELLED")]
    [InlineData("  wrong_person  ")]
    public void Validate_AllowsKnownReasonCodes(string reasonCode)
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = reasonCode,
            ReasonDetail = "Optionaler Hinweis."
        };

        var (normalizedCode, normalizedDetail, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.Null(error);
        Assert.Equal(reasonCode.Trim().ToLowerInvariant(), normalizedCode);
        Assert.Equal("Optionaler Hinweis.", normalizedDetail);
    }

    [Fact]
    public void Validate_RequiresDetail_WhenReasonIsOther()
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = "other",
            ReasonDetail = null
        };

        var (_, _, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.NotNull(error);
        Assert.Contains("other", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsOtherWithDetail()
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = "other",
            ReasonDetail = "Eintritt wegen Krankenstand verschoben — entscheidet noch HR-Lead."
        };

        var (code, detail, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.Null(error);
        Assert.Equal("other", code);
        Assert.Equal(request.ReasonDetail, detail);
    }

    [Theory]
    [InlineData("unknown_code")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsUnknownOrEmptyReasonCode(string reasonCode)
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = reasonCode,
            ReasonDetail = null
        };

        var (_, _, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsNullRequest()
    {
        var (_, _, error) = WorkflowCancellationReasonCodes.Validate(null);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsDetailOverMaxLength()
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = "entry_cancelled",
            ReasonDetail = new string('a', WorkflowCancellationReasonCodes.MaxDetailLength + 1)
        };

        var (_, _, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_NormalizesBlankDetailToNull()
    {
        var request = new WorkflowCancellationRequest
        {
            ReasonCode = "entry_cancelled",
            ReasonDetail = "   "
        };

        var (_, detail, error) = WorkflowCancellationReasonCodes.Validate(request);

        Assert.Null(error);
        Assert.Null(detail);
    }
}
