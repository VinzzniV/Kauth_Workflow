using Xunit;

namespace API.Tests;

public sealed class EnvVaultKeyProviderTests
{
    [Fact]
    public void Constructor_NullValue_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new EnvVaultKeyProvider(rawValue: null));
        Assert.Contains("KAUTH_VAULT_KEY", ex.Message);
    }

    [Fact]
    public void Constructor_EmptyValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new EnvVaultKeyProvider(rawValue: string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new EnvVaultKeyProvider(rawValue: "   "));
    }

    [Fact]
    public void Constructor_ShorterThanMinimum_Throws()
    {
        var shortKey = new string('a', 31);
        var ex = Assert.Throws<InvalidOperationException>(() => new EnvVaultKeyProvider(rawValue: shortKey));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Constructor_MinimumLength_Accepted()
    {
        var key = new string('a', 32);
        var provider = new EnvVaultKeyProvider(rawValue: key);
        Assert.Equal(key, provider.GetSymmetricKey());
    }

    [Fact]
    public void Constructor_TrimsSurroundingWhitespace()
    {
        var key = new string('a', 40);
        var provider = new EnvVaultKeyProvider(rawValue: "  " + key + "  ");
        Assert.Equal(key, provider.GetSymmetricKey());
    }
}
