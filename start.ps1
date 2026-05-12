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

function Test-TcpPortUsable {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function Get-AvailableDevWebPort {
    $preferredPorts = @(8080, 8081, 4174, 4175, 9000, 9001)

    foreach ($port in $preferredPorts) {
        if (-not (Test-TcpPortInUse -Port $port) -and (Test-TcpPortUsable -Port $port)) {
            return $port
        }
    }

    throw "Es konnte kein freier Web-Port aus der bevorzugten Liste ($($preferredPorts -join ', ')) gefunden werden."
}

function Get-AvailableDevDbPort {
    $preferredPorts = @(26432, 35432, 35433, 36432, 36433, 45432, 45433)

    foreach ($port in $preferredPorts) {
        if (-not (Test-TcpPortInUse -Port $port) -and (Test-TcpPortUsable -Port $port)) {
            return $port
        }
    }

    throw "Es konnte kein freier Dev-DB-Port aus der bevorzugten Liste ($($preferredPorts -join ', ')) gefunden werden."
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

function Get-ComposeDbHostPort {
    $mappedPort = & docker compose -f compose.yml -f compose.dev-db.yml port db 5432 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mappedPort)) {
        return $null
    }

    $trimmed = $mappedPort.Trim()
    $portText = $trimmed.Substring($trimmed.LastIndexOf(":") + 1)
    $parsedPort = 0
    if ([int]::TryParse($portText, [ref]$parsedPort)) {
        return $parsedPort
    }

    return $null
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
    $containerId = Get-ComposeDbContainerId

    if ($containerId) {
        $state = Get-DockerContainerState -ContainerId $containerId
        $mappedPort = Get-ComposeDbHostPort

        if ($state.Status -eq "running" -and $state.Health -eq "healthy") {
            Write-Host "Dev-Datenbank laeuft bereits und ist healthy."
            return $mappedPort
        }

        if ($state.Status -eq "running") {
            Write-Host "Dev-Datenbank laeuft bereits, ist aber noch nicht healthy. Warte auf Bereitschaft ..."
            Wait-ForDevDatabase
            return (Get-ComposeDbHostPort)
        }
    }

    $dbPort = Get-AvailableDevDbPort
    $previousDevDbPort = $env:DEV_DB_PORT
    $env:DEV_DB_PORT = $dbPort.ToString()

    if ($dbPort -ne 26432) {
        Write-Warning "Dev-DB-Port 26432 ist auf diesem Host nicht nutzbar. Verwende stattdessen Port $dbPort."
    }

    Write-Host "Starte Dev-Datenbank ueber Docker Compose ..."
    try {
        Invoke-ExternalCommand -FilePath "docker" -Arguments @("compose", "-f", "compose.yml", "-f", "compose.dev-db.yml", "up", "-d", "db") -ActionDescription "Dev-Datenbankstart"
    }
    finally {
        if ($null -eq $previousDevDbPort) {
            Remove-Item Env:DEV_DB_PORT -ErrorAction SilentlyContinue
        }
        else {
            $env:DEV_DB_PORT = $previousDevDbPort
        }
    }

    Write-Host "Warte auf DB-Bereitschaft ..."
    Wait-ForDevDatabase
    return (Get-ComposeDbHostPort)
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

    $dbPort = Ensure-DevDatabaseReady

    $webEnvLocal = Join-Path $repoRoot "web/.env.local"
    if (-not (Test-Path $webEnvLocal)) {
        Write-Warning "web/.env.local fehlt. Vite kann ohne diese Datei falsch konfiguriert sein."
    }

    $webPort = Get-AvailableDevWebPort
    $apiCommand = "Set-Location '$repoRoot'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='http://0.0.0.0:5001'; `$env:AUTH_MODE='dev-sim'; `$env:ConnectionStrings__Default='Host=localhost;Port=$dbPort;Username=app;Password=app_pw;Database=appdb;GSS Encryption Mode=Disable;SSL Mode=Disable'; `$env:PUBLIC_BASE_URL='http://localhost:$webPort'; `$env:Cors__AllowedOrigins__0='http://localhost:$webPort'; `$env:NotificationEmail__FrontendBaseUrl='http://localhost:$webPort'; `$env:DIRECTORY_GROUP_PREFIX='Onboarding-App-'; `$env:DIRECTORY_SYNC_SCHEDULED='true'; `$env:SWAGGER_ENABLED='true'; dotnet run --project api/API/API.csproj --no-launch-profile"
    $webCommand = "Set-Location '$repoRoot/web'; `$env:VITE_PORT='$webPort'; npm run dev -- --host 127.0.0.1 --port $webPort"

    Write-Host "Oeffne API-Fenster ..."
    Start-PowershellWindow -WindowTitle "kauth_workflow API" -Command $apiCommand

    Write-Host "Oeffne Web-Fenster ..."
    Start-PowershellWindow -WindowTitle "kauth_workflow Web" -Command $webCommand

    Write-Host ""
    Write-Host "Dev-Start angestossen."
    Write-Host "API: http://127.0.0.1:5001"
    Write-Host "Web: http://127.0.0.1:$webPort"
    Write-Host "DB:  localhost:$dbPort"

    if ($dbPort -ne 26432) {
        Write-Warning "DB-gebundene Tests erwarten standardmaessig Port 26432. Fuer Tests auf Port $dbPort setze ONBOARDING_TEST_CONNECTION_STRING entsprechend."
    }
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
