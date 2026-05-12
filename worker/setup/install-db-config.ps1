# install-db-config.ps1
#
# Schreibt die Worker-DB-Konfiguration auf der Windows-Worker-VM.
#
# V1 (Skeleton, Etappe 9a Schritt 2): KLARTEXT in %ProgramData%\KauthWorker\db.config.json.
# Bewusste Skeleton-Loesung — der Loader liest diesen Pfad nur als 3. Fallback und loggt eine
# Warnung. Reicht fuer den Skeleton-E2E auf einer Test-VM.
#
# TODO Schritt 3: DPAPI-Encryption mit ProtectedData (DataProtectionScope.LocalMachine, gleicher
# Service-User-Kontext wie der gMSA, der spaeter den Worker laeuft). Verschluesselte Datei landet
# in db.config.dpapi; der Loader bevorzugt dann diesen Pfad vor dem Plain-JSON.
#
# Aufruf:
#   .\install-db-config.ps1 -Host postgres.example.local -Port 5432 -Database kauth_workflow `
#                           -Username kauth_worker -Password <secret>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $DbHost,
    [int] $Port = 5432,
    [Parameter(Mandatory = $true)][string] $Database,
    [Parameter(Mandatory = $true)][string] $Username,
    [Parameter(Mandatory = $true)][string] $Password
)

$ErrorActionPreference = 'Stop'

$programData = [Environment]::GetFolderPath('CommonApplicationData')
$targetDir = Join-Path $programData 'KauthWorker'

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "Created $targetDir"
}

$targetFile = Join-Path $targetDir 'db.config.json'
$connection = "Host=$DbHost;Port=$Port;Database=$Database;Username=$Username;Password=$Password;Include Error Detail=true"

$payload = [PSCustomObject]@{
    connectionString = $connection
} | ConvertTo-Json -Depth 3

Set-Content -Path $targetFile -Value $payload -Encoding UTF8

# ACL auf den Service-User einschraenken (Pflicht in Prod-Setup).
$acl = Get-Acl $targetFile
$acl.SetAccessRuleProtection($true, $false)
$systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    'SYSTEM', 'FullControl', 'Allow')
$adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Administrators', 'FullControl', 'Allow')
$acl.AddAccessRule($systemRule)
$acl.AddAccessRule($adminRule)
Set-Acl $targetFile $acl

Write-Warning "DB-Konfig wurde im KLARTEXT abgelegt ($targetFile). Pflicht-Umstellung auf DPAPI in Etappe 9a Schritt 3."
Write-Host "Worker-DB-Konfig geschrieben: $targetFile"
