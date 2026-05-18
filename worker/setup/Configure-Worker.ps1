<#
.SYNOPSIS
Gefuehrte Worker-Einrichtung: ein Aufruf, der DB-Konfig + Vault-Key + Service-
Installation in der richtigen Reihenfolge ausfuehrt.

.DESCRIPTION
Wrapper um die drei bestehenden Skripte (install-db-config.ps1, install-vault-key.ps1,
install-windows-service.ps1). Interaktiv: fragt fehlende Werte ab und zeigt klare
Voraussetzungs-Checks. Non-interactive: alle Werte als Parameter uebergeben.

Wichtig:
- Skript MUSS auf der Worker-VM unter Admin laufen (DPAPI-LocalMachine-Scope; sc.exe config).
- Der Vault-Key muss bitgenau identisch sein zum Linux-API-`KAUTH_VAULT_KEY`.
- Vor dem Service-Start: gMSA per `Install-ADServiceAccount` einrichten und
  `Test-ADServiceAccount` muss True liefern.

Volle Doku: KauthWorkflow/Betrieb/Worker-Setup.md

.PARAMETER DbHost
Postgres-Host (FQDN oder IP).

.PARAMETER DbPort
Postgres-Port (Default 5432).

.PARAMETER Database
DB-Name (z.B. kauth_workflow).

.PARAMETER DbUsername
DB-User mit Worker-Grants (z.B. kauth_worker).

.PARAMETER DbPassword
DB-Passwort. Wird im interaktiven Modus als SecureString abgefragt.

.PARAMETER VaultKey
Symmetrischer Vault-Key (Base64, mind. 32 Zeichen). Muss identisch sein zum
Linux-API-`KAUTH_VAULT_KEY`. Wird im interaktiven Modus abgefragt.

.PARAMETER BinaryPath
Pfad zur publishten AdAutomationWorker.exe (z.B. C:\Apps\KauthWorker\AdAutomationWorker.exe).

.PARAMETER ServiceAccount
gMSA-Service-Account-Name mit trailing '$' (z.B. 'DOMAIN\kauth-worker$'). Ohne
ServiceAccount laeuft der Service unter LocalSystem und kann nicht in AD schreiben —
das Skript warnt entsprechend.

.PARAMETER PlainJson
Schreibt DB-Konfig + Vault-Key im Klartext-JSON statt DPAPI. Nur fuer Dev-Setups.

.PARAMETER SkipServiceInstall
Laesst die Service-Installation aus (z.B. wenn nur die Configs neu geschrieben werden).

.PARAMETER WhatIf
Zeigt was passieren wuerde, ohne die install-*.ps1 wirklich aufzurufen.

.EXAMPLE
.\Configure-Worker.ps1
Interaktive Einrichtung — fragt jeden Wert ab.

