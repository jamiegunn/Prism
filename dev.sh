#!/usr/bin/env bash
#
# Prism development environment launcher.
#
# Asks a few questions each run and remembers the answers in .prism-dev.conf, so
# the next run's defaults are what you chose last time — hold Enter to repeat a
# setup, or change one answer without remembering any flags.
#
# Usage:
#   ./dev.sh                Ask, then start. Enter accepts the previous answer.
#   ./dev.sh --yes          Skip the questions, reuse the previous answers
#   ./dev.sh --reconfigure  Ask, ignoring the previous answers — useful when the
#                           saved ones came from a different machine
#   ./dev.sh --stop         Stop all services
#
#   ./dev.sh --backend      Only Postgres + API     ) explicit flags win over
#   ./dev.sh --frontend     Only frontend dev server ) the saved answers
#   ./dev.sh --gpu          Also start vLLM (needs an NVIDIA GPU)
#
# It never prompts when stdin is not a terminal, so CI and scripts get the
# defaults rather than hanging on a question nobody is there to answer.

set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
LOGS="$ROOT/logs"
mkdir -p "$LOGS"

RED='\033[0;31m'; GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; NC='\033[0m'
step() { echo -e "\n${CYAN}=> $1${NC}"; }
ok()   { echo -e "   ${GREEN}$1${NC}"; }
warn() { echo -e "   ${YELLOW}$1${NC}"; }

GPU=false; STOP=false; BACKEND_ONLY=false; FRONTEND_ONLY=false
RECONFIGURE=false; ASSUME_YES=false
SCOPE_FROM_FLAG=false

for arg in "$@"; do
  case "$arg" in
    --gpu)         GPU=true ;;
    --stop)        STOP=true ;;
    --backend)     BACKEND_ONLY=true;  SCOPE_FROM_FLAG=true ;;
    --frontend)    FRONTEND_ONLY=true; SCOPE_FROM_FLAG=true ;;
    --reconfigure) RECONFIGURE=true ;;
    --yes|-y)      ASSUME_YES=true ;;
    --help|-h)     sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown arg: $arg"; echo "Try: $0 --help"; exit 1 ;;
  esac
done

CONFIG="$ROOT/.prism-dev.conf"

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

  docker compose -f "$ROOT/docker-compose.yml" --profile gpu --profile ollama down 2>/dev/null || true
  ok "Stopped Docker containers"
  exit 0
fi

# The local inference servers worth looking for, as port|id|type|endpoint|label.
#
# Probing a port beats checking PATH. A binary on PATH may not be running, a
# running server may not be the binary you found, and on Windows the launcher
# runs under Git Bash or WSL where a native install is not on PATH at all while
# its port is perfectly reachable. Asking the port "who are you" answers the
# question that actually matters on every platform.
#
# Kept in step with DiscoverProvidersHandler.Candidates; ProviderCandidateParity
# tests fail if the two lists drift apart.
PRISM_PROVIDER_CANDIDATES=(
  "11434|detected-ollama|Ollama|http://localhost:11434|Ollama"
  "8000|detected-vllm|Vllm|http://localhost:8000|vLLM"
  "1234|detected-lmstudio|LmStudio|http://localhost:1234/v1|LM Studio"
)

# True when version $1 is at least $2. Compared field by field rather than with
# sort -V, which BSD and GNU sort do not agree about.
prism_version_at_least() {
  local have="$1" want="$2"
  local h1 h2 h3 w1 w2 w3

  IFS=. read -r h1 h2 h3 <<< "$have"
  IFS=. read -r w1 w2 w3 <<< "$want"

  h1=${h1:-0}; h2=${h2:-0}; h3=${h3:-0}
  w1=${w1:-0}; w2=${w2:-0}; w3=${w3:-0}

  [ "$h1" -ne "$w1" ] && { [ "$h1" -gt "$w1" ]; return; }
  [ "$h2" -ne "$w2" ] && { [ "$h2" -gt "$w2" ]; return; }
  [ "$h3" -ge "$w3" ]
}

