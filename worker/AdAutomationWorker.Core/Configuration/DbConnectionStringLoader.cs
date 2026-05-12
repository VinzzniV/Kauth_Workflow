using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AdAutomationWorker.Core.Configuration;

// Drei-Pfad-Loader fuer den Worker-DB-Connection-String. Reihenfolge:
//   1. Env-Var KAUTH_WORKER_DB_CONNECTION (Dev/Test/CI/Container).
//   2. DPAPI-File <ProgramData>\KauthWorker\db.config.dpapi (Prod, Schritt 3).
//   3. Plain-JSON <ProgramData>\KauthWorker\db.config.json (Skeleton/Dev).
//
// V1: DPAPI ist Stub. Wenn die Datei existiert, wird sie zwar gefunden, aber der
// Decrypt ist noch nicht eingebaut → harte Fehlermeldung mit Hinweis auf Schritt 3.
// Bei Json-Fallback gibt es eine laute Warn-Logmeldung — wir wollen kein Klartext-Setup
// in Prod, der Fallback existiert nur fuer den Skeleton-E2E.
public sealed class DbConnectionStringLoader
{
    public const string EnvironmentVariableName = "KAUTH_WORKER_DB_CONNECTION";

    private readonly Func<string, bool> fileExists;
    private readonly Func<string, string> readAllText;
    private readonly Func<string, string?> readEnvironmentVariable;
    private readonly string programDataDirectory;
    private readonly ILogger<DbConnectionStringLoader>? logger;

    public DbConnectionStringLoader(ILogger<DbConnectionStringLoader>? logger = null)
        : this(File.Exists, File.ReadAllText, Environment.GetEnvironmentVariable, ResolveDefaultProgramDataDirectory(), logger)
    {
    }

    internal DbConnectionStringLoader(
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory,
        ILogger<DbConnectionStringLoader>? logger)
    {
        this.fileExists = fileExists;
        this.readAllText = readAllText;
        this.readEnvironmentVariable = readEnvironmentVariable;
        this.programDataDirectory = programDataDirectory;
        this.logger = logger;
    }

    public string DpapiPath => Path.Combine(programDataDirectory, "db.config.dpapi");

    public string JsonPath => Path.Combine(programDataDirectory, "db.config.json");

    public string Load()
    {
        var fromEnvironment = readEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        if (fileExists(DpapiPath))
        {
            // TODO Schritt 3: DPAPI-Decrypt einfuegen (System.Security.Cryptography.ProtectedData,
            // DataProtectionScope.LocalMachine, gleicher Service-User-Kontext wie zur Verschluesselung).
            throw new InvalidOperationException(
                $"DPAPI-encrypted DB config detected at '{DpapiPath}'. Decryption is not yet implemented (Etappe 9a Schritt 3). " +
                "Remove the file and use the plain JSON fallback for the Skeleton-E2E, or set the environment variable " +
                $"'{EnvironmentVariableName}' temporarily.");
        }

        if (fileExists(JsonPath))
        {
            logger?.LogWarning(
                "Plain-Text DB config in use ({Path}). Skeleton/Dev only — switch to DPAPI in Etappe 9a Schritt 3.",
                JsonPath);

            var rawJson = readAllText(JsonPath);
            var parsed = JsonSerializer.Deserialize<JsonConfigShape>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed?.ConnectionString is null || string.IsNullOrWhiteSpace(parsed.ConnectionString))
            {
                throw new InvalidOperationException(
                    $"Worker DB config '{JsonPath}' is missing 'connectionString'.");
            }

            return parsed.ConnectionString.Trim();
        }

        throw new InvalidOperationException(
            $"No worker DB config found. Set environment variable '{EnvironmentVariableName}' or create '{JsonPath}'.");
    }

    private static string ResolveDefaultProgramDataDirectory()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
        {
            programData = OperatingSystem.IsWindows()
                ? @"C:\ProgramData"
                : "/var/lib/kauth";
        }
        return Path.Combine(programData, "KauthWorker");
    }

    internal static DbConnectionStringLoader CreateForTesting(
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory)
        => new(fileExists, readAllText, readEnvironmentVariable, programDataDirectory, logger: null);

    private sealed class JsonConfigShape
    {
        public string? ConnectionString { get; set; }
    }
}
