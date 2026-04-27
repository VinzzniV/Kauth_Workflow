<#
.SYNOPSIS
    Erstellt ein Handoff-ZIP des Repos fuer ein Production-Deployment.

.DESCRIPTION
    Bundelt alle git-getrackten Dateien in handoff/kauth_workflow-<timestamp>.zip.
    Damit landen automatisch keine Eintraege aus .gitignore im ZIP
    (kein .git, kein node_modules, dist, bin, obj, .env.prod, web/.env.local, *.log).

    Lokale Modifikationen an getrackten Dateien werden mit eingepackt — ein
    git status -Hinweis warnt, falls das nicht erwuenscht ist.

.PARAMETER OutputDir
    Zielverzeichnis fuer das ZIP. Default: "handoff" (relativ zum Repo-Root).

.PARAMETER NoZip
    Nur in einem Stage-Ordner sammeln, nicht packen.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Prepare-Handoff.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Prepare-Handoff.ps1 -NoZip
#>

[CmdletBinding()]
param(
    [string]$OutputDir = "handoff",
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Push-Location $repoRoot
try {
    & git rev-parse --git-dir *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Prepare-Handoff muss aus einem git-Repo heraus laufen."
    }

    $dirty = & git status --porcelain
    if ($dirty) {
        Write-Warning "Working tree hat lokale Aenderungen. Diese werden mit eingepackt."
        Write-Warning "Falls das nicht gewuenscht ist: vorher committen oder verwerfen."
    }

    # -c core.quotePath=false: Pfade mit Umlauten / Sonderzeichen unverstuemmelt ausgeben
    $allFiles = & git -c core.quotePath=false ls-files
    if ($LASTEXITCODE -ne 0 -or -not $allFiles) {
        throw "git ls-files lieferte keine Dateien."
    }

    # Build-Outputs, Cache-Verzeichnisse und KI-Tooling-Configs, die zwar getrackt
    # sind, aber nicht ins Handoff gehoeren.
    $excludePrefixes = @(
        'artifacts/',
        '.tmp-build/',
        '.tmp-test-obj/',
        '.tmp-vm-dev/',
        'handoff/',
        '.claude/',
        '.codex/',
        '.github/'
    )

    $files = $allFiles | Where-Object {
        $path = $_
        -not ($excludePrefixes | Where-Object { $path -like "$_*" })
    }

    $skipped = $allFiles.Count - $files.Count
    if ($skipped -gt 0) {
        Write-Host "Filter: $skipped getrackte Build-/Cache-Dateien werden uebersprungen."
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $bundleName = "kauth_workflow-$timestamp"
    $outputRoot = Join-Path $repoRoot $OutputDir
    $stagingDir = Join-Path $outputRoot $bundleName

    if (-not (Test-Path $outputRoot)) {
        New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
    }
    if (Test-Path $stagingDir) {
        Remove-Item -Recurse -Force $stagingDir
    }
    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

    Write-Host "Sammle $($files.Count) Dateien nach $stagingDir ..."

    foreach ($rel in $files) {
        $src = Join-Path $repoRoot $rel
        if (-not (Test-Path -LiteralPath $src)) {
            Write-Warning "Datei fehlt im Working Tree (uebersprungen): $rel"
            continue
        }
        $dst = Join-Path $stagingDir $rel
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path -LiteralPath $dstDir)) {
            New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
        }
        Copy-Item -LiteralPath $src -Destination $dst
    }

    if ($NoZip) {
        Write-Host "Handoff-Stage fertig: $stagingDir"
        return
    }

    $zipPath = Join-Path $outputRoot "$bundleName.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

    Write-Host "Komprimiere zu $zipPath ..."
    Compress-Archive -Path (Join-Path $stagingDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

    Remove-Item -Recurse -Force $stagingDir

    $sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host ""
    Write-Host "Handoff-ZIP fertig:" -ForegroundColor Green
    Write-Host "  Pfad   : $zipPath"
    Write-Host "  Groesse: $sizeMB MB"
    Write-Host "  Inhalt : $($files.Count) Dateien (alle git-getrackt)"
    Write-Host ""
    Write-Host "Naechste Schritte siehe KauthWorkflow/Betrieb/Deployment-Checkliste.md"
}
finally {
    Pop-Location
}