# Pulls a model when the server has none, because a running Ollama with an
# empty model list produces exactly the same empty screens as no Ollama at all.
# The prefix lets the same logic drive the host binary and the container.
ensure_ollama_model() {
  local prefix="$1" model="$2"

  if ${prefix}ollama list 2>/dev/null | tail -n +2 | grep -qE '\S'; then
    ok "A model is already available."
    return 0
  fi

  [ -n "$model" ] || model="mistral:7b-instruct"

  step "Pulling $model (first run only — this downloads a few GB)..."
  if ${prefix}ollama pull "$model"; then
    ok "$model is ready."
  else
    warn "Could not pull $model. Do it yourself with:  ${prefix}ollama pull $model"
  fi

  # Ollama gained logprobs in 0.12.11. Before that Prism told everyone here that
  # token-level views needed vLLM — which on an Apple Silicon Mac was advice to
  # run something that cannot run there at all.
  # `|| true` is load-bearing: with `set -o pipefail`, a grep that matches nothing
  # fails the whole pipeline and would take the launcher down with it.
  local version
  version="$(${prefix}ollama --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || true)"

  if [ -n "$version" ] && ! prism_version_at_least "$version" 0.12.11; then
    warn "This Ollama is $version, which predates per-token probabilities (added in 0.12.11),"
    warn "so the heatmap, entropy view and Token Explorer stay empty. Updating turns them on."
  fi
}

# Whether the Ollama container Prism starts is the thing on port 11434. This is
# a different question from "is port 11434 open", and the difference is the whole
# bug: a container and a native server answer identically there, only one of them
# can hold the port, and mistaking one for the other means measuring the
# container twice while believing you switched to Metal.
_prism_ollama_container_running() {
  command -v docker >/dev/null 2>&1 || return 1
  docker ps --filter 'name=prism-ollama' --filter 'status=running' \
            --format '{{.Names}}' 2>/dev/null | grep -q prism-ollama
}

# ── Configuration ─────────────────────────────────────────────────────
#
# Asked once, remembered in .prism-dev.conf, and skipped entirely when there is
# nobody to answer. Every question has a recommended default on the first
# option, so holding Enter gets a working setup.

# shellcheck disable=SC1090
[ -f "$CONFIG" ] && ! $RECONFIGURE && . "$CONFIG"

PRISM_SCOPE="${PRISM_SCOPE:-all}"
PRISM_PROVIDER="${PRISM_PROVIDER:-later}"
PRISM_API_PORT_PREF="${PRISM_API_PORT_PREF:-5000}"
PRISM_MODEL="${PRISM_MODEL:-}"
PRISM_OLLAMA_MEMORY_GIB="${PRISM_OLLAMA_MEMORY_GIB:-}"
PRISM_REMOTE_URL="${PRISM_REMOTE_URL:-}"

interactive() { [ -t 0 ] && [ -t 1 ] && ! $ASSUME_YES; }

# Returns the saved answer when it is still one of the options, otherwise the
# first option. A config carried to a different machine can name something that
# is no longer possible — a container runtime that is not installed, a GPU that
# is not there — and silently defaulting to it would offer a choice that cannot
# work.
default_or_first() {
  local saved="$1"; shift
  local opt

  for opt in "$@"; do
    [ "${opt%%:*}" = "$saved" ] && { printf '%s' "$saved"; return; }
  done

  printf '%s' "${1%%:*}"
}

