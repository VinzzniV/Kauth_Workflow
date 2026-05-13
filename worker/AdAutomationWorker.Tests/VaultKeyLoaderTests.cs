using System.Security.Cryptography;
using System.Text;
using AdAutomationWorker.Core.Configuration;
using Xunit;

namespace AdAutomationWorker.Tests;

public sealed class VaultKeyLoaderTests
{
    private const string FakeProgramData = "/fake/programdata/KauthWorker";
    private const string LongKey = "abcdefghijklmnopqrstuvwxyz-1234567890-LONG-ENOUGH";

    [Fact]
    public void Load_FromEnvironmentVariable_TakesPrecedence()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "vault.config.json"), "{\"symmetricKey\":\"" + LongKey + "\"}");
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => "  env-key-with-enough-entropy-XYZ-1234  ",
            FakeProgramData);

        Assert.Equal("env-key-with-enough-entropy-XYZ-1234", loader.Load());
    }

    [Fact]
    public void Load_EnvShorterThanMinimum_Throws()
    {
        var fakeFs = new FakeFileSystem();
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => "shortkey",
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Load_NoEnvNoFile_Throws()
    {
        var fakeFs = new FakeFileSystem();
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains(VaultKeyLoader.EnvironmentVariableName, ex.Message);
    }

    [Fact]
    public void Load_JsonFallback_ReadsSymmetricKey()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "vault.config.json"), "{\"symmetricKey\":\"" + LongKey + "\"}");
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        Assert.Equal(LongKey, loader.Load());
    }

    [Fact]
    public void Load_JsonMissingField_Throws()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "vault.config.json"), "{}");
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("symmetricKey", ex.Message);
    }

    [Fact]
    public void Load_DpapiFile_WithDecryptor_ReturnsKey()
    {
        var fakeFs = new FakeFileSystem();
        var cipher = new byte[] { 0x01, 0x02, 0x03 };
        var plain = Encoding.UTF8.GetBytes("{\"symmetricKey\":\"" + LongKey + "\"}");
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "vault.config.dpapi"), cipher);

        var decryptor = new FakeDecryptor(_ => plain);
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            decryptor);

        Assert.Equal(LongKey, loader.Load());
    }

    [Fact]
    public void Load_DpapiFile_WithoutDecryptor_Throws()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "vault.config.dpapi"), new byte[] { 0xAA });
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            decryptor: null);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("IDbConfigDecryptor", ex.Message);
    }

    [Fact]
    public void Load_DpapiFile_DecryptFailure_HasUserHint()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "vault.config.dpapi"), new byte[] { 0xFF });
        var decryptor = new FakeDecryptor(_ => throw new CryptographicException("simulated failure"));
        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            decryptor);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("DPAPI", ex.Message);
        Assert.Contains("install-vault-key.ps1", ex.Message);
        Assert.IsType<CryptographicException>(ex.InnerException);
    }

    [Fact]
    public void Load_DpapiFile_TakesPrecedenceOverJsonFallback()
    {
        var fakeFs = new FakeFileSystem();
        var cipher = new byte[] { 0x10 };
        var plain = Encoding.UTF8.GetBytes("{\"symmetricKey\":\"" + LongKey + "\"}");
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "vault.config.dpapi"), cipher);
        fakeFs.WriteText(Path.Combine(FakeProgramData, "vault.config.json"), "{\"symmetricKey\":\"OTHER-KEY-ABCDEFGHIJKLMNOPQRSTUV-LONG\"}");

        var loader = VaultKeyLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            new FakeDecryptor(_ => plain));

        Assert.Equal(LongKey, loader.Load());
    }

    private sealed class FakeFileSystem
    {
        private readonly Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> bytes = new(StringComparer.OrdinalIgnoreCase);

        public void WriteText(string path, string content) => texts[path] = content;
        public void WriteBytes(string path, byte[] content) => bytes[path] = content;
        public bool Exists(string path) => texts.ContainsKey(path) || bytes.ContainsKey(path);
        public string ReadText(string path) => texts.TryGetValue(path, out var v) ? v : throw new FileNotFoundException(path);
        public byte[] ReadBytes(string path) => bytes.TryGetValue(path, out var v) ? v : throw new FileNotFoundException(path);
    }

    private sealed class FakeDecryptor : IDbConfigDecryptor
    {
        private readonly Func<byte[], byte[]> decrypt;
        public FakeDecryptor(Func<byte[], byte[]> decrypt) => this.decrypt = decrypt;
        public byte[] Decrypt(byte[] cipher) => decrypt(cipher);
    }
}
