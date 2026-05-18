<#
.SYNOPSIS
Interaktiver Konfigurations-Wizard fuer .env.prod (Default) und web/.env.local.

.DESCRIPTION
Fuehrt durch alle relevanten Variablen, validiert Eingaben und schreibt die
kanonischen Konfig-Dateien. Bestehende Dateien werden vor dem Ueberschreiben
mit Zeitstempel-Backup gesichert.

Modi:
  -Profile prod      Erst-Einrichtung der .env.prod (Default).
  -Profile dev-web   Erst-Einrichtung der web/.env.local (nur Frontend-Dev).
  -Update            Zeigt aktuelle Werte als Default und fragt jeden ab.
  -Show              Read-only-Anzeige der aktuellen Werte (Secrets redacted).

Vollstaendige Variablen-Uebersicht: KauthWorkflow/Betrieb/Konfiguration.md

.EXAMPLE
.\scripts\Configure.ps1
.\scripts\Configure.ps1 -Profile dev-web
.\scripts\Configure.ps1 -Update
.\scripts\Configure.ps1 -Show
#>

[CmdletBinding()]
param(
    [ValidateSet("prod", "dev-web")]
    [string]$Profile = "prod",
    [switch]$Update,
    [switch]$Show
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$envProdPath = Join-Path $repoRoot ".env.prod"
$envWebLocalPath = Join-Path $repoRoot "web/.env.local"

# Variablen, deren Werte beim -Show oder -Update als Secret redacted angezeigt werden.
$SecretVars = @(
    "POSTGRES_PASSWORD",
    "ENTRA_CLIENT_SECRET",
    "GRAPH_CLIENT_SECRET",
    "KAUTH_VAULT_KEY"
)

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
        [scriptblock]$Validator,
        [switch]$AllowEmpty
    )
    if ($Hint) { Write-Hint $Hint }
    $defaultLabel = if ($null -ne $Default -and $Default -ne "") { " [$Default]" } else { "" }
    while ($true) {
        $input = Read-Host "$Name$defaultLabel"
        if ([string]::IsNullOrWhiteSpace($input)) {
            $input = if ($null -ne $Default) { $Default } else { "" }
        }
        if (-not $AllowEmpty -and [string]::IsNullOrWhiteSpace($input)) {
            Write-Host "  Pflichtfeld — bitte einen Wert eingeben." -ForegroundColor Yellow
            continue
        }
        if ($Validator) {
            $valid = & $Validator $input
            if (-not $valid) { continue }
        }
        return $input
    }
}

function Read-Secret {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Hint,
        [string]$CurrentValue,
        [switch]$AllowEmpty,
        [scriptblock]$Generator
    )
    if ($Hint) { Write-Hint $Hint }
    $statusLabel = if ($CurrentValue) { " [aktuell gesetzt — leer = behalten]" } elseif ($Generator) { " [leer = generieren]" } else { "" }
    while ($true) {
        $secure = Read-Host "$Name$statusLabel" -AsSecureString
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try {
            $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        } finally {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
        if ([string]::IsNullOrWhiteSpace($plain)) {
            if ($CurrentValue) { return $CurrentValue }
            if ($Generator) {
                $generated = & $Generator
                Write-Host "  -> Generiert." -ForegroundColor Green
                return $generated
            }
            if ($AllowEmpty) { return "" }
            Write-Host "  Pflichtfeld — bitte einen Wert eingeben (oder Skript abbrechen)." -ForegroundColor Yellow
            continue
        }
        return $plain
    }
}

function Test-IsHttpsUrl {
    param([string]$Value)
    if ($Value -match '^https://[^\s]+$') { return $true }
    Write-Host "  Muss eine https://-URL sein." -ForegroundColor Yellow
    return $false
}

function Test-IsGuid {
    param([string]$Value)
    [Guid]$out = [Guid]::Empty
    if ([Guid]::TryParse($Value, [ref]$out)) { return $true }
    Write-Host "  Muss eine GUID sein (00000000-0000-0000-0000-000000000000)." -ForegroundColor Yellow
    return $false
}

