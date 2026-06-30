[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$FollowLogs,
    [int]$HealthTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

$backendDir = (Resolve-Path $PSScriptRoot).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$envFile = Join-Path $backendDir ".env"
$envTemplate = Join-Path $backendDir ".env.example"
$healthUrl = "http://localhost:5163/api/health"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker Desktop / docker CLI is required to run the backend in a container."
}

if (-not (Test-Path $envFile)) {
    if (-not (Test-Path $envTemplate)) {
        throw "Missing backend .env and .env.example. Cannot prepare container configuration."
    }

    Copy-Item -LiteralPath $envTemplate -Destination $envFile
    Write-Host "Created backend .env from .env.example for Docker Compose."
}

$composeArgs = @("compose", "up")
if (-not $NoBuild) {
    $composeArgs += "--build"
}
$composeArgs += "-d", "mongo", "api"

Push-Location $repoRoot
try {
    & docker @composeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker $($composeArgs -join ' ') failed."
    }

    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    do {
        try {
            $response = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 5
            if ($response.success) {
                Write-Host "API is healthy at $healthUrl"
                if ($FollowLogs) {
                    & docker compose logs -f api
                }
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    Write-Warning "API did not become healthy within $HealthTimeoutSeconds seconds. Recent API logs:"
    & docker compose logs --tail 50 api
    exit 1
}
finally {
    Pop-Location
}
