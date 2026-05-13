namespace API;

// Liefert den symmetrischen Vault-Schluessel fuer pgcrypto-basierte Operationen auf
// `temporary_credentials`. Singleton: Schluessel wird einmal beim API-Start aus der
// Konfiguration geladen und gehalten.
//
// Migrationspfad-Etappe 9a Schritt 6 Sub-A.
internal interface IVaultKeyProvider
{
    string GetSymmetricKey();
}