function Test-IsPositiveInt {
    param([string]$Value)
    [int]$out = 0
    if ([int]::TryParse($Value, [ref]$out) -and $out -gt 0) { return $true }
    Write-Host "  Muss eine positive Zahl sein." -ForegroundColor Yellow
    return $false
}

function New-RandomPassword {
    param([int]$Length = 32)
    $bytes = New-Object byte[] $Length
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes).Substring(0, $Length)
}

function New-VaultKey {
    # 32 Bytes (256 Bit), Base64 — passt zum API-EnvVaultKeyProvider.
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes)
}

function Read-EnvFile {
    param([Parameter(Mandatory)][string]$Path)
    $result = [ordered]@{}
    if (-not (Test-Path $Path)) { return $result }
    foreach ($line in Get-Content $Path) {
        $match = [regex]::Match($line, '^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*)$')
        if ($match.Success) {
            $key = $match.Groups[1].Value
            $value = $match.Groups[2].Value.Trim()
            # einfache Quote-Entfernung am Anfang+Ende
            if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") {
                $value = $matches[1]
            }
            $result[$key] = $value
        }
    }
    return $result
}

function Backup-File {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    $timestamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
    $backupPath = "$Path.bak.$timestamp"
    Copy-Item -Path $Path -Destination $backupPath
    Write-Host "Backup angelegt: $backupPath" -ForegroundColor DarkGray
    return $backupPath
}

function Format-RedactedValue {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Value
    )
    if ($SecretVars -contains $Name) {
        if ([string]::IsNullOrWhiteSpace($Value)) { return "(unset)" }
        return "(set, $($Value.Length) Zeichen)"
    }
    if ([string]::IsNullOrWhiteSpace($Value)) { return "(unset)" }
    return $Value
}

function Show-EnvFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )
    Write-Section "$Label ($Path)"
    if (-not (Test-Path $Path)) {
        Write-Host "  Datei existiert nicht." -ForegroundColor Yellow
        return
    }
    $values = Read-EnvFile -Path $Path
    if ($values.Count -eq 0) {
        Write-Host "  Keine Variablen gefunden." -ForegroundColor Yellow
        return
    }
    foreach ($key in $values.Keys) {
        $redacted = Format-RedactedValue -Name $key -Value $values[$key]
        Write-Host ("  {0,-50} = {1}" -f $key, $redacted)
    }
}

# --- Prod-Flow -------------------------------------------------------------