# ask <variable> <prompt> <default> <option>...
#
# Each option is "value:description". Prints them numbered, reads a number, and
# assigns the chosen value. Anything unrecognised keeps the default rather than
# re-prompting, because a launcher that will not let you past a question is
# worse than one that guesses.
ask() {
  local __var="$1" prompt="$2" default="$3"; shift 3
  local options=("$@") i=1 opt value desc answer

  echo ""
  echo -e "${CYAN}$prompt${NC}"
  for opt in "${options[@]}"; do
    value="${opt%%:*}"; desc="${opt#*:}"
    if [ "$value" = "$default" ]; then
      echo -e "   $i) $desc ${GREEN}[default]${NC}"
    else
      echo "   $i) $desc"
    fi
    i=$((i + 1))
  done

  printf '   > '
  read -r answer || answer=""

  if [ -n "$answer" ] && [ -z "${answer//[0-9]/}" ] \
     && [ "$answer" -ge 1 ] 2>/dev/null && [ "$answer" -le "${#options[@]}" ]; then
    opt="${options[$((answer - 1))]}"
    printf -v "$__var" '%s' "${opt%%:*}"
  else
    printf -v "$__var" '%s' "$default"
  fi
}

configure() {
  echo ""
  if $RECONFIGURE; then
    echo -e "${CYAN}Starting from this machine's recommendations, ignoring your saved answers.${NC}"
  elif [ -f "$CONFIG" ]; then
    echo -e "${CYAN}Press Enter to keep what you chose last time, or pick something else.${NC}"
  else
    echo -e "${CYAN}Setting up. Press Enter to take the default in each case.${NC}"
  fi

  ask PRISM_SCOPE "What should I start?" "$PRISM_SCOPE" \
    "all:Everything — database, API and frontend" \
    "backend:Backend only — database and API" \
    "frontend:Frontend only — the Vite dev server"

  # Only offer what this machine can actually do, and put the fastest workable
  # option first. Listing vLLM on an Apple Silicon Mac would be an invitation to
  # pick something that cannot run at all.
  local provider_opts=() os arch metal avail model_default
  os="$(uname -s)"
  arch="$(uname -m)"

  # Metal is an Apple Silicon story, not a macOS one. Ollama on an Intel Mac is
  # CPU-only like anywhere else, so promising "the Apple GPU" there would be the
  # same kind of overclaim the container wording was corrected for.
  metal=false
  [ "$os" = "Darwin" ] && [ "$arch" = "arm64" ] && metal=true

  # Size from what the container runtime can hand out. On macOS and Windows that
  # is the VM's allocation, which is the real ceiling; fall back to the host's
  # RAM only when no runtime is up to ask.
  avail="$(prism_runtime_memory_gib 2>/dev/null || true)"
  [ -n "$avail" ] || avail="$(prism_host_memory_gib 2>/dev/null || true)"
  [ -n "$avail" ] || avail=8

  model_default="$(prism_recommended_model "$avail")"

  # Recommend from the machine only when there is no previous answer. Silently
  # replacing a deliberate 11 GiB with a freshly computed 5 would undo a choice
  # without saying so.
  local memory_default
  memory_default="${PRISM_OLLAMA_MEMORY_GIB:-$(prism_recommended_memory_gib "$avail")}"

  # Something already listening usually beats everything: it costs nothing and is
  # running for a reason. But "already running" must name *which* server, and it
  # must not hide the native option — the container holding the port is exactly
  # when someone wants to switch to Metal, and we can stop it to let them.
  local port_taken=false container_up=false native_installed=false
  _prism_port_open localhost 11434 && port_taken=true
  _prism_ollama_container_running && container_up=true
  command -v ollama >/dev/null 2>&1 && native_installed=true

  local detected_opt="" start_opt=""

  if $port_taken; then
    if $container_up; then
      if [ "$os" = "Darwin" ]; then
        detected_opt="detected-ollama:Use the Ollama container already on 11434 — CPU-only on macOS"
      else
        detected_opt="detected-ollama:Use the Ollama container already running on port 11434"
      fi
    else
      detected_opt="detected-ollama:Use the Ollama already running natively on port 11434"
    fi
  fi

  # Offered when the port is free, and also when our own container is the one
  # holding it, because that is a conflict dev.sh knows how to resolve. Withheld
  # only when a native Ollama already has the port — "start it" would be a no-op
  # and the detected option above says the same thing more honestly.
  if $native_installed && { ! $port_taken || $container_up; }; then
    start_opt="start-ollama:Start the Ollama you have installed"
    $metal && start_opt="$start_opt — uses the Apple GPU via Metal"
    if $container_up; then
      start_opt="$start_opt (stops the container, which holds the port)"
    fi
  fi

  # Likely-fastest first. A CPU-only container should not outrank the native
  # Metal server merely because it happens to be up already.
  if [ -n "$start_opt" ] && $container_up && [ "$os" = "Darwin" ]; then
    provider_opts+=("$start_opt")
    [ -n "$detected_opt" ] && provider_opts+=("$detected_opt")
  else
    [ -n "$detected_opt" ] && provider_opts+=("$detected_opt")
    [ -n "$start_opt" ] && provider_opts+=("$start_opt")
  fi

  # Everything else that might already be up. Port 11434 is handled above, because
  # Ollama is the one where *which* server answers changes what we offer.
  local candidate cport cid ctype cendpoint clabel
  for candidate in "${PRISM_PROVIDER_CANDIDATES[@]}"; do
    IFS='|' read -r cport cid ctype cendpoint clabel <<< "$candidate"

    if [ "$cport" != "11434" ] && _prism_port_open localhost "$cport"; then
      provider_opts+=("$cid:Use the $clabel already running on port $cport")
    fi
  done

  # The container needs no install at all, which is the point. State the cost
  # honestly rather than letting someone discover it as "the model is slow".
  if command -v docker >/dev/null 2>&1; then
    if [ "$os" = "Darwin" ]; then
      provider_opts+=("container-ollama:Run Ollama in a container — nothing to install; CPU-only on macOS")
    elif command -v nvidia-smi >/dev/null 2>&1; then
      provider_opts+=("container-ollama:Run Ollama in a container — nothing to install, uses your GPU")
    else
      provider_opts+=("container-ollama:Run Ollama in a container — nothing to install")
    fi

    # vLLM is CUDA-only. No GPU, no option.
    command -v nvidia-smi >/dev/null 2>&1 && \
      provider_opts+=("container-vllm:Run vLLM in a container — the only one with token probabilities")
  fi

  provider_opts+=("remote:Point at a server somewhere else — I will give you the URL")
  provider_opts+=("later:Nothing yet — I will connect one from the Models page")

  echo ""
  echo -e "${CYAN}Detected ${avail} GiB available to containers.${NC}"

  ask PRISM_PROVIDER \
    "Which model should Prism read? (the heatmaps and Token Explorer are built on this)" \
    "$(default_or_first "$PRISM_PROVIDER" "${provider_opts[@]}")" \
    "${provider_opts[@]}"

  # Follow-ups, only for the answers that need them.
  case "$PRISM_PROVIDER" in
    container-ollama|start-ollama)
      echo ""
      [ -n "$PRISM_MODEL" ] && model_default="$PRISM_MODEL"
      printf "   Model to pull [%s]: " "$model_default"
      read -r answer || answer=""
      PRISM_MODEL="${answer:-$model_default}"

      if [ "$PRISM_PROVIDER" = "container-ollama" ]; then
        printf "   Memory for the container in GiB [%s]: " "$memory_default"
        read -r answer || answer=""
        PRISM_OLLAMA_MEMORY_GIB="${answer:-$memory_default}"
      fi
      ;;
    remote)
      echo ""
      local url_default="${PRISM_REMOTE_URL:-http://localhost:11434}"
      printf "   Endpoint URL [%s]: " "$url_default"
      read -r answer || answer=""
      PRISM_REMOTE_URL="${answer:-$url_default}"
      ;;
  esac

  # Only worth asking about when the usual port is unavailable; otherwise it is
  # a question with one sensible answer.
  if _prism_port_open localhost 5000; then
    local holder
    holder="$(command -v lsof >/dev/null 2>&1 && lsof -nP -iTCP:5000 -sTCP:LISTEN 2>/dev/null | awk 'NR==2 {print $1}')"
    echo ""
    warn "Port 5000 is taken${holder:+ by '$holder'}${holder:+.}"
    [ "$(uname -s)" = "Darwin" ] && warn "On macOS that is usually AirPlay Receiver."
    printf "   API port [5001]: "
    read -r answer || answer=""
    PRISM_API_PORT_PREF="${answer:-5001}"
  fi

  cat > "$CONFIG" <<EOF
