namespace API;

// Vault-Key-Quelle: Env-Var KAUTH_VAULT_KEY. Bewusst nicht aus appsettings.json —
// produktive Keys gehoeren nie in versioniertes Config-Material. Auf Windows-Worker-Seite
// liegt derselbe Key DPAPI-encrypted als `vault.config.dpapi` (siehe Worker-Slice 6b).
//
// Mindestlaenge 32 ist ein Plausibilitaets-Check (kurze Keys sind eine Fehlkonfiguration);
// pgcrypto akzeptiert beliebige Laengen, aber `pgp_sym_encrypt` mit einem 8-Zeichen-Key
// ist effektiv keine Verschluesselung.
internal sealed class EnvVaultKeyProvider : IVaultKeyProvider
{
    public const string EnvironmentVariableName = "KAUTH_VAULT_KEY";
    private const int MinimumKeyLength = 32;

    private readonly string symmetricKey;

    public EnvVaultKeyProvider()
        : this(Environment.GetEnvironmentVariable(EnvironmentVariableName))
    {
    }

    internal EnvVaultKeyProvider(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(
                $"Environment variable '{EnvironmentVariableName}' is not set. " +
                "Set it to the symmetric Vault key (same value as the Worker's vault.config.dpapi).");
        }

        var trimmed = rawValue.Trim();
        if (trimmed.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"Environment variable '{EnvironmentVariableName}' is too short " +
                $"({trimmed.Length} chars; required >= {MinimumKeyLength}). " +
                "Short keys defeat pgcrypto symmetric encryption.");
        }

        symmetricKey = trimmed;
    }

    public string GetSymmetricKey() => symmetricKey;
}