function Invoke-ProdWizard {
    Write-Host ""
    Write-Host "Konfigurations-Wizard fuer .env.prod" -ForegroundColor Green
    Write-Hint "Variablen-Uebersicht: KauthWorkflow/Betrieb/Konfiguration.md"

    $existing = Read-EnvFile -Path $envProdPath
    if ($existing.Count -gt 0 -and -not $Update) {
        Write-Host ""
        Write-Host ".env.prod existiert bereits ($($existing.Count) Variablen)." -ForegroundColor Yellow
        $continue = Read-Host "Ueberschreiben? Bestehende Werte werden als Default vorgeschlagen [j/N]"
        if ($continue -notmatch '^[jJyY]$') {
            Write-Host "Abgebrochen — keine Aenderungen." -ForegroundColor Yellow
            return
        }
    }

    $values = [ordered]@{}

    Write-Section "Grunddaten"
    $values["PUBLIC_HOSTNAME"] = Read-Value -Name "PUBLIC_HOSTNAME" `
        -Hint "DNS-Name ohne Schema, z.B. onboarding.example.local" `
        -Default $existing["PUBLIC_HOSTNAME"]
    $values["PUBLIC_BASE_URL"] = Read-Value -Name "PUBLIC_BASE_URL" `
        -Hint "Oeffentliche https-URL, muss mit der Entra SPA Redirect-URI uebereinstimmen" `
        -Default $existing["PUBLIC_BASE_URL"] `
        -Validator ${function:Test-IsHttpsUrl}

    Write-Section "Web-Container"
    $values["WEB_BIND_HOST"] = Read-Value -Name "WEB_BIND_HOST" -Default ($existing["WEB_BIND_HOST"] ?? "0.0.0.0") -AllowEmpty
    $values["WEB_HTTP_PORT"] = Read-Value -Name "WEB_HTTP_PORT" -Default ($existing["WEB_HTTP_PORT"] ?? "80") -Validator ${function:Test-IsPositiveInt}
    $values["WEB_HTTPS_PORT"] = Read-Value -Name "WEB_HTTPS_PORT" -Default ($existing["WEB_HTTPS_PORT"] ?? "443") -Validator ${function:Test-IsPositiveInt}
    $values["WEB_AUTH_MODE"] = Read-Value -Name "WEB_AUTH_MODE" `
        -Hint "Auth-Modus im Web-Container (dev-sim oder entra); separat vom API-AUTH_MODE" `
        -Default ($existing["WEB_AUTH_MODE"] ?? "entra")

    Write-Section "Datenbank"
    $values["POSTGRES_DB"] = Read-Value -Name "POSTGRES_DB" -Default ($existing["POSTGRES_DB"] ?? "appdb")
    $values["POSTGRES_USER"] = Read-Value -Name "POSTGRES_USER" -Default ($existing["POSTGRES_USER"] ?? "app")
    $values["POSTGRES_PASSWORD"] = Read-Secret -Name "POSTGRES_PASSWORD" `
        -Hint "leer = 32-Char-Random generieren" `
        -CurrentValue $existing["POSTGRES_PASSWORD"] `
        -Generator ${function:New-RandomPassword}

    Write-Section "Entra (Authentifizierung + Graph)"
    Write-Hint "Tenant + Client IDs muessen GUID-Format haben."
    $values["ENTRA_TENANT_ID"] = Read-Value -Name "ENTRA_TENANT_ID" -Default $existing["ENTRA_TENANT_ID"] -Validator ${function:Test-IsGuid}
    $values["ENTRA_CLIENT_ID"] = Read-Value -Name "ENTRA_CLIENT_ID" -Default $existing["ENTRA_CLIENT_ID"] -Validator ${function:Test-IsGuid}
    $values["ENTRA_AUDIENCE"] = Read-Value -Name "ENTRA_AUDIENCE" `
        -Hint "z.B. api://00000000-0000-0000-0000-000000000000" `
        -Default $existing["ENTRA_AUDIENCE"]
    $values["ENTRA_CLIENT_SECRET"] = Read-Secret -Name "ENTRA_CLIENT_SECRET" `
        -Hint "Mindestens eins von ENTRA_CLIENT_SECRET oder GRAPH_CLIENT_SECRET ist Pflicht in prod" `
        -CurrentValue $existing["ENTRA_CLIENT_SECRET"] `
        -AllowEmpty
    $values["GRAPH_CLIENT_SECRET"] = Read-Secret -Name "GRAPH_CLIENT_SECRET" `
        -Hint "Optionaler Graph/Mail-Secret-Override; leer lassen wenn ENTRA_CLIENT_SECRET gesetzt ist" `
        -CurrentValue $existing["GRAPH_CLIENT_SECRET"] `
        -AllowEmpty

    Write-Section "Graph-Application-Permissions (Admin-Consent)"
    Write-Hint "Folgende Application-Permissions muessen im Entra-Portal mit Admin-Consent freigegeben sein:"
    Write-Hint "  - Mail.Send"
    Write-Hint "  - User.Read.All"
    Write-Hint "  - LicenseAssignment.ReadWrite.All"
    Write-Hint "  - Group.Read.All (fuer reference_user.groups Mapping-Source)"
    if ($values["ENTRA_CLIENT_ID"] -and $values["ENTRA_TENANT_ID"]) {
        $portalUrl = "https://entra.microsoft.com/$($values['ENTRA_TENANT_ID'])/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$($values['ENTRA_CLIENT_ID'])"
        $openPortal = Read-Host "App-Registration im Browser oeffnen? [j/N]"
        if ($openPortal -match '^[jJyY]$') { Start-Process $portalUrl }
    }

    Write-Section "Vault (Initial-Passwoerter)"
    Write-Hint "Symmetrischer Schluessel (Base64, 32 Bytes). Muss identisch sein zum Worker-vault.config.dpapi."
    $values["KAUTH_VAULT_KEY"] = Read-Secret -Name "KAUTH_VAULT_KEY" `
        -CurrentValue $existing["KAUTH_VAULT_KEY"] `
        -Generator ${function:New-VaultKey}

    Write-Section "Directory-Sync"
    $values["DIRECTORY_GROUP_PREFIX"] = Read-Value -Name "DIRECTORY_GROUP_PREFIX" `
        -Hint "Prefix fuer Sync-Gruppen, z.B. Onboarding-App-" `
        -Default ($existing["DIRECTORY_GROUP_PREFIX"] ?? "Onboarding-App-") `
        -AllowEmpty
    $values["DIRECTORY_EXPLICIT_GROUP_IDS"] = Read-Value -Name "DIRECTORY_EXPLICIT_GROUP_IDS" `
        -Hint "Komma-getrennte explizite Gruppen-IDs (optional)" `
        -Default $existing["DIRECTORY_EXPLICIT_GROUP_IDS"] -AllowEmpty
    $values["DIRECTORY_SYNC_SCHEDULED"] = Read-Value -Name "DIRECTORY_SYNC_SCHEDULED" `
        -Default ($existing["DIRECTORY_SYNC_SCHEDULED"] ?? "true")
    $values["DIRECTORY_SYNC_INTERVAL_MINUTES"] = Read-Value -Name "DIRECTORY_SYNC_INTERVAL_MINUTES" `
        -Hint "Empfohlen 60 (stuendlich) bis 480 (alle 8h)" `
        -Default ($existing["DIRECTORY_SYNC_INTERVAL_MINUTES"] ?? "60") `
        -Validator ${function:Test-IsPositiveInt}
    $values["AUTO_PROVISION_DEFAULT_ROLE_KEY"] = Read-Value -Name "AUTO_PROVISION_DEFAULT_ROLE_KEY" `
        -Hint "Default-Rolle fuer Auto-Provision; leer = keine automatische Rollenzuweisung" `
        -Default $existing["AUTO_PROVISION_DEFAULT_ROLE_KEY"] -AllowEmpty

    Write-Section "Automation-Retry (optional)"
    Write-Hint "Leer lassen = Code-Defaults (3 / 60s / 300s) verwenden."
    $values["WORKFLOW_AUTOMATION_MAX_ATTEMPTS"] = Read-Value -Name "WORKFLOW_AUTOMATION_MAX_ATTEMPTS" `
        -Default $existing["WORKFLOW_AUTOMATION_MAX_ATTEMPTS"] -AllowEmpty
    $values["WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS"] = Read-Value -Name "WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS" `
        -Default $existing["WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS"] -AllowEmpty
    $values["WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS"] = Read-Value -Name "WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS" `
        -Default $existing["WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS"] -AllowEmpty

    Write-Section "Sonstiges (optional)"
    $values["SWAGGER_ENABLED"] = Read-Value -Name "SWAGGER_ENABLED" `
        -Hint "In Production verboten — Default false belassen" `
        -Default ($existing["SWAGGER_ENABLED"] ?? "false")
    $values["HOST_RUNTIME_HEALTH_ENABLED"] = Read-Value -Name "HOST_RUNTIME_HEALTH_ENABLED" `
        -Hint "Linux-Host/VM-Block im Admin-Dashboard? (true/false)" `
        -Default ($existing["HOST_RUNTIME_HEALTH_ENABLED"] ?? "false")
    $values["HOST_RUNTIME_PROCFS_PATH"] = Read-Value -Name "HOST_RUNTIME_PROCFS_PATH" `
        -Default ($existing["HOST_RUNTIME_PROCFS_PATH"] ?? "/host-proc") `
        -AllowEmpty
    $values["HOST_RUNTIME_ROOT_PATH"] = Read-Value -Name "HOST_RUNTIME_ROOT_PATH" `
        -Default ($existing["HOST_RUNTIME_ROOT_PATH"] ?? "/host-root") `
        -AllowEmpty
    $values["RUNTIME_HEALTH_STORAGE_PATHS"] = Read-Value -Name "RUNTIME_HEALTH_STORAGE_PATHS" `
        -Hint "Label/Pfad-Liste: label1=/p1;label2=/p2 (optional)" `
        -Default $existing["RUNTIME_HEALTH_STORAGE_PATHS"] -AllowEmpty

    # --- Schreiben ---
    Backup-File -Path $envProdPath | Out-Null
    Write-ProdEnvFile -Path $envProdPath -Values $values

    Write-Host ""
    Write-Host ".env.prod geschrieben: $envProdPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Naechste Schritte:" -ForegroundColor Cyan
    Write-Host "  - Stack starten:   ./scripts/start-vm.sh prod   (Linux)  bzw.  .\start.ps1 prod   (Windows-Dev)"
    Write-Host "  - Smoke-Pfad:      curl -k https://<PUBLIC_HOSTNAME>/api/health/ready"
    Write-Host "  - Konfig pruefen:  docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml config"
    Write-Host "  - Drift-Schutz:    pwsh scripts/verify-config-coverage.ps1"
    Write-Host ""
    Write-Host "Worker-Setup auf der Windows-VM:  worker/setup/Configure-Worker.ps1 (kommt mit Slice K4)" -ForegroundColor DarkGray
}

