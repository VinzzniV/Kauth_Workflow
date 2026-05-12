using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AdAutomationWorker.Core.Configuration;

// Drei-Pfad-Loader fuer den Worker-DB-Connection-String. Reihenfolge:
//   1. Env-Var KAUTH_WORKER_DB_CONNECTION (Dev/Test/CI/Container).
//   2. DPAPI-File <ProgramData>\KauthWorker\db.config.dpapi (Prod, Etappe 9a Schritt 3).
//   3. Plain-JSON <ProgramData>\KauthWorker\db.config.json (Dev/Skeleton-Fallback).
//
// DPAPI-Pfad (seit Schritt 3): Loader bekommt einen IDbConfigDecryptor injected (im Host
// `WindowsDpapiDecryptor` mit DataProtectionScope.LocalMachine). Fehlt der Decryptor + Datei
// existiert -> harter Misconfig-Throw. Decrypt-Failure wird zu einer Fehlermeldung mit Hinweis
// auf install-db-config.ps1 (gleicher Service-User + dieselbe Maschine sind Voraussetzung).
//
// Plain-JSON bleibt als Dev-Fallback mit lautem Warn-Log; Default ist DPAPI.
public sealed class DbConnectionStringLoader
{
    public const string EnvironmentVariableName = "KAUTH_WORKER_DB_CONNECTION";

    private readonly Func<string, bool> fileExists;
    private readonly Func<string, string> readAllText;
    private readonly Func<string, byte[]> readAllBytes;
    private readonly Func<string, string?> readEnvironmentVariable;
    private readonly string programDataDirectory;
    private readonly IDbConfigDecryptor? decryptor;
    private readonly ILogger<DbConnectionStringLoader>? logger;

    public DbConnectionStringLoader(
        IDbConfigDecryptor? decryptor = null,
        ILogger<DbConnectionStringLoader>? logger = null)
        : this(
            File.Exists,
            File.ReadAllText,
            File.ReadAllBytes,
            Environment.GetEnvironmentVariable,
            ResolveDefaultProgramDataDirectory(),
            decryptor,
            logger)
    {
    }

    internal DbConnectionStringLoader(
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Func<string, byte[]> readAllBytes,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory,
        IDbConfigDecryptor? decryptor,
        ILogger<DbConnectionStringLoader>? logger)
    {
        this.fileExists = fileExists;
        this.readAllText = readAllText;
        this.readAllBytes = readAllBytes;
        this.readEnvironmentVariable = readEnvironmentVariable;
        this.programDataDirectory = programDataDirectory;
        this.decryptor = decryptor;
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
            if (decryptor is null)
            {
                throw new InvalidOperationException(
                    $"DPAPI-encrypted DB config detected at '{DpapiPath}', but no IDbConfigDecryptor is registered in DI. " +
                    "Register WindowsDpapiDecryptor in Program.cs, or remove the file to fall back to JSON.");
            }

            byte[] plainBytes;
            try
            {
                var cipher = readAllBytes(DpapiPath);
                plainBytes = decryptor.Decrypt(cipher);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"DPAPI decryption of '{DpapiPath}' failed. Scope is LocalMachine — possibly different service user " +
                    "or the file was copied from another machine. Re-run install-db-config.ps1 on this host under the " +
                    "same service user. Inner exception: " + ex.Message,
                    ex);
            }

            var plainJson = Encoding.UTF8.GetString(plainBytes);
            return ParseConnectionStringJson(plainJson, DpapiPath);
        }

        if (fileExists(JsonPath))
        {
            logger?.LogWarning(
                "Plain-Text DB config in use ({Path}). Dev/Skeleton only — switch to DPAPI via install-db-config.ps1 (default).",
                JsonPath);

            var rawJson = readAllText(JsonPath);
            return ParseConnectionStringJson(rawJson, JsonPath);
        }

        throw new InvalidOperationException(
            $"No worker DB config found. Set environment variable '{EnvironmentVariableName}' or run install-db-config.ps1.");
    }

    private static string ParseConnectionStringJson(string rawJson, string sourcePath)
    {
        var parsed = JsonSerializer.Deserialize<JsonConfigShape>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed?.ConnectionString is null || string.IsNullOrWhiteSpace(parsed.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Worker DB config '{sourcePath}' is missing 'connectionString'.");
        }

        return parsed.ConnectionString.Trim();
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
        Func<string, byte[]> readAllBytes,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory,
        IDbConfigDecryptor? decryptor = null)
        => new(fileExists, readAllText, readAllBytes, readEnvironmentVariable, programDataDirectory, decryptor, logger: null);

    private sealed class JsonConfigShape
    {
        public string? ConnectionString { get; set; }
    }
}
