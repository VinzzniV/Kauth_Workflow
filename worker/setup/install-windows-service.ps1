# install-windows-service.ps1
#
# Registriert AdAutomationWorker.exe als Windows-Service. Setzt optional einen gMSA als
# Service-Account ueber `sc.exe config obj=`.
#
# Voraussetzungen fuer den gMSA-Pfad (Pflicht fuer LDAPS-Schreib-Handler ab Etappe 9a Schritt 3):
#   - Domain-Admin hat den gMSA angelegt (`New-ADServiceAccount`) und der Worker-VM den
#     PrincipalsAllowedToRetrieveManagedPassword-Eintrag gegeben.
#   - Auf der Worker-VM ist `Install-ADServiceAccount <name>` gelaufen.
#   - Der gMSA hat auf der Ziel-OU `Create Child Objects` (user) und `Reset Password`-Rechte.
# Details: siehe ../setup/README.md
#
# Aufruf:
#   .\install-windows-service.ps1 -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
#                                 -ServiceAccount 'DOMAIN\kauth-worker$'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BinaryPath,
    [string] $ServiceName = 'KauthAdAutomationWorker',
    [string] $DisplayName = 'Kauth AD Automation Worker',
    [string] $Description = 'Pickt automation_jobs mit target_runtime=windows_worker aus der Workflow-DB und fuehrt registrierte Handler aus (LDAPS-Schreibpfad, gMSA-AD-Auth, DPAPI-DB-Auth).',
    [string] $ServiceAccount
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

New-Service `
    -Name $ServiceName `
    -DisplayName $DisplayName `
    -Description $Description `
    -BinaryPathName "`"$BinaryPath`"" `
    -StartupType Manual | Out-Null

if ($ServiceAccount) {
    # New-Service akzeptiert -Credential mit gMSA und leerem Password nicht direkt; sc.exe config
    # ist hier der zuverlaessige Weg. Achtung: trailing '$' Pflicht bei gMSA-Accounts.
    Write-Host "Setze Service-Account auf $ServiceAccount (gMSA wenn Name auf '$' endet)."
    & sc.exe config $ServiceName obj= $ServiceAccount | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe config schlug fehl (Exit $LASTEXITCODE). Pruefe Service-Account-Format DOMAIN\name$ und Install-ADServiceAccount-Lauf."
    }
    Write-Host "Service-Account gesetzt. Vor dem Start sicherstellen, dass der gMSA per Test-ADServiceAccount validiert ist."
} else {
    Write-Warning "Kein -ServiceAccount uebergeben. Service laeuft unter LocalSystem; LDAPS-Handler werden ohne Domain-Auth nicht funktionieren."
}

Write-Host "Service $ServiceName registriert. Manueller Start: Start-Service $ServiceName"
