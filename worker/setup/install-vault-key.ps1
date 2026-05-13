# install-vault-key.ps1
#
# Schreibt den Vault-Schluessel auf der Windows-Worker-VM (Etappe 9a Schritt 6).
#
# Default: DPAPI-verschluesselt in %ProgramData%\KauthWorker\vault.config.dpapi mit
# LocalMachine-Scope, damit der gMSA-Service-User auf derselben Maschine entschluesseln kann.
#
# Dev-Fallback: `-PlainJson` schreibt Klartext-JSON in %ProgramData%\KauthWorker\vault.config.json.
# Der Loader bevorzugt immer DPAPI; im Klartext-Pfad warnt er laut.
#
# Wichtig: Der hier gespeicherte Wert muss bitgenau identisch sein zum `KAUTH_VAULT_KEY`-Env-Var
# auf dem Linux-API-Host. Andernfalls kann die API die vom Worker geschriebenen Eintraege nicht
# entschluesseln und der Welcome-Mail-Pfad scheitert mit klarer Meldung.
#
# Aufruf (Default DPAPI):
#   .\install-vault-key.ps1 -SymmetricKey '<32+ Zeichen>'
#
# Aufruf (Dev-Klartext):
#   .\install-vault-key.ps1 -SymmetricKey '<32+ Zeichen>' -PlainJson

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SymmetricKey,
    [switch] $PlainJson
)

$ErrorActionPreference = 'Stop'

if ($SymmetricKey.Length -lt 32) {
    throw "SymmetricKey ist zu kurz ($($SymmetricKey.Length) Zeichen; mindestens 32 erforderlich)."
}

function Restrict-FileAcl {
    param([Parameter(Mandatory = $true)][string]$Path)

    $acl = Get-Acl $Path
    $acl.SetAccessRuleProtection($true, $false)
    $systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        'SYSTEM', 'FullControl', 'Allow')
    $adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        'BUILTIN\Administrators', 'FullControl', 'Allow')
    $acl.AddAccessRule($systemRule)
    $acl.AddAccessRule($adminRule)
    Set-Acl $Path $acl
}

$programData = [Environment]::GetFolderPath('CommonApplicationData')
$targetDir = Join-Path $programData 'KauthWorker'

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "Created $targetDir"
}

$jsonPayload = [PSCustomObject]@{ symmetricKey = $SymmetricKey } | ConvertTo-Json -Depth 3 -Compress

$dpapiPath = Join-Path $targetDir 'vault.config.dpapi'
$jsonPath  = Join-Path $targetDir 'vault.config.json'

if ($PlainJson) {
    if (Test-Path $dpapiPath) {
        Remove-Item $dpapiPath -Force
        Write-Host "Removed existing $dpapiPath (PlainJson path takes over)."
    }

    Set-Content -Path $jsonPath -Value $jsonPayload -Encoding UTF8
    Restrict-FileAcl -Path $jsonPath
    Write-Warning "Vault-Key wurde im KLARTEXT abgelegt ($jsonPath). Nur fuer Dev/Skeleton — Default ist DPAPI."
    Write-Host "Vault-Key (Klartext) geschrieben: $jsonPath"
    return
}

$plainBytes  = [System.Text.Encoding]::UTF8.GetBytes($jsonPayload)
$cipherBytes = [System.Security.Cryptography.ProtectedData]::Protect(
    $plainBytes,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::LocalMachine)

[System.IO.File]::WriteAllBytes($dpapiPath, $cipherBytes)

if (Test-Path $jsonPath) {
    Remove-Item $jsonPath -Force
    Write-Host "Removed legacy $jsonPath (DPAPI path is default)."
}

Restrict-FileAcl -Path $dpapiPath
Write-Host "Vault-Key (DPAPI, LocalMachine-Scope) geschrieben: $dpapiPath"