.EXAMPLE
.\Configure-Worker.ps1 -DbHost postgres.example.local -Database kauth_workflow `
    -DbUsername kauth_worker -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
    -ServiceAccount 'DOMAIN\kauth-worker$'
Non-interactive bis auf DbPassword + VaultKey (werden weiter als SecureString abgefragt).

.EXAMPLE
.\Configure-Worker.ps1 -SkipServiceInstall
Nur DB-Konfig + Vault-Key neu schreiben (z. B. nach Vault-Key-Rotation).
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DbHost,
    [int]$DbPort = 5432,
    [string]$Database,
    [string]$DbUsername,
    [System.Security.SecureString]$DbPassword,
    [System.Security.SecureString]$VaultKey,
    [string]$BinaryPath,
    [string]$ServiceAccount,
    [switch]$PlainJson,
    [switch]$SkipServiceInstall
)

$ErrorActionPreference = "Stop"

$setupDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDbConfig = Join-Path $setupDir "install-db-config.ps1"
$installVaultKey = Join-Path $setupDir "install-vault-key.ps1"
$installService = Join-Path $setupDir "install-windows-service.ps1"

foreach ($path in @($installDbConfig, $installVaultKey, $installService)) {
    if (-not (Test-Path $path)) {
        throw "Erwartetes Skript nicht gefunden: $path. Wurde der worker/setup-Ordner vollstaendig deployed?"
    }
}

# --- Helfer ----------------------------------------------------------------

function Write-Section {
    param([Parameter(Mandatory)][string]$Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Write-Hint {
    param([Parameter(Mandatory)][string]$Text)
    Write-Host "  $Text" -ForegroundColor DarkGray
}

function Read-Value {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Hint,
        [string]$Default,
        [scriptblock]$Validator
    )
    if ($Hint) { Write-Hint $Hint }
    $defaultLabel = if ($Default) { " [$Default]" } else { "" }
    while ($true) {
        $input = Read-Host "$Name$defaultLabel"
        if ([string]::IsNullOrWhiteSpace($input) -and $Default) { $input = $Default }
        if ([string]::IsNullOrWhiteSpace($input)) {
            Write-Host "  Pflichtfeld." -ForegroundColor Yellow
            continue
        }
        if ($Validator -and -not (& $Validator $input)) { continue }
        return $input
    }
}

function Read-SecureValue {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Hint,
        [int]$MinLength = 0
    )
    if ($Hint) { Write-Hint $Hint }
    while ($true) {
        $secure = Read-Host "$Name" -AsSecureString
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try {
            $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        } finally {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
        if ([string]::IsNullOrWhiteSpace($plain)) {
            Write-Host "  Pflichtfeld." -ForegroundColor Yellow
            continue
        }
        if ($MinLength -gt 0 -and $plain.Length -lt $MinLength) {
            Write-Host "  Mindestens $MinLength Zeichen erforderlich (eingegeben: $($plain.Length))." -ForegroundColor Yellow
            continue
        }
        return $plain
    }
}

function ConvertTo-PlainString {
    param([System.Security.SecureString]$Secure)
    if (-not $Secure) { return $null }
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Test-IsAdmin {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# --- Voraussetzungs-Check --------------------------------------------------

function Invoke-Prerequisites {
    Write-Section "Voraussetzungs-Check"

    if (-not $IsWindows) {
        throw "Configure-Worker.ps1 ist Windows-spezifisch (DPAPI + Windows-Service). Aktuelles OS ist nicht Windows."
    }

    if (-not (Test-IsAdmin)) {
        throw "Bitte als Administrator ausfuehren (DPAPI-LocalMachine-Scope + sc.exe config brauchen Admin-Rechte)."
    }
    Write-Hint "Admin-Kontext: ok."

    $domain = (Get-CimInstance -ClassName Win32_ComputerSystem).Domain
    if ($domain -and $domain -ne "WORKGROUP") {
        Write-Hint "Domain-joined: $domain"
    } else {
        Write-Warning "Maschine ist nicht domain-joined ($domain). LDAPS-Schreibhandler werden ohne Domain-Kontext nicht funktionieren."
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $version = & dotnet --version 2>$null
        Write-Hint ".NET-Runtime gefunden: $version"
    } else {
        Write-Warning ".NET-Runtime nicht im PATH. Fuer Worker-Service wird .NET 8 Runtime benoetigt."
    }

    Write-Hint "gMSA-Pruefung erfolgt vor Service-Start (`Test-ADServiceAccount <name>` muss True liefern)."
}

# --- DB-Konfig -------------------------------------------------------------

function Invoke-DbConfig {
    Write-Section "1/3 — Postgres-Konfiguration (-> %ProgramData%\KauthWorker\db.config.dpapi)"

    if (-not $DbHost) { $DbHost = Read-Value -Name "DB-Host" -Hint "FQDN oder IP des Postgres-Servers" }
    if (-not $Database) { $Database = Read-Value -Name "Database" -Default "kauth_workflow" }
    if (-not $DbUsername) { $DbUsername = Read-Value -Name "DB-Username" -Hint "z. B. kauth_worker" }
    if (-not $DbPassword) {
        $plainPwd = Read-SecureValue -Name "DB-Password" -Hint "Wird ohne Echo eingegeben"
    } else {
        $plainPwd = ConvertTo-PlainString -Secure $DbPassword
    }

    $args = @{
        DbHost   = $DbHost
        Port     = $DbPort
        Database = $Database
        Username = $DbUsername
        Password = $plainPwd
    }
    if ($PlainJson) { $args["PlainJson"] = $true }

    if ($PSCmdlet.ShouldProcess("install-db-config.ps1", "DB-Konfig schreiben fuer $DbUsername@$DbHost`:$DbPort/$Database")) {
        & $installDbConfig @args
    }
}

# --- Vault-Key -------------------------------------------------------------