# Written by ./dev.sh. Delete this file or run ./dev.sh --reconfigure to change it.
# Not tracked by git — these are your choices, not the project's.
PRISM_SCOPE=$PRISM_SCOPE
PRISM_PROVIDER=$PRISM_PROVIDER
PRISM_API_PORT_PREF=$PRISM_API_PORT_PREF
PRISM_MODEL=$PRISM_MODEL
PRISM_OLLAMA_MEMORY_GIB=${PRISM_OLLAMA_MEMORY_GIB:-$memory_default}
PRISM_REMOTE_URL=$PRISM_REMOTE_URL
EOF

  echo ""
  ok "Saved. Next run these become the defaults; --yes skips the questions."
}

# Source dev-env early: configure() needs _prism_port_open.
# shellcheck disable=SC1091
. "$ROOT/scripts/dev-env.sh"

# Ask every time there is someone to answer. What you wanted last run is not
# necessarily what you want now — a different model, backend only, a remote
# endpoint — and having to remember a --reconfigure flag to change your mind is
# a worse default than three keystrokes. Last run's answers become this run's
# defaults, so holding Enter reproduces it exactly.
if interactive; then
  configure
elif [ -f "$CONFIG" ]; then
  echo -e "${CYAN}Using .prism-dev.conf${NC} (scope: $PRISM_SCOPE, provider: $PRISM_PROVIDER)"
