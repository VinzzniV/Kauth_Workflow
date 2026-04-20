[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("dev", "prod")]
    [string]$Mode
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

function Fail {
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [int]$ExitCode = 1
    )

    Write-Error $Message
    exit $ExitCode
}

function Test-CommandExists {
    param([Parameter(Mandatory = $true)][string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "$Description wurde nicht gefunden: $Path"
    }
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$ActionDescription
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$ActionDescription ist fehlgeschlagen (Exit-Code $LASTEXITCODE)."
    }
}

function Test-TcpPortInUse {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    return $listeners.Port -contains $Port
}

function Get-ComposeDbContainerId {
    $containerId = & docker compose -f compose.yml -f compose.dev-db.yml ps -q db
    if ($LASTEXITCODE -ne 0) {
        throw "DB-Container konnte nicht ueber Docker Compose abgefragt werden."
    }

    if ($null -eq $containerId) {
        return ""
    }

    return $containerId.Trim()
}

function Get-DockerContainerState {
    param([Parameter(Mandatory = $true)][string]$ContainerId)

    $state = & docker inspect --format "{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}" $ContainerId
    if ($LASTEXITCODE -ne 0) {
        throw "Docker-Status fuer Container $ContainerId konnte nicht gelesen werden."
    }

    $parts = $state.Trim().Split("|")
    return @{
        Status = $parts[0]
        Health = $parts[1]
    }
}

function Wait-ForDevDatabase {
    param([int]$TimeoutSeconds = 90)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        $containerId = Get-ComposeDbContainerId
        if (-not $containerId) {
            Start-Sleep -Seconds 2
            continue
        }

        $state = Get-DockerContainerState -ContainerId $containerId
        Write-Host "DB-Status: $($state.Status), Health: $($state.Health)"

        if ($state.Status -eq "running" -and $state.Health -eq "healthy") {
            return
        }

        if ($state.Status -in @("exited", "dead")) {
            throw "DB-Container ist nicht lauffaehig (Status: $($state.Status))."
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "DB wurde innerhalb von $TimeoutSeconds Sekunden nicht healthy."
}

function Ensure-DevDatabaseReady {
    $dbPort = 26432
    $containerId = Get-ComposeDbContainerId

    if ($containerId) {
        $state = Get-DockerContainerState -ContainerId $containerId

        if ($state.Status -eq "running" -and $state.Health -eq "healthy") {
            Write-Host "Dev-Datenbank laeuft bereits und ist healthy."
            return
        }

        if ($state.Status -eq "running") {
            Write-Host "Dev-Datenbank laeuft bereits, ist aber noch nicht healthy. Warte auf Bereitschaft ..."
            Wait-ForDevDatabase
            return
        }
    }

    if (Test-TcpPortInUse -Port $dbPort) {
        throw "Der Dev-DB-Port $dbPort ist bereits belegt. Beende den anderen Prozess oder passe das Port-Mapping in compose.dev-db.yml an."
    }

    Write-Host "Starte Dev-Datenbank ueber Docker Compose ..."
    Invoke-ExternalCommand -FilePath "docker" -Arguments @("compose", "-f", "compose.yml", "-f", "compose.dev-db.yml", "up", "-d", "db") -ActionDescription "Dev-Datenbankstart"

    Write-Host "Warte auf DB-Bereitschaft ..."
    Wait-ForDevDatabase
}

function Start-PowershellWindow {
    param(
        [Parameter(Mandatory = $true)][string]$WindowTitle,
        [Parameter(Mandatory = $true)][string]$Command
    )

    $process = Start-Process powershell `
        -ArgumentList "-NoExit", "-Command", "& { `$Host.UI.RawUI.WindowTitle = '$WindowTitle'; $Command }" `
        -PassThru

    if (-not $process) {
        throw "PowerShell-Fenster '$WindowTitle' konnte nicht gestartet werden."
    }
}

function Start-DevEnvironment {
    if (-not (Test-CommandExists "docker")) {
        throw "docker wurde nicht gefunden."
    }

    if (-not (Test-CommandExists "dotnet")) {
        throw "dotnet wurde nicht gefunden."
    }

    if (-not (Test-CommandExists "npm")) {
        throw "npm wurde nicht gefunden."
    }

    Assert-PathExists -Path (Join-Path $repoRoot "compose.yml") -Description "compose.yml"
    Assert-PathExists -Path (Join-Path $repoRoot "compose.dev-db.yml") -Description "compose.dev-db.yml"
    Assert-PathExists -Path (Join-Path $repoRoot "api/API/API.csproj") -Description "API-Projekt"
    Assert-PathExists -Path (Join-Path $repoRoot "web/package.json") -Description "web/package.json"

    Ensure-DevDatabaseReady

    $webEnvLocal = Join-Path $repoRoot "web/.env.local"
    if (-not (Test-Path $webEnvLocal)) {
        Write-Warning "web/.env.local fehlt. Vite kann ohne diese Datei falsch konfiguriert sein."
    }

    $apiCommand = "Set-Location '$repoRoot'; dotnet run --project api/API/API.csproj --launch-profile API"
    $webCommand = "Set-Location '$repoRoot/web'; npm run dev"

    Write-Host "Oeffne API-Fenster ..."
    Start-PowershellWindow -WindowTitle "kauth_workflow API" -Command $apiCommand

    Write-Host "Oeffne Web-Fenster ..."
    Start-PowershellWindow -WindowTitle "kauth_workflow Web" -Command $webCommand

    Write-Host ""
    Write-Host "Dev-Start angestossen."
    Write-Host "API: http://127.0.0.1:5001"
    Write-Host "Web: http://127.0.0.1:5173"
    Write-Host "DB:  localhost:26432"
}

function Start-ProdEnvironment {
    if (-not (Test-CommandExists "docker")) {
        throw "docker wurde nicht gefunden."
    }

    Assert-PathExists -Path (Join-Path $repoRoot "compose.yml") -Description "compose.yml"
    Assert-PathExists -Path (Join-Path $repoRoot "compose.prod.yml") -Description "compose.prod.yml"

    $envFile = Join-Path $repoRoot ".env.prod"
    if (-not (Test-Path $envFile)) {
        throw ".env.prod wurde nicht gefunden. Lege die Datei zuerst an, z. B. auf Basis von .env.prod.example."
    }

    Write-Host "Starte produktiven Compose-Stack ..."
    Invoke-ExternalCommand -FilePath "docker" -Arguments @("compose", "--env-file", ".env.prod", "-f", "compose.yml", "-f", "compose.prod.yml", "up", "-d", "--build") -ActionDescription "Prod-Stack-Start"

    Write-Host ""
    Write-Host "Prod-Start angestossen."
    Write-Host "Status: docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps"
    Write-Host "Logs:   docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f"
}

if (-not $Mode) {
    $selection = Read-Host "Welchen Modus starten? [dev/prod]"
    $Mode = $selection.Trim().ToLowerInvariant()
}

switch ($Mode) {
    "dev" {
        try {
            Start-DevEnvironment
        }
        catch {
            Fail -Message "Dev-Start fehlgeschlagen: $($_.Exception.Message)"
        }
    }
    "prod" {
        try {
            Start-ProdEnvironment
        }
        catch {
            Fail -Message "Prod-Start fehlgeschlagen: $($_.Exception.Message)"
        }
    }
    default { throw "Ungueltiger Modus '$Mode'. Erlaubt sind: dev, prod." }
}