function Write-ProdEnvFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Values
    )
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# ===========================================================================")
    $lines.Add("# Produktive Laufzeitvariablen")
    $lines.Add("# Generiert von scripts/Configure.ps1 am $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))")
    $lines.Add("# Vollstaendige Uebersicht: KauthWorkflow/Betrieb/Konfiguration.md")
    $lines.Add("# ===========================================================================")
    $lines.Add("")

    $sections = @(
        @{ Title = "Grunddaten"; Keys = @("PUBLIC_HOSTNAME", "PUBLIC_BASE_URL") },
        @{ Title = "Web-Container"; Keys = @("WEB_BIND_HOST", "WEB_HTTP_PORT", "WEB_HTTPS_PORT", "WEB_AUTH_MODE") },
        @{ Title = "Datenbank"; Keys = @("POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD") },
        @{ Title = "Entra"; Keys = @("ENTRA_TENANT_ID", "ENTRA_CLIENT_ID", "ENTRA_AUDIENCE", "ENTRA_CLIENT_SECRET", "GRAPH_CLIENT_SECRET") },
        @{ Title = "Vault"; Keys = @("KAUTH_VAULT_KEY") },
        @{ Title = "Directory-Sync"; Keys = @("DIRECTORY_GROUP_PREFIX", "DIRECTORY_EXPLICIT_GROUP_IDS", "DIRECTORY_SYNC_SCHEDULED", "DIRECTORY_SYNC_INTERVAL_MINUTES", "AUTO_PROVISION_DEFAULT_ROLE_KEY") },
        @{ Title = "Automation-Retry (optional, leer = Code-Defaults)"; Keys = @("WORKFLOW_AUTOMATION_MAX_ATTEMPTS", "WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS", "WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS") },
        @{ Title = "Sonstiges"; Keys = @("SWAGGER_ENABLED", "HOST_RUNTIME_HEALTH_ENABLED", "HOST_RUNTIME_PROCFS_PATH", "HOST_RUNTIME_ROOT_PATH", "RUNTIME_HEALTH_STORAGE_PATHS") }
    )

    foreach ($section in $sections) {
        $lines.Add("# --- $($section.Title) ---")
        foreach ($key in $section.Keys) {
            $value = if ($Values.Contains($key)) { $Values[$key] } else { "" }
            $lines.Add("$key=$value")
        }
        $lines.Add("")
    }

    Set-Content -Path $Path -Value $lines -Encoding UTF8
}

