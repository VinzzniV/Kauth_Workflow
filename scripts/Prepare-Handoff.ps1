param(
    [string]$OutputName = "OnBoarding-handoff.zip"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$handoffRoot = Join-Path $repoRoot "handoff"
$stageRoot = Join-Path $handoffRoot "stage"
$zipPath = Join-Path $handoffRoot $OutputName

$excludeDirectories = @(
    ".git",
    ".claude",
    "handoff",
    "node_modules",
    "web/node_modules",
    "web/.vite",
    "web/dist",
    "api/API/bin",
    "api/API/obj",
    "api/API.Tests/bin",
    "api/API.Tests/obj",
    "TestResults",
    ".vs"
)

$excludeFiles = @(
    ".claude/settings.local.json",
    ".env.prod",
    "web/.env.local"
)

$excludeFileNamePatterns = @(
    "*.log",
    "*.tmp",
    "*.user",
    "*.suo",
    "*.cache",
    "*.coverage",
    "*.trx"
)

$forbiddenPathMatchers = @(
    { param($path) $path -like ".git/*" },
    { param($path) $path -like ".vs/*" },
    { param($path) $path -like "*/node_modules/*" -or $path -like "node_modules/*" },
    { param($path) $path -like "*/dist/*" -or $path -like "dist/*" },
    { param($path) $path -like "*/bin/*" -or $path -like "bin/*" },
    { param($path) $path -like "*/obj/*" -or $path -like "obj/*" },
    { param($path) $path -like "*/TestResults/*" -or $path -like "TestResults/*" },
    { param($path) $path -eq ".env.prod" }
)

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

    $segments = $RelativePath.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $segments) {
        if ($segment -in @("node_modules", "dist", "bin", "obj")) {
            return $true
        }
    }

    foreach ($entry in $excludeDirectories) {
        if ($RelativePath -eq $entry -or $RelativePath.StartsWith("$entry/")) {
            return $true
        }
    }

    return $false
}

function Should-ExcludeFile {
    param([string]$RelativePath)

    $segments = $RelativePath.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $segments) {
        if ($segment -in @("node_modules", "dist", "bin", "obj")) {
            return $true
        }
    }

    foreach ($entry in $excludeFiles) {
        if ($RelativePath -eq $entry -or $RelativePath.StartsWith("$entry/")) {
            return $true
        }
    }

    $fileName = [System.IO.Path]::GetFileName($RelativePath)
    foreach ($pattern in $excludeFileNamePatterns) {
        if ($fileName -like $pattern) {
            return $true
        }
    }

    return $false
}

function Assert-HandoffArchiveClean {
    param([string]$ArchivePath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $violations = @()

        foreach ($entry in $archive.Entries) {
            $normalized = $entry.FullName.Replace("\", "/")
            foreach ($matcher in $forbiddenPathMatchers) {
                if (& $matcher $normalized) {
                    $violations += $normalized
                    break
                }
            }
        }

        if ($violations.Count -gt 0) {
            $uniqueViolations = $violations | Sort-Object -Unique
            throw "Handoff archive contains forbidden entries: $($uniqueViolations -join ', ')"
        }
    }
    finally {
        $archive.Dispose()
    }
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
Assert-HandoffArchiveClean -ArchivePath $zipPath

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

Write-Output "Created handoff zip: $zipPath"
