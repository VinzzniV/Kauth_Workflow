using AdAutomationWorker.Core.Ad;
using Xunit;

namespace AdAutomationWorker.Tests.Ad;

public sealed class AdPasswordGeneratorTests
{
    [Fact]
    public void Generate_Default_Returns16Chars()
    {
        var password = AdPasswordGenerator.Generate();
        Assert.Equal(16, password.Length);
    }

    [Fact]
    public void Generate_ExplicitLength_Returns24Chars()
    {
        var password = AdPasswordGenerator.Generate(24);
        Assert.Equal(24, password.Length);
    }

    [Fact]
    public void Generate_TooShort_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AdPasswordGenerator.Generate(7));
    }

    [Fact]
    public void Generate_PropertyTest_AllPasswordsHaveAllFourComplexityClasses()
    {
        for (var i = 0; i < 1000; i++)
        {
            var password = AdPasswordGenerator.Generate();

            Assert.Contains(password, c => c is >= 'A' and <= 'Z');
            Assert.Contains(password, c => c is >= 'a' and <= 'z');
            Assert.Contains(password, c => c is >= '0' and <= '9');
            Assert.Contains(password, "!@#$%^&*-_=+".Contains);
        }
    }

    [Fact]
    public void Generate_PropertyTest_PasswordsAreNotPredictable()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            seen.Add(AdPasswordGenerator.Generate());
        }
        Assert.Equal(100, seen.Count);
    }
}
