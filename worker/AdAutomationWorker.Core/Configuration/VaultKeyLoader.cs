using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AdAutomationWorker.Core.Configuration;

// Drei-Pfad-Loader fuer den Vault-Schluessel (Etappe 9a Schritt 6 Sub-B):
//   1. Env-Var KAUTH_WORKER_VAULT_KEY (Dev/Test/CI/Container).
//   2. DPAPI-File <ProgramData>\KauthWorker\vault.config.dpapi (Prod, gleiche Datenschutz-
//      Klasse wie db.config.dpapi).
//   3. Plain-JSON <ProgramData>\KauthWorker\vault.config.json (Dev-Fallback mit Warn-Log).
//
// Der Schluessel muss bitgenau identisch sein zum KAUTH_VAULT_KEY-Env-Var auf dem
// Linux-API-Host -- sonst kann `pgp_sym_decrypt` die vom Worker geschriebenen Zeilen nicht
// entschluesseln. Inbetriebnahme-Hinweis steht in worker/setup/README.md.
public sealed class VaultKeyLoader
{
    public const string EnvironmentVariableName = "KAUTH_WORKER_VAULT_KEY";
    private const int MinimumKeyLength = 32;

    private readonly Func<string, bool> fileExists;
    private readonly Func<string, string> readAllText;
    private readonly Func<string, byte[]> readAllBytes;
    private readonly Func<string, string?> readEnvironmentVariable;
    private readonly string programDataDirectory;
    private readonly IDbConfigDecryptor? decryptor;
    private readonly ILogger<VaultKeyLoader>? logger;

    public VaultKeyLoader(
        IDbConfigDecryptor? decryptor = null,
        ILogger<VaultKeyLoader>? logger = null)
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

    internal VaultKeyLoader(
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Func<string, byte[]> readAllBytes,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory,
        IDbConfigDecryptor? decryptor,
        ILogger<VaultKeyLoader>? logger)
    {
        this.fileExists = fileExists;
        this.readAllText = readAllText;
        this.readAllBytes = readAllBytes;
        this.readEnvironmentVariable = readEnvironmentVariable;
        this.programDataDirectory = programDataDirectory;
        this.decryptor = decryptor;
        this.logger = logger;
    }

    public string DpapiPath => Path.Combine(programDataDirectory, "vault.config.dpapi");

    public string JsonPath => Path.Combine(programDataDirectory, "vault.config.json");

    public string Load()
    {
        var fromEnvironment = readEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return ValidateKey(fromEnvironment.Trim(), "environment variable");
        }

        if (fileExists(DpapiPath))
        {
            if (decryptor is null)
            {
                throw new InvalidOperationException(
                    $"DPAPI-encrypted vault key detected at '{DpapiPath}', but no IDbConfigDecryptor is registered in DI. " +
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
                    "or the file was copied from another machine. Re-run install-vault-key.ps1 on this host under the " +
                    "same service user. Inner exception: " + ex.Message,
                    ex);
            }

            var plainJson = Encoding.UTF8.GetString(plainBytes);
            return ValidateKey(ParseKeyJson(plainJson, DpapiPath), DpapiPath);
        }

        if (fileExists(JsonPath))
        {
            logger?.LogWarning(
                "Plain-Text vault key in use ({Path}). Dev/Skeleton only — switch to DPAPI via install-vault-key.ps1 (default).",
                JsonPath);

            var rawJson = readAllText(JsonPath);
            return ValidateKey(ParseKeyJson(rawJson, JsonPath), JsonPath);
        }

        throw new InvalidOperationException(
            $"No vault key found. Set environment variable '{EnvironmentVariableName}' or run install-vault-key.ps1.");
    }

    private static string ParseKeyJson(string rawJson, string sourcePath)
    {
        var parsed = JsonSerializer.Deserialize<JsonConfigShape>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed?.SymmetricKey is null || string.IsNullOrWhiteSpace(parsed.SymmetricKey))
        {
            throw new InvalidOperationException(
                $"Vault key config '{sourcePath}' is missing 'symmetricKey'.");
        }

        return parsed.SymmetricKey.Trim();
    }

    private static string ValidateKey(string candidate, string source)
    {
        if (candidate.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"Vault key from '{source}' is too short ({candidate.Length} chars; required >= {MinimumKeyLength}). " +
                "Short keys defeat pgcrypto symmetric encryption.");
        }
        return candidate;
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

    internal static VaultKeyLoader CreateForTesting(
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Func<string, byte[]> readAllBytes,
        Func<string, string?> readEnvironmentVariable,
        string programDataDirectory,
        IDbConfigDecryptor? decryptor = null)
        => new(fileExists, readAllText, readAllBytes, readEnvironmentVariable, programDataDirectory, decryptor, logger: null);

    private sealed class JsonConfigShape
    {
        public string? SymmetricKey { get; set; }
    }
}
