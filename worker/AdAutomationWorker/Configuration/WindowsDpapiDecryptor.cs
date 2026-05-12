using System.Security.Cryptography;
using AdAutomationWorker.Core.Configuration;

namespace AdAutomationWorker.Configuration;

// Windows-only DPAPI-Decrypt-Adapter. LocalMachine-Scope: gleicher Service-User + dieselbe
// Maschine sind Voraussetzung — install-db-config.ps1 verschluesselt mit demselben Scope.
internal sealed class WindowsDpapiDecryptor : IDbConfigDecryptor
{
    public byte[] Decrypt(byte[] cipher)
        => ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.LocalMachine);
}
