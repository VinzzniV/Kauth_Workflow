# install-db-config.ps1
#
# Schreibt die Worker-DB-Konfiguration auf der Windows-Worker-VM.
#
# Default (V2, Etappe 9a Schritt 3): DPAPI-verschluesselt in %ProgramData%\KauthWorker\db.config.dpapi.
# Scope = LocalMachine, damit der gMSA-Service-User auf derselben Maschine entschluesseln kann.
# WICHTIG: Das Skript muss auf derselben Maschine wie der spaetere Service laufen — der
# LocalMachine-Scope ist maschinen-gebunden.
#
# Dev-Fallback: `-PlainJson` schreibt Klartext-JSON nach %ProgramData%\KauthWorker\db.config.json.
# Loader bevorzugt immer die DPAPI-Datei; der Plain-Pfad existiert nur fuer Dev-Setups, der
# Loader gibt dabei eine laute Warn-Logmeldung aus.
#
# Aufruf (Default DPAPI):
#   .\install-db-config.ps1 -DbHost postgres.example.local -Database kauth_workflow `
#                           -Username kauth_worker -Password <secret>
#
# Aufruf (Dev-Klartext):
#   .\install-db-config.ps1 -DbHost ... -Database ... -Username ... -Password ... -PlainJson

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $DbHost,
    [int] $Port = 5432,
    [Parameter(Mandatory = $true)][string] $Database,
    [Parameter(Mandatory = $true)][string] $Username,
    [Parameter(Mandatory = $true)][string] $Password,
    [switch] $PlainJson
)

$ErrorActionPreference = 'Stop'

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

$connection = "Host=$DbHost;Port=$Port;Database=$Database;Username=$Username;Password=$Password;Include Error Detail=true"
$jsonPayload = [PSCustomObject]@{ connectionString = $connection } | ConvertTo-Json -Depth 3 -Compress

$dpapiPath = Join-Path $targetDir 'db.config.dpapi'
$jsonPath  = Join-Path $targetDir 'db.config.json'

if ($PlainJson) {
    # Dev-Pfad: Klartext + Warnung. Bestehende DPAPI-Datei entfernen, damit der Loader eindeutig auf
    # den Klartext faellt (DPAPI wuerde sonst Vorrang haben und der Decryptor wuerde versuchen,
    # alte Bytes zu entschluesseln).
    if (Test-Path $dpapiPath) {
        Remove-Item $dpapiPath -Force
        Write-Host "Removed existing $dpapiPath (PlainJson path takes over)."
    }

    Set-Content -Path $jsonPath -Value $jsonPayload -Encoding UTF8
    Restrict-FileAcl -Path $jsonPath
    Write-Warning "DB-Konfig wurde im KLARTEXT abgelegt ($jsonPath). Nur fuer Dev/Skeleton — Default ist DPAPI."
    Write-Host "Worker-DB-Konfig (Klartext) geschrieben: $jsonPath"
    return
}

# Default: DPAPI-Encrypt mit LocalMachine-Scope.
$plainBytes  = [System.Text.Encoding]::UTF8.GetBytes($jsonPayload)
$cipherBytes = [System.Security.Cryptography.ProtectedData]::Protect(
    $plainBytes,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::LocalMachine)

[System.IO.File]::WriteAllBytes($dpapiPath, $cipherBytes)

# Klartext-Pfad entfernen, damit keine veraltete Datei mit-existiert.
if (Test-Path $jsonPath) {
    Remove-Item $jsonPath -Force
    Write-Host "Removed legacy $jsonPath (DPAPI path is default)."
}

Restrict-FileAcl -Path $dpapiPath
Write-Host "Worker-DB-Konfig (DPAPI, LocalMachine-Scope) geschrieben: $dpapiPath"
