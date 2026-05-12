namespace AdAutomationWorker.Core.Configuration;

// Plattform-neutrales Decrypt-Vertragsfenster fuer die DB-Konfig. Linux-bar in Core,
// damit `DbConnectionStringLoader` testbar bleibt. Die einzige produktive Implementierung
// `WindowsDpapiDecryptor` lebt im net8.0-windows-Host und nutzt `ProtectedData.Unprotect`
// mit `DataProtectionScope.LocalMachine`.
public interface IDbConfigDecryptor
{
    byte[] Decrypt(byte[] cipher);
}
