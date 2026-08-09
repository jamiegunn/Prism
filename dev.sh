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
API_PORT="${PRISM_API_PORT:-5000}"

if ! $FRONTEND_ONLY; then
  # Port 5000 is not as free as it looks. On macOS the AirPlay Receiver holds it
  # by default, and any other stopped-then-restarted service may still have it.
  # Kestrel's failure is a bind error buried in a log the launcher used to
  # ignore, so find out here instead.
  if _prism_port_open localhost "$API_PORT"; then
    holder="$(command -v lsof >/dev/null 2>&1 && lsof -nP -iTCP:"$API_PORT" -sTCP:LISTEN 2>/dev/null | awk 'NR==2 {print $1}')"
    warn "Port $API_PORT is already in use${holder:+ by '$holder'}."

    if [ "$(uname -s)" = "Darwin" ] && [ "$API_PORT" = "5000" ]; then
      warn "On macOS this is usually AirPlay Receiver. To free it:"
      warn "  System Settings -> General -> AirDrop & Handoff -> AirPlay Receiver: Off"
    fi

    for candidate in 5001 5002 5003 5004 5005; do
      if ! _prism_port_open localhost "$candidate"; then
        API_PORT="$candidate"
        break
      fi
    done

    if _prism_port_open localhost "$API_PORT"; then
      echo -e "${RED}No free port found between 5000 and 5005. Set PRISM_API_PORT.${NC}"
      exit 1
    fi
    ok "Using port $API_PORT instead."
  fi

  # The Vite dev server proxies /api to this, so it has to be told.
  export PRISM_API_URL="http://localhost:$API_PORT"

  step "Starting backend API on $PRISM_API_URL ..."

  if [ ! -d "$ROOT/backend/src/Prism.Api/bin" ]; then
    echo "   Building backend (first run)..."
    dotnet build "$ROOT/backend/Prism.sln" --nologo -q
  fi

  # --no-launch-profile keeps launchSettings.json from overriding the URL, but it also
  # drops ASPNETCORE_ENVIRONMENT to Production. Migrations, seeding, Swagger and the DI
  # scope validation are all gated on Development, so without this the API starts, applies
  # no migrations, and the first request fails with 'relation "jobs" does not exist'.
  export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

  dotnet run --project "$ROOT/backend/src/Prism.Api" --no-launch-profile --urls "$PRISM_API_URL" \
    > "$LOGS/api-stdout.log" 2> "$LOGS/api-stderr.log" &
  API_PID=$!
  echo $API_PID > "$LOGS/api.pid"

  # Wait for it to actually answer. Printing "API starting (PID 71848)" and
  # exiting 0 while Kestrel died two seconds later is how a bind failure turns
  # into "Backend unreachable" in the UI with no clue where to look.
  echo -n "   Waiting for the API..."
  api_up=false
  for _ in $(seq 1 60); do
    if ! kill -0 "$API_PID" 2>/dev/null; then
      break                       # process gone; no point waiting out the clock
    fi
    if curl -fsS "$PRISM_API_URL/health" >/dev/null 2>&1; then
      api_up=true; break
    fi
    echo -n "."
    sleep 1
  done

  if $api_up; then
    ok " Ready! Swagger: $PRISM_API_URL/swagger"
  else
    echo ""
    echo -e "${RED}The API did not come up.${NC}"
    echo ""
    # Look in both streams. A .NET exception is logged to stdout by Serilog,
    # but the SDK's own failures — a missing project, an unrestorable package —
    # only ever reach stderr, which is how this printed nothing at all the first
    # time it was tried.
    shown=false
    for logfile in "$LOGS/api-stderr.log" "$LOGS/api-stdout.log"; do
      [ -s "$logfile" ] || continue
      echo "From $(basename "$logfile"):"
      if grep -m1 -A4 -iE "error|exception|fail|denied" "$logfile" 2>/dev/null | grep -q .; then
        grep -m1 -A4 -iE "error|exception|fail|denied" "$logfile" 2>/dev/null | cut -c1-160 | sed 's/^/    /'
      else
        tail -8 "$logfile" 2>/dev/null | cut -c1-160 | sed 's/^/    /'
      fi
      echo ""
      shown=true
    done
    $shown || echo "    (both logs are empty — the process died before writing anything)"

    echo "Full logs: $LOGS/api-stdout.log"
    echo "           $LOGS/api-stderr.log"
    echo "Diagnose:  ./scripts/doctor.sh"
    kill "$API_PID" 2>/dev/null || true
    rm -f "$LOGS/api.pid"
    exit 1
  fi
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
$FRONTEND_ONLY || echo "  API:       http://localhost:$API_PORT"
$FRONTEND_ONLY || echo "  Swagger:   http://localhost:$API_PORT/swagger"
$FRONTEND_ONLY || echo "  Health:    http://localhost:$API_PORT/health"
$GPU && echo "  vLLM:      http://localhost:8000"
echo ""
echo -e "  Stop all:  ${CYAN}./dev.sh --stop${NC}"
echo ""