fi

# Explicit flags always beat the saved answers.
if ! $SCOPE_FROM_FLAG; then
  case "$PRISM_SCOPE" in
    backend)  BACKEND_ONLY=true ;;
    frontend) FRONTEND_ONLY=true ;;
  esac
fi

[ "$PRISM_PROVIDER" = "container-vllm" ] && GPU=true
: "${PRISM_API_PORT:=$PRISM_API_PORT_PREF}"

# ── Preflight ─────────────────────────────────────────────────────────
step "Checking prerequisites..."

# Discovery came from scripts/dev-env.sh, sourced above — the same code the
# pre-commit hook and the doctor use, so a toolchain that is installed but not
# on this shell's PATH is found rather than reported missing.
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

  # Fall through to the readiness check rather than letting `set -e` end the run here.
  # The wait loop below exists precisely to report a Postgres that did not come up, and an
  # unguarded failure at this line kills the launcher before it can — no message, no API, no
  # frontend, just a script that stops.
  docker compose "${COMPOSE_ARGS[@]}" \
    || warn "docker compose exited non-zero; checking whether Postgres came up anyway."

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

# ── 1b. Inference provider ───────────────────────────────────────────
#
# Answering "start Ollama for me" and then being told there is no model
# connected would make the question pointless.

# PROVIDER_ENDPOINT is what gets registered with the API further down, so the
# app is usable the moment it opens rather than showing an empty Models page.
PROVIDER_ENDPOINT=""
PROVIDER_TYPE=""

