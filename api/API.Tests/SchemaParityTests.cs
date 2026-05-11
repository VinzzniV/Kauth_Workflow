using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace API.Tests;

public sealed class SchemaParityTests
{
    private const string ManualDirectoryRelative = "db/manual";
    private const string ManifestRelative = "db/manual/manifest.json";
    private const string SchemaRelative = "db/01_schema.sql";

    [Fact]
    public void Manifest_CoversEveryManualSqlFile_OneToOne()
    {
        var manifest = LoadManifest();
        var manualDirectory = FindRepositoryDirectory(ManualDirectoryRelative);

        var sqlFilesOnDisk = Directory
            .EnumerateFiles(manualDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var sqlFilesInManifest = manifest.Migrations
            .Select(entry => entry.File)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var missingInManifest = sqlFilesOnDisk.Except(sqlFilesInManifest, StringComparer.Ordinal).ToList();
        var missingOnDisk = sqlFilesInManifest.Except(sqlFilesOnDisk, StringComparer.Ordinal).ToList();
        var duplicateManifestEntries = manifest.Migrations
            .GroupBy(entry => entry.File, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(
            missingInManifest.Count == 0,
            $"db/manual SQL-Datei ohne Manifest-Eintrag: {string.Join(", ", missingInManifest)}. " +
            "Eintrag in db/manual/manifest.json ergaenzen.");
        Assert.True(
            missingOnDisk.Count == 0,
            $"Manifest verweist auf fehlende Datei: {string.Join(", ", missingOnDisk)}. " +
            "Datei wiederherstellen oder Manifest-Eintrag entfernen.");
        Assert.True(
            duplicateManifestEntries.Count == 0,
            $"Doppelte Manifest-Eintraege fuer: {string.Join(", ", duplicateManifestEntries)}.");
    }

    [Fact]
    public async Task Manifest_ExpectedTokens_PresentInCanonicalSchema()
    {
        var manifest = LoadManifest();
        var schema = await File.ReadAllTextAsync(FindRepositoryFile(SchemaRelative));

        var failures = new List<string>();
        foreach (var entry in manifest.Migrations)
        {
            foreach (var expected in entry.ExpectInSchema)
            {
                if (!schema.Contains(expected, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"[{entry.File}] erwarteter Marker fehlt in db/01_schema.sql: \"{expected}\"");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Schema-Drift (manual -> 01_schema.sql):\n" + string.Join("\n", failures));
    }

    [Fact]
    public async Task Manifest_ForbiddenTokens_AbsentFromCanonicalSchema()
    {
        var manifest = LoadManifest();
        var schema = await File.ReadAllTextAsync(FindRepositoryFile(SchemaRelative));

        var failures = new List<string>();
        foreach (var entry in manifest.Migrations)
        {
            foreach (var forbidden in entry.ForbidInSchema)
            {
                if (schema.Contains(forbidden, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"[{entry.File}] verbotener Marker steht noch in db/01_schema.sql: \"{forbidden}\"");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Schema-Drift (verbotener Altstand in 01_schema.sql):\n" + string.Join("\n", failures));
    }

    [Fact]
    public void Manifest_Entries_AreWellFormed()
    {
        var manifest = LoadManifest();

        Assert.NotEmpty(manifest.Migrations);
        foreach (var entry in manifest.Migrations)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.File), "Manifest-Eintrag ohne 'file'.");
            Assert.False(string.IsNullOrWhiteSpace(entry.Description), $"Manifest-Eintrag ohne 'description': {entry.File}.");
            Assert.True(
                entry.ExpectInSchema.Count > 0 || entry.ForbidInSchema.Count > 0,
                $"Manifest-Eintrag ohne 'expect_in_schema' und ohne 'forbid_in_schema': {entry.File}. " +
                "Mindestens ein Marker pro Eintrag ist Pflicht, sonst greift der Paritaets-Check nicht.");
        }
    }

    private static ManifestRoot LoadManifest()
    {
        var manifestPath = FindRepositoryFile(ManifestRelative);
        var json = File.ReadAllText(manifestPath);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        var manifest = JsonSerializer.Deserialize<ManifestRoot>(json, options);
        Assert.NotNull(manifest);
        return manifest!;
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{relativePath}' could not be found.");
    }

    private static string FindRepositoryDirectory(string relativePath)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository directory '{relativePath}' could not be found.");
    }

    private sealed record ManifestRoot
    {
        [JsonPropertyName("migrations")]
        public List<ManifestEntry> Migrations { get; init; } = new();
    }

    private sealed record ManifestEntry
    {
        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("expect_in_schema")]
        public List<string> ExpectInSchema { get; init; } = new();

        [JsonPropertyName("forbid_in_schema")]
        public List<string> ForbidInSchema { get; init; } = new();
    }
}