# --- Dev-Web-Flow ----------------------------------------------------------

function Invoke-DevWebWizard {
    Write-Host ""
    Write-Host "Konfigurations-Wizard fuer web/.env.local (Frontend-Dev)" -ForegroundColor Green
    Write-Hint "Fuer API-Dev-Werte siehe start.ps1 oder api/API/Properties/launchSettings.json."

    $existing = Read-EnvFile -Path $envWebLocalPath
    if ($existing.Count -gt 0 -and -not $Update) {
        Write-Host ""
        Write-Host "web/.env.local existiert bereits ($($existing.Count) Variablen)." -ForegroundColor Yellow
        $continue = Read-Host "Ueberschreiben? [j/N]"
        if ($continue -notmatch '^[jJyY]$') {
            Write-Host "Abgebrochen." -ForegroundColor Yellow
            return
        }
    }

    $values = [ordered]@{}

    Write-Section "API-Proxy"
    $values["VITE_API_PROXY_TARGET"] = Read-Value -Name "VITE_API_PROXY_TARGET" `
        -Hint "Vite-Dev-Server-Proxy auf die lokale API" `
        -Default ($existing["VITE_API_PROXY_TARGET"] ?? "http://127.0.0.1:5001")

    Write-Section "Auth-Modus"
    $values["VITE_AUTH_MODE"] = Read-Value -Name "VITE_AUTH_MODE" `
        -Hint "dev-sim oder entra" `
        -Default ($existing["VITE_AUTH_MODE"] ?? "dev-sim")

    if ($values["VITE_AUTH_MODE"] -eq "entra") {
        Write-Section "Entra (lokale Tests gegen den echten Redirect-Flow)"
        $values["VITE_ENTRA_CLIENT_ID"] = Read-Value -Name "VITE_ENTRA_CLIENT_ID" -Default $existing["VITE_ENTRA_CLIENT_ID"] -Validator ${function:Test-IsGuid}
        $values["VITE_ENTRA_TENANT_ID"] = Read-Value -Name "VITE_ENTRA_TENANT_ID" -Default $existing["VITE_ENTRA_TENANT_ID"] -Validator ${function:Test-IsGuid}
        $values["VITE_ENTRA_AUDIENCE"] = Read-Value -Name "VITE_ENTRA_AUDIENCE" `
            -Default ($existing["VITE_ENTRA_AUDIENCE"] ?? "api://00000000-0000-0000-0000-000000000000")
        $values["VITE_ENTRA_REDIRECT_URI"] = Read-Value -Name "VITE_ENTRA_REDIRECT_URI" `
            -Hint "https-URL der Redirect-URI" `
            -Default $existing["VITE_ENTRA_REDIRECT_URI"] `
            -Validator ${function:Test-IsHttpsUrl}
    }

    Backup-File -Path $envWebLocalPath | Out-Null
    Write-DevWebEnvFile -Path $envWebLocalPath -Values $values

    Write-Host ""
    Write-Host "web/.env.local geschrieben: $envWebLocalPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Naechste Schritte:" -ForegroundColor Cyan
    Write-Host "  - API starten: dotnet run --project api/API/API.csproj --launch-profile API"
    Write-Host "  - Web starten: cd web; npm install; npm run dev"
}

function Write-DevWebEnvFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Values
    )
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Frontend-Dev — generiert von scripts/Configure.ps1 am $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))")
    foreach ($key in $Values.Keys) {
        $lines.Add("$key=$($Values[$key])")
    }
    Set-Content -Path $Path -Value $lines -Encoding UTF8
}

# --- Show-Modus ------------------------------------------------------------

function Invoke-Show {
    Show-EnvFile -Path $envProdPath -Label ".env.prod"
    Show-EnvFile -Path $envWebLocalPath -Label "web/.env.local"
    Write-Host ""
    Write-Host "Hinweis: Secrets sind redacted; Klartext-Werte stehen in der Datei selbst." -ForegroundColor DarkGray
}

# --- Main ------------------------------------------------------------------

if ($Show) {
    Invoke-Show
    return
}

switch ($Profile) {
    "prod"    { Invoke-ProdWizard }
    "dev-web" { Invoke-DevWebWizard }
}