if ! $FRONTEND_ONLY; then
  case "$PRISM_PROVIDER" in

    detected-*)
      # Resolved from the candidate list rather than hard-coded per provider, so
      # adding a server is one line in one place.
      for candidate in "${PRISM_PROVIDER_CANDIDATES[@]}"; do
        IFS='|' read -r cport cid ctype cendpoint clabel <<< "$candidate"

        [ "$cid" = "$PRISM_PROVIDER" ] || continue

        if _prism_port_open localhost "$cport"; then
          ok "$clabel is listening on $cport."
          PROVIDER_ENDPOINT="$cendpoint"; PROVIDER_TYPE="$ctype"
        else
          # Saved from a previous run, but gone now — say so rather than
          # registering an endpoint nothing is behind.
          warn "Nothing is answering on port $cport, where $clabel used to be."
          warn "Prism will still start; connect a model from the Models page."
        fi

        break
      done
      ;;

    start-ollama)
      # "Something is on 11434" is not the same as "your native Ollama is on
      # 11434". If the container from a previous run still holds the port, a
      # native `ollama serve` cannot bind, every client keeps talking to the
      # container, and you end up comparing the container against itself while
      # believing you switched to Metal.
      if _prism_ollama_container_running; then
        warn "The Ollama container is holding port 11434, so a native Ollama cannot start."
        step "Stopping the container so the native one can take the port..."
        docker compose -f "$ROOT/docker-compose.yml" --profile ollama stop ollama >/dev/null 2>&1 || true
        for _ in $(seq 1 15); do _prism_port_open localhost 11434 || break; sleep 1; done
      fi

      # Ask again rather than assume the stop worked. Announcing "running
      # natively" while the container still has the port would recreate the
      # exact false belief this branch exists to prevent.
      if _prism_ollama_container_running; then
        warn "The container still holds port 11434, so this is the container, not Metal."
        warn "Stop it yourself and run again:  docker compose --profile ollama stop ollama"
        PROVIDER_ENDPOINT="http://localhost:11434"; PROVIDER_TYPE="Ollama"
        ensure_ollama_model "docker exec prism-ollama " "$PRISM_MODEL"
      else
        if _prism_port_open localhost 11434; then
          ok "Ollama is already running natively."
        else
          step "Starting Ollama..."
          ollama serve > "$LOGS/ollama.log" 2>&1 &
          echo $! > "$LOGS/ollama.pid"
          for _ in $(seq 1 20); do _prism_port_open localhost 11434 && break; sleep 1; done
        fi

        if _prism_port_open localhost 11434; then
          ok "Ollama is listening on 11434."
          PROVIDER_ENDPOINT="http://localhost:11434"; PROVIDER_TYPE="Ollama"
          ensure_ollama_model "" "$PRISM_MODEL"
        else
          warn "Ollama did not come up — see $LOGS/ollama.log"
        fi
      fi
      ;;

    container-ollama)
      # The mirror image of the problem above: a native Ollama on 11434 stops
      # the container publishing to it, and the container ends up unreachable
      # while everything still appears to work.
      if _prism_port_open localhost 11434 && ! _prism_ollama_container_running; then
        warn "Something already holds port 11434 — most likely a native Ollama."
        warn "Prism will use that instead of the container; they cannot both have the port."
        warn "To use the container, stop the native one first:  pkill -f 'ollama serve'"
        PROVIDER_ENDPOINT="http://localhost:11434"; PROVIDER_TYPE="Ollama"
        # Still the native server's model list that matters, so pull into it —
        # registering an endpoint whose model is missing opens an empty
        # Playground, which is the thing this whole step exists to avoid.
        ensure_ollama_model "" "$PRISM_MODEL"
      else
        step "Starting Ollama in a container..."
        export PRISM_OLLAMA_MEMORY="${PRISM_OLLAMA_MEMORY_GIB:-8}g"
        export PRISM_OLLAMA_CPUS="${PRISM_OLLAMA_CPUS:-$(prism_runtime_cpus 2>/dev/null || echo 4)}"
        echo "   Limits: ${PRISM_OLLAMA_MEMORY} memory, ${PRISM_OLLAMA_CPUS} CPUs"

        if docker compose -f "$ROOT/docker-compose.yml" --profile ollama up -d ollama; then
          echo -n "   Waiting for Ollama..."
          for _ in $(seq 1 60); do
            _prism_port_open localhost 11434 && break
            echo -n "."; sleep 1
          done
          echo ""

          if _prism_port_open localhost 11434; then
            ok "Ollama is listening on 11434."
            PROVIDER_ENDPOINT="http://localhost:11434"; PROVIDER_TYPE="Ollama"
            ensure_ollama_model "docker exec prism-ollama " "$PRISM_MODEL"
          else
            warn "The container started but nothing is answering on 11434."
            warn "Check:  docker compose logs ollama"
          fi
        else
          warn "Could not start the Ollama container. Check:  docker compose logs ollama"
        fi
      fi
      ;;

    container-vllm)
      # Brought up by the gpu profile in the PostgreSQL step above.
      if _prism_port_open localhost 8000; then
        ok "vLLM is listening on 8000."
        PROVIDER_ENDPOINT="http://localhost:8000"; PROVIDER_TYPE="Vllm"
      else
        warn "vLLM is not answering on 8000 yet. It loads weights on first start,"
        warn "which can take several minutes. Follow it with: docker compose logs -f vllm"
      fi
      ;;

    remote)
      step "Checking $PRISM_REMOTE_URL ..."
      remote_host="$(printf '%s' "$PRISM_REMOTE_URL" | sed -E 's#^[a-z]+://##; s#[:/].*$##')"
      remote_port="$(printf '%s' "$PRISM_REMOTE_URL" | sed -nE 's#^[a-z]+://[^:/]+:([0-9]+).*#\1#p')"
      [ -n "$remote_port" ] || case "$PRISM_REMOTE_URL" in https://*) remote_port=443 ;; *) remote_port=80 ;; esac

      if _prism_port_open "$remote_host" "$remote_port"; then
        ok "Reachable."
        PROVIDER_ENDPOINT="$PRISM_REMOTE_URL"
        # Guess the type from how it answers; the probe on registration corrects it.
        if curl -fsS --max-time 5 "$PRISM_REMOTE_URL/api/tags" >/dev/null 2>&1; then
          PROVIDER_TYPE="Ollama"
        else
          PROVIDER_TYPE="OpenAiCompatible"
        fi
        echo "   Looks like: $PROVIDER_TYPE"
      else
        warn "Nothing is answering at $remote_host:$remote_port."
        warn "Prism will still start; connect it from the Models page once it is up."
      fi
      ;;
  esac
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

    # A compile error is a normal thing to hit on a first run, and it needs to be reported as
    # one. Unguarded, `set -e` ends the launcher on the spot with the build output already
    # swallowed by -q, which reads as "the script mysteriously stopped".
    if ! dotnet build "$ROOT/backend/Prism.sln" --nologo; then
      warn "The backend did not build. Fix the errors above, then run ./dev.sh again."
      warn "The database is up, so this is the only thing standing in the way."
      exit 1
    fi
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

    # Register whatever we started, so the app opens on a working Playground
    # rather than an empty Models page. Skipped when something is already
    # registered — re-adding on every launch would pile up duplicates.
    if [ -n "$PROVIDER_ENDPOINT" ]; then
      existing="$(curl -fsS --max-time 5 "$PRISM_API_URL/api/v1/models/instances" 2>/dev/null || echo '')"

      if printf '%s' "$existing" | grep -q "\"endpoint\":\"${PROVIDER_ENDPOINT}\""; then
        ok "$PROVIDER_ENDPOINT is already registered."
      else
        if curl -fsS --max-time 15 -X POST "$PRISM_API_URL/api/v1/models/instances" \
             -H 'Content-Type: application/json' \
             -d "{\"name\":\"Local ${PROVIDER_TYPE}\",\"endpoint\":\"${PROVIDER_ENDPOINT}\",\"providerType\":\"${PROVIDER_TYPE}\",\"isDefault\":true}" \
             >/dev/null 2>&1; then
          ok "Registered $PROVIDER_ENDPOINT as the default model."
        else
          warn "Could not register $PROVIDER_ENDPOINT automatically."
          warn "Add it from the Models page — it will be found by the search there."
        fi
      fi
    fi
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
