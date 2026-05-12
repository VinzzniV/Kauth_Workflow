# install-windows-service.ps1
#
# Registriert AdAutomationWorker.exe als Windows-Service.
#
# V1 (Skeleton): Service laeuft unter dem Default-Service-User (LocalSystem) oder unter dem mit
# -Credential uebergebenen Account. Der gMSA-Pfad (gMSA fuer AD-Auth, separat von DB-Auth) kommt
# in Etappe 9a Schritt 3, sobald der erste echte AD-Handler kommt — dann wird hier `-Credential
# (Get-Credential)` auf den gMSA-Account gesetzt (Format: DOMAIN\kauth-worker$).
#
# Aufruf:
#   .\install-windows-service.ps1 -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BinaryPath,
    [string] $ServiceName = 'KauthAdAutomationWorker',
    [string] $DisplayName = 'Kauth AD Automation Worker',
    [string] $Description = 'Pickt automation_jobs mit target_runtime=windows_worker aus der Workflow-DB und fuehrt registrierte Handler aus (LDAPS-Schreibpfad, gMSA-AD-Auth).'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BinaryPath)) {
    throw "Binary nicht gefunden: $BinaryPath"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service $ServiceName existiert bereits — wird gestoppt + entfernt."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

# Etappe 9a Schritt 3: hier `-Credential (Get-Credential)` auf gMSA setzen.
New-Service `
    -Name $ServiceName `
    -DisplayName $DisplayName `
    -Description $Description `
    -BinaryPathName "`"$BinaryPath`"" `
    -StartupType Manual | Out-Null

Write-Host "Service $ServiceName registriert. Manueller Start: Start-Service $ServiceName"