function Invoke-VaultKey {
    Write-Section "2/3 — Vault-Key (-> %ProgramData%\KauthWorker\vault.config.dpapi)"
    Write-Hint "Muss bitgenau identisch sein zum Linux-API-KAUTH_VAULT_KEY."

    if (-not $VaultKey) {
        $plainKey = Read-SecureValue -Name "Vault-Key" -MinLength 32 -Hint "Base64-Wert, mind. 32 Zeichen"
    } else {
        $plainKey = ConvertTo-PlainString -Secure $VaultKey
        if ($plainKey.Length -lt 32) {
            throw "VaultKey ist zu kurz ($($plainKey.Length) Zeichen; mindestens 32 erforderlich)."
        }
    }

    $args = @{ SymmetricKey = $plainKey }
    if ($PlainJson) { $args["PlainJson"] = $true }

    if ($PSCmdlet.ShouldProcess("install-vault-key.ps1", "Vault-Key schreiben")) {
        & $installVaultKey @args
    }
}

# --- Windows-Service -------------------------------------------------------

function Invoke-ServiceInstall {
    Write-Section "3/3 — Windows-Service KauthAdAutomationWorker"

    if (-not $BinaryPath) {
        $BinaryPath = Read-Value -Name "BinaryPath" `
            -Hint "Pfad zur publishten AdAutomationWorker.exe, z. B. C:\Apps\KauthWorker\AdAutomationWorker.exe" `
            -Default "C:\Apps\KauthWorker\AdAutomationWorker.exe"
    }

    if (-not (Test-Path $BinaryPath)) {
        throw "Binary nicht gefunden: $BinaryPath. Zuerst `dotnet publish worker/AdAutomationWorker -c Release -o <Pfad>` ausfuehren."
    }

    if (-not $ServiceAccount) {
        Write-Hint "gMSA-Account mit trailing '$', z. B. DOMAIN\kauth-worker$. Leer lassen = LocalSystem (LDAPS-Handler funktionieren dann nicht)."
        $ServiceAccount = Read-Host "ServiceAccount (leer = LocalSystem)"
    }

    if ($ServiceAccount -and -not $ServiceAccount.EndsWith('$')) {
        Write-Warning "gMSA-Namen enden ueblicherweise auf '$' (z. B. DOMAIN\kauth-worker`$). Fortsetzen mit '$ServiceAccount'? [j/N]"
        $confirm = Read-Host
        if ($confirm -notmatch '^[jJyY]$') {
            throw "Abgebrochen — bitte korrekten gMSA-Namen angeben (mit trailing `$)."
        }
    }

    $args = @{ BinaryPath = $BinaryPath }
    if ($ServiceAccount) { $args["ServiceAccount"] = $ServiceAccount }

    if ($PSCmdlet.ShouldProcess("install-windows-service.ps1", "Service registrieren mit BinaryPath=$BinaryPath; ServiceAccount=$ServiceAccount")) {
        & $installService @args
    }
}

# --- Smoke -----------------------------------------------------------------

function Invoke-Smoke {
    Write-Section "Smoke"
    $svc = Get-Service -Name "KauthAdAutomationWorker" -ErrorAction SilentlyContinue
    if ($svc) {
        Write-Hint "Service KauthAdAutomationWorker: $($svc.Status)"
        Write-Hint "Manueller Start: Start-Service KauthAdAutomationWorker"
    } else {
        Write-Warning "Service KauthAdAutomationWorker nicht gefunden — bitte Schritt 3 pruefen."
    }

    $programData = [Environment]::GetFolderPath('CommonApplicationData')
    $dbDpapi = Join-Path $programData "KauthWorker\db.config.dpapi"
    $vaultDpapi = Join-Path $programData "KauthWorker\vault.config.dpapi"
    foreach ($file in @($dbDpapi, $vaultDpapi)) {
        if (Test-Path $file) {
            Write-Hint "vorhanden: $file"
        } else {
            Write-Warning "fehlt: $file"
        }
    }
}

# --- Main ------------------------------------------------------------------

Write-Host ""
Write-Host "Worker-Einrichtung (Configure-Worker.ps1)" -ForegroundColor Green
Write-Hint "Doku: KauthWorkflow/Betrieb/Worker-Setup.md"

Invoke-Prerequisites
Invoke-DbConfig
Invoke-VaultKey
if (-not $SkipServiceInstall) {
    Invoke-ServiceInstall
} else {
    Write-Section "3/3 — Service-Installation ausgelassen (-SkipServiceInstall)"
}
Invoke-Smoke

Write-Host ""
Write-Host "Fertig." -ForegroundColor Green
Write-Host "Naechste Schritte:" -ForegroundColor Cyan
Write-Host "  - gMSA verifizieren:        Test-ADServiceAccount <name>"
Write-Host "  - Service starten:          Start-Service KauthAdAutomationWorker"
Write-Host "  - Smoke gegen Test-DC:      siehe Worker-Setup.md (E2E CreateAdUserLdaps)"
