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
        fakeFs.Write(Path.Combine(FakeProgramData, "db.config.json"), "{\"connectionString\":\"Host=from-json\"}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.Read,
            _ => "Host=from-env",
            FakeProgramData);

        var actual = loader.Load();

        Assert.Equal("Host=from-env", actual);
    }

    [Fact]
    public void Load_NoEnvNoFile_Throws()
    {
        var fakeFs = new FakeFileSystem();
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.Read,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains(DbConnectionStringLoader.EnvironmentVariableName, ex.Message);
    }

    [Fact]
    public void Load_JsonFallback_ReadsConnectionString()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.Write(Path.Combine(FakeProgramData, "db.config.json"), "{\"connectionString\":\"Host=fallback\"}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.Read,
            _ => null,
            FakeProgramData);

        Assert.Equal("Host=fallback", loader.Load());
    }

    [Fact]
    public void Load_DpapiFileExists_ThrowsWithHandoffToStep3()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.Write(Path.Combine(FakeProgramData, "db.config.dpapi"), "encrypted-bytes");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.Read,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("DPAPI", ex.Message);
        Assert.Contains("Schritt 3", ex.Message);
    }

    [Fact]
    public void Load_JsonMissingField_Throws()
    {
        var fakeFs = new FakeFileSystem();
        fakeFs.Write(Path.Combine(FakeProgramData, "db.config.json"), "{}");
        var loader = DbConnectionStringLoader.CreateForTesting(
            fakeFs.Exists,
            fakeFs.Read,
            _ => null,
            FakeProgramData);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("connectionString", ex.Message);
    }

    private sealed class FakeFileSystem
    {
        private readonly Dictionary<string, string> store = new(StringComparer.OrdinalIgnoreCase);
        public void Write(string path, string content) => store[path] = content;
        public bool Exists(string path) => store.ContainsKey(path);
        public string Read(string path) => store.TryGetValue(path, out var v) ? v : throw new FileNotFoundException(path);
    }
}
