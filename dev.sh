#!/usr/bin/env bash
#
# Prism development environment launcher.
#
# Usage:
#   ./dev.sh              Start everything (Postgres + API + Frontend)
#   ./dev.sh --gpu        Also start vLLM (requires NVIDIA GPU)
#   ./dev.sh --stop       Stop all services
#   ./dev.sh --backend    Only Postgres + API
#   ./dev.sh --frontend   Only frontend dev server

set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
LOGS="$ROOT/logs"
mkdir -p "$LOGS"

RED='\033[0;31m'; GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; NC='\033[0m'
step() { echo -e "\n${CYAN}=> $1${NC}"; }
ok()   { echo -e "   ${GREEN}$1${NC}"; }
warn() { echo -e "   ${YELLOW}$1${NC}"; }

GPU=false; STOP=false; BACKEND_ONLY=false; FRONTEND_ONLY=false

for arg in "$@"; do
  case "$arg" in
    --gpu)      GPU=true ;;
    --stop)     STOP=true ;;
    --backend)  BACKEND_ONLY=true ;;
    --frontend) FRONTEND_ONLY=true ;;
    *) echo "Unknown arg: $arg"; exit 1 ;;
  esac
done

# ── Stop ──────────────────────────────────────────────────────────────
if $STOP; then
  step "Stopping all Prism services..."

  # Kill tracked PIDs
  for pidfile in "$LOGS"/*.pid; do
    [ -f "$pidfile" ] || continue
    pid=$(cat "$pidfile")
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null && ok "Stopped $(basename "$pidfile" .pid) (PID $pid)"
    fi
    rm -f "$pidfile"
  done

  docker compose -f "$ROOT/docker-compose.yml" --profile gpu down 2>/dev/null || true
  ok "Stopped Docker containers"
  exit 0
fi

# ── Preflight ─────────────────────────────────────────────────────────
step "Checking prerequisites..."

# Same discovery the pre-commit hook and the doctor use, so a toolchain that is
# installed but not on this shell's PATH is found rather than reported missing.
# shellcheck disable=SC1091
. "$ROOT/scripts/dev-env.sh"

prism_find_dotnet || {
  echo -e "${RED}No .NET SDK found.${NC}"
  echo "Run ./scripts/doctor.sh — it looks in the usual install locations and says what to do."
  exit 1
}
prism_check_dotnet_runtime

prism_find_node || {
  echo -e "${RED}No Node.js found.${NC}"
  echo "Run ./scripts/doctor.sh — it can load Node through nvm."
  exit 1
}

command -v docker >/dev/null 2>&1 || { warn "Docker not found — Postgres must be running separately"; NO_DOCKER=true; }

if [ -z "${NO_DOCKER:-}" ] && ! docker info >/dev/null 2>&1; then
  warn "Docker is installed but its daemon is not responding."
  warn "Run ./scripts/doctor.sh for the options on this machine."
  NO_DOCKER=true
fi

# ── 1. PostgreSQL ────────────────────────────────────────────────────
if ! $FRONTEND_ONLY && [ -z "${NO_DOCKER:-}" ]; then
  step "Starting PostgreSQL..."
  COMPOSE_ARGS=(-f "$ROOT/docker-compose.yml")
  $GPU && COMPOSE_ARGS+=(--profile gpu)
  COMPOSE_ARGS+=(up -d)

  docker compose "${COMPOSE_ARGS[@]}"

  echo -n "   Waiting for PostgreSQL..."
  ready=false
  for _ in $(seq 1 45); do
    health=$(docker inspect --format='{{.State.Health.Status}}' prism-postgres 2>/dev/null || echo "unknown")
    if [ "$health" = "healthy" ] || _prism_port_open localhost 5438; then
      ready=true; ok " Ready!"; break
    fi
    echo -n "."
    sleep 1
  done
  $ready || warn " Postgres did not report ready. Check:  docker compose logs postgres"
fi

# ── 2. Backend API ───────────────────────────────────────────────────
if ! $FRONTEND_ONLY; then
  step "Starting backend API on http://localhost:5000 ..."

  if [ ! -d "$ROOT/backend/src/Prism.Api/bin" ]; then
    echo "   Building backend (first run)..."
    dotnet build "$ROOT/backend/Prism.sln" --nologo -q
  fi

  dotnet run --project "$ROOT/backend/src/Prism.Api" --no-launch-profile --urls "http://localhost:5000" \
    > "$LOGS/api-stdout.log" 2> "$LOGS/api-stderr.log" &
  echo $! > "$LOGS/api.pid"
  ok "API starting (PID $!)... Swagger: http://localhost:5000/swagger"
fi

# ── 3. Frontend ──────────────────────────────────────────────────────
if ! $BACKEND_ONLY; then
  step "Starting frontend on http://localhost:5173 ..."

  prism_ensure_node_modules || {
    echo -e "${RED}Frontend packages could not be installed.${NC}"
    echo "Run it directly to see why:  cd frontend && npm ci"
    exit 1
  }

  (cd "$ROOT/frontend" && npm run dev) \
    > "$LOGS/frontend-stdout.log" 2> "$LOGS/frontend-stderr.log" &
  echo $! > "$LOGS/frontend.pid"
  ok "Frontend starting (PID $!)..."
fi

# ── Done ─────────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}  Prism is starting up!${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""
# Only advertise what was actually started — listing a frontend that --backend
# deliberately skipped sends people to a port with nothing on it.
$BACKEND_ONLY  || echo "  Frontend:  http://localhost:5173"
$FRONTEND_ONLY || echo "  API:       http://localhost:5000"
$FRONTEND_ONLY || echo "  Swagger:   http://localhost:5000/swagger"
$FRONTEND_ONLY || echo "  Health:    http://localhost:5000/health"
$GPU && echo "  vLLM:      http://localhost:8000"
echo ""
echo -e "  Stop all:  ${CYAN}./dev.sh --stop${NC}"
echo ""
