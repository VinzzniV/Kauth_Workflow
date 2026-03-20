param(
    [string]$OutputName = "OnBoarding-handoff.zip"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$handoffRoot = Join-Path $repoRoot "handoff"
$stageRoot = Join-Path $handoffRoot "stage"
$zipPath = Join-Path $handoffRoot $OutputName

$cleanupTargets = @(
    ".claude/settings.local.json",
    "web/.env.local",
    "web/dist",
    "api/API.Tests/bin",
    "api/API.Tests/obj"
)

$excludeDirectories = @(
    ".git",
    ".claude",
    "handoff",
    "node_modules",
    "web/node_modules",
    "web/dist",
    "api/API/bin",
    "api/API/obj",
    "api/API.Tests/bin",
    "api/API.Tests/obj"
)

$excludeFiles = @(
    ".claude/settings.local.json",
    "web/.env.local"
)

function Remove-TargetIfPresent {
    param([string]$RelativePath)

    $fullPath = Join-Path $repoRoot $RelativePath
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

function Ensure-ParentDirectory {
    param([string]$FilePath)

    $parent = Split-Path -Parent $FilePath
    if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
}

function Get-RelativePathNormalized {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseUri = New-Object System.Uri(($BasePath.TrimEnd('\') + '\'))
    $targetUri = New-Object System.Uri($TargetPath)

    return $baseUri.MakeRelativeUri($targetUri).ToString().Replace("\", "/")
}

function Should-ExcludeDirectory {
    param([string]$RelativePath)

    foreach ($entry in $excludeDirectories) {
        if ($RelativePath -eq $entry -or $RelativePath.StartsWith("$entry/")) {
            return $true
        }
    }

    return $false
}

function Should-ExcludeFile {
    param([string]$RelativePath)

    foreach ($entry in $excludeFiles) {
        if ($RelativePath -eq $entry -or $RelativePath.StartsWith("$entry/")) {
            return $true
        }
    }

    return $false
}

foreach ($target in $cleanupTargets) {
    Remove-TargetIfPresent -RelativePath $target
}

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

$allDirectories = Get-ChildItem -LiteralPath $repoRoot -Directory -Recurse -Force |
    Sort-Object { $_.FullName.Length }

foreach ($directory in $allDirectories) {
    $relativePath = Get-RelativePathNormalized -BasePath $repoRoot -TargetPath $directory.FullName

    if (Should-ExcludeDirectory -RelativePath $relativePath) {
        continue
    }

    $destination = Join-Path $stageRoot $relativePath
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
}

$allFiles = Get-ChildItem -LiteralPath $repoRoot -File -Recurse -Force

foreach ($file in $allFiles) {
    $relativePath = Get-RelativePathNormalized -BasePath $repoRoot -TargetPath $file.FullName

    if (Should-ExcludeFile -RelativePath $relativePath) {
        continue
    }

    $parentDirectory = Split-Path -Parent $relativePath
    if (-not [string]::IsNullOrWhiteSpace($parentDirectory) -and (Should-ExcludeDirectory -RelativePath $parentDirectory.Replace("\", "/"))) {
        continue
    }

    $destination = Join-Path $stageRoot $relativePath
    Ensure-ParentDirectory -FilePath $destination
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

Write-Output "Created handoff zip: $zipPath"
