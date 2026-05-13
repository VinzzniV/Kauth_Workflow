namespace AdAutomationWorker.Core.Configuration;

// Schmaler Halter fuer den Vault-Key. Wird in der DI als Singleton registriert; PostgresWorkerJobStore
// nimmt den Provider als Parameter und fragt pro Vault-Insert den Key ab. Damit ist der Key einmal
// pro Worker-Lifetime geladen und gehalten, nicht pro Insert neu von Disk gelesen.
//
// Etappe 9a Schritt 6 Sub-B.
public sealed class VaultKeyProvider
{
    private readonly string symmetricKey;

    public VaultKeyProvider(string symmetricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symmetricKey);
        this.symmetricKey = symmetricKey;
    }

    public string GetSymmetricKey() => symmetricKey;
}
