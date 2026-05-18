<#
.SYNOPSIS
Drift-Schutz fuer .env.prod.example vs. compose.yml + compose.prod.yml.

.DESCRIPTION
Pro Variable, die in compose.yml oder compose.prod.yml als ${VAR}-Reference
auftaucht, muss ein passender Eintrag in .env.prod.example existieren — entweder
als aktive Zeile (VAR=...) oder als auskommentierter Hint (# VAR=...).

Ausnahmen (technische Hilfsvariablen, die Compose intern setzt):
- Variablen, die nur im Web-Container-`environment:`-Block stehen und vom
  Compose-Default-Pattern abgedeckt sind.
- POSTGRES_*-Defaults sind explizit gewuenscht aktive Zeilen.

Exit-Code:
- 0: alle Variablen abgedeckt.
- 1: mindestens eine Variable fehlt im Example.

.EXAMPLE
.\scripts\verify-config-coverage.ps1
#>

[CmdletBinding()]
param(
    [string]$ExamplePath,
    [string[]]$ComposeFiles,
    [string]$RepoRoot
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

if (-not $ExamplePath) {
    $ExamplePath = Join-Path $RepoRoot ".env.prod.example"
}

if (-not $ComposeFiles -or $ComposeFiles.Count -eq 0) {
    $ComposeFiles = @(
        Join-Path $RepoRoot "compose.yml"
        Join-Path $RepoRoot "compose.prod.yml"
    )
}

if (-not (Test-Path $ExamplePath)) {
    throw "Example-Datei nicht gefunden: $ExamplePath"
}

# Variablen, die NICHT aus .env.prod kommen sondern Compose-intern festgelegt
# sind. Diese werden bewusst aus dem Drift-Check ausgeschlossen.
$Exclusions = @(
    # ASPNETCORE_ENVIRONMENT ist in compose.prod.yml:11 hart auf "Production"
    # gesetzt; in compose.yml mit Dev-Default. Kein .env.prod-Schalter.
    "ASPNETCORE_ENVIRONMENT",
    # API-AUTH_MODE ist in compose.prod.yml:12 hart auf "entra" gesetzt; in
    # compose.yml mit Dev-Default "dev-sim". Kein .env.prod-Schalter. Der Web-
    # Container nutzt WEB_AUTH_MODE als separaten Schalter.
    "AUTH_MODE"
)

# Schritt 1: Variablen aus Compose-Files extrahieren.
$ComposeVars = [System.Collections.Generic.HashSet[string]]::new()
foreach ($file in $ComposeFiles) {
    if (-not (Test-Path $file)) {
        Write-Warning "Compose-Datei nicht gefunden, ueberspringe: $file"
        continue
    }
    $content = Get-Content -Raw -Path $file
    # Match ${VAR_NAME} und ${VAR_NAME:-default} — capture nur den Namen.
    $matches = [regex]::Matches($content, '\$\{([A-Z_][A-Z0-9_]*)(?::-[^}]*)?\}')
    foreach ($m in $matches) {
        $varName = $m.Groups[1].Value
        if ($Exclusions -notcontains $varName) {
            [void]$ComposeVars.Add($varName)
        }
    }
}

# Schritt 2: Variablen aus .env.prod.example extrahieren.
$ExampleVars = [System.Collections.Generic.HashSet[string]]::new()
$exampleLines = Get-Content -Path $ExamplePath
foreach ($line in $exampleLines) {
    # Match "VAR=..." und "# VAR=..." (auskommentierte Hints).
    $match = [regex]::Match($line, '^\s*#?\s*([A-Z_][A-Z0-9_]*)\s*=')
    if ($match.Success) {
        [void]$ExampleVars.Add($match.Groups[1].Value)
    }
}

# Schritt 3: Diff.
$Missing = @()
foreach ($var in $ComposeVars) {
    if (-not $ExampleVars.Contains($var)) {
        $Missing += $var
    }
}

if ($Missing.Count -eq 0) {
    Write-Host "OK — alle $($ComposeVars.Count) Compose-Variablen sind in $ExamplePath dokumentiert." -ForegroundColor Green
    exit 0
}

Write-Host "FAIL — folgende Variablen werden in Compose referenziert, fehlen aber in $ExamplePath`:" -ForegroundColor Red
$Missing | Sort-Object | ForEach-Object { Write-Host "  - $_" }
Write-Host ""
Write-Host "Fuege sie in .env.prod.example hinzu (aktiver Eintrag VAR= oder auskommentierter Hint # VAR=)." -ForegroundColor Yellow
exit 1
