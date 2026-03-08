<#
.SYNOPSIS
    Prism development environment launcher.
.DESCRIPTION
    Starts PostgreSQL (via Docker), the .NET backend API, and the React frontend dev server.
.PARAMETER Stop
    Stops all running services.
.PARAMETER Gpu
    Also starts the vLLM inference server (requires NVIDIA GPU + Docker GPU support).
.PARAMETER BackendOnly
    Only starts PostgreSQL and the backend API (no frontend).
.PARAMETER FrontendOnly
    Only starts the frontend dev server (assumes backend is already running).
.EXAMPLE
    .\dev.ps1              # Start everything (Postgres + API + Frontend)
    .\dev.ps1 -Gpu         # Start everything including vLLM
    .\dev.ps1 -Stop        # Stop all services
    .\dev.ps1 -BackendOnly # Just Postgres + API
#>

param(
    [switch]$Stop,
    [switch]$Gpu,
    [switch]$BackendOnly,
    [switch]$FrontendOnly
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

function Write-Step($msg) { Write-Host "`n=> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "   $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "   $msg" -ForegroundColor Yellow }

# ── Stop ──────────────────────────────────────────────────────────────
if ($Stop) {
    Write-Step "Stopping all Prism services..."

    # Kill background jobs from this session
    Get-Job -Name "prism-*" -ErrorAction SilentlyContinue | Stop-Job -PassThru | Remove-Job

    # Stop any dotnet/node processes on our ports
    $apiProc = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
    $feProc  = Get-NetTCPConnection -LocalPort 5173 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique

    if ($apiProc) { Stop-Process -Id $apiProc -Force -ErrorAction SilentlyContinue; Write-Ok "Stopped backend API" }
    if ($feProc)  { Stop-Process -Id $feProc -Force -ErrorAction SilentlyContinue; Write-Ok "Stopped frontend dev server" }

    docker compose -f "$Root\docker-compose.yml" --profile gpu down 2>$null
    Write-Ok "Stopped Docker containers"
    exit 0
}

# ── Preflight checks ─────────────────────────────────────────────────
Write-Step "Checking prerequisites..."

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Warn "Docker not found. PostgreSQL must be running separately."
    $noDocker = $true
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI not found. Install .NET 9 SDK from https://dot.net"
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js not found. Install from https://nodejs.org"
}

# ── 1. PostgreSQL ────────────────────────────────────────────────────
if (-not $FrontendOnly) {
    if (-not $noDocker) {
        Write-Step "Starting PostgreSQL..."
        $composeArgs = @("-f", "$Root\docker-compose.yml")
        if ($Gpu) { $composeArgs += "--profile", "gpu" }
        $composeArgs += "up", "-d"

        & docker compose @composeArgs
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed" }

        # Wait for Postgres to be healthy
        Write-Host "   Waiting for PostgreSQL..." -NoNewline
        for ($i = 0; $i -lt 30; $i++) {
            $health = docker inspect --format='{{.State.Health.Status}}' prism-postgres 2>$null
            if ($health -eq "healthy") { Write-Ok " Ready!"; break }
            Write-Host "." -NoNewline
            Start-Sleep -Seconds 1
        }
        if ($health -ne "healthy") { Write-Warn " PostgreSQL not healthy yet, continuing anyway..." }
    }

    # ── 2. Backend API ───────────────────────────────────────────────
    Write-Step "Starting backend API on http://localhost:5000 ..."

    # Install deps if needed
    if (-not (Test-Path "$Root\backend\src\Prism.Api\bin")) {
        Write-Host "   Building backend (first run)..."
        & dotnet build "$Root\backend\Prism.sln" --nologo -q
    }

    Start-Process -NoNewWindow -FilePath "dotnet" `
        -ArgumentList "run", "--project", "$Root\backend\src\Prism.Api", "--no-launch-profile", "--urls", "http://localhost:5000" `
        -RedirectStandardError "$Root\logs\api-stderr.log"

    Write-Ok "API starting... (Swagger: http://localhost:5000/swagger)"
}

# ── 3. Frontend ──────────────────────────────────────────────────────
if (-not $BackendOnly) {
    Write-Step "Starting frontend on http://localhost:5173 ..."

    if (-not (Test-Path "$Root\frontend\node_modules")) {
        Write-Host "   Installing npm packages (first run)..."
        Push-Location "$Root\frontend"
        & npm install
        Pop-Location
    }

    Start-Process -NoNewWindow -FilePath "npm" `
        -ArgumentList "run", "dev" `
        -WorkingDirectory "$Root\frontend"

    Write-Ok "Frontend starting... (http://localhost:5173)"
}

# ── Done ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Prism is starting up!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Frontend:  http://localhost:5173"
Write-Host "  API:       http://localhost:5000"
Write-Host "  Swagger:   http://localhost:5000/swagger"
Write-Host "  Health:    http://localhost:5000/health"
if ($Gpu) {
Write-Host "  vLLM:      http://localhost:8000"
}
Write-Host ""
Write-Host "  Stop all:  .\dev.ps1 -Stop" -ForegroundColor DarkGray
Write-Host ""
