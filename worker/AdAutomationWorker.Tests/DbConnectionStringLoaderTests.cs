using System.Security.Cryptography;
using System.Text;
using AdAutomationWorker.Core.Configuration;
using Xunit;

namespace AdAutomationWorker.Tests;

public sealed class DbConnectionStringLoaderTests
{
    private const string FakeProgramData = "/fake/programdata/KauthWorker";

    [Fact]
    public void Load_FromEnvironmentVariable_TakesPrecedence()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "db.config.json"), "{\"connectionString\":\"Host=from-json\"}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => "Host=from-env",
            FakeProgramData);

        Assert.Equal("Host=from-env", loader.Load());
    }

    [Fact]
    public void Load_NoEnvNoFile_Throws()
    {
        var fakeFs = new FakeFileSystem();
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains(DbConnectionStringLoader.EnvironmentVariableName, ex.Message);
    }

    [Fact]
    public void Load_JsonFallback_ReadsConnectionString()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "db.config.json"), "{\"connectionString\":\"Host=fallback\"}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        Assert.Equal("Host=fallback", loader.Load());
    }

    [Fact]
    public void Load_JsonMissingField_Throws()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteText(Path.Combine(FakeProgramData, "db.config.json"), "{}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("connectionString", ex.Message);
    }

    [Fact]
    public void Load_DpapiFile_WithDecryptor_ReturnsConnectionString()
    {
        var fakeFs = new FakeFileSystem();
        var cipher = new byte[] { 0x01, 0x02, 0x03 };
        var plain = Encoding.UTF8.GetBytes("{\"connectionString\":\"Host=dpapi-host\"}");
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "db.config.dpapi"), cipher);

        var decryptor = new FakeDecryptor(_ => plain);
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            decryptor);

        Assert.Equal("Host=dpapi-host", loader.Load());
        Assert.Equal(cipher, decryptor.LastCipher);
    }

    [Fact]
    public void Load_DpapiFile_WithoutDecryptor_Throws()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "db.config.dpapi"), new byte[] { 0xAA });
        var loader = DbConnectionStringLoader.CreateForTesting(
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
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "db.config.dpapi"), new byte[] { 0xFF });
        var decryptor = new FakeDecryptor(_ => throw new CryptographicException("simulated failure"));
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            decryptor);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("DPAPI", ex.Message);
        Assert.Contains("install-db-config.ps1", ex.Message);
        Assert.IsType<CryptographicException>(ex.InnerException);
    }

    [Fact]
    public void Load_DpapiFile_TakesPrecedenceOverJsonFallback()
    {
        var fakeFs = new FakeFileSystem();
        var cipher = new byte[] { 0x10 };
        var plain = Encoding.UTF8.GetBytes("{\"connectionString\":\"Host=dpapi-host\"}");
        fakeFs.WriteBytes(Path.Combine(FakeProgramData, "db.config.dpapi"), cipher);
        fakeFs.WriteText(Path.Combine(FakeProgramData, "db.config.json"), "{\"connectionString\":\"Host=plain-host\"}");

        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.ReadText,
            fakeFs.ReadBytes,
            _ => null,
            FakeProgramData,
            new FakeDecryptor(_ => plain));

        Assert.Equal("Host=dpapi-host", loader.Load());
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
        public byte[]? LastCipher { get; private set; }
        public FakeDecryptor(Func<byte[], byte[]> decrypt) => this.decrypt = decrypt;
        public byte[] Decrypt(byte[] cipher)
        {
            LastCipher = cipher;
            return decrypt(cipher);
        }
    }
}
