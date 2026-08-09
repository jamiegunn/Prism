#!/usr/bin/env bash
#
# Finds and provisions everything the build and test suites need.
#
# Source this, do not execute it — its job is to export PATH, DOTNET_ROOT and
# PRISM_TEST_DB into the calling shell:
#
#     . scripts/dev-env.sh
#     prism_env_prepare          # discover tools, start Postgres, install packages
#
# Both the pre-commit hook and scripts/doctor.sh use it, so a fix made here
# reaches every entry point at once.
#
# Written for bash 3.2, which is what macOS ships. No associative arrays, no
# mapfile, no ${var,,}.

# Not `set -e`: this file is sourced, and killing the caller's shell on a failed
# probe would be hostile. Every function returns a status instead.

PRISM_ENV_QUIET="${PRISM_ENV_QUIET:-0}"
PRISM_ENV_MISSING="${PRISM_ENV_MISSING:-}"

_prism_say()  { [ "$PRISM_ENV_QUIET" = "1" ] || printf '%s\n' "$*" >&2; }
_prism_act()  { printf '   → %s\n' "$*" >&2; }
_prism_warn() { printf '   ! %s\n' "$*" >&2; }

# ---------------------------------------------------------------------------
# Repo root
# ---------------------------------------------------------------------------

prism_repo_root() {
  git rev-parse --show-toplevel 2>/dev/null || pwd
}

# ---------------------------------------------------------------------------
# .NET
#
# A GUI git client, an editor's integrated terminal and a cron job all start
# with a thinner PATH than a login shell. The SDK is usually installed and
# simply not visible, so look in the standard locations before giving up.
# ---------------------------------------------------------------------------

prism_find_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    return 0
  fi

  local candidate
  for candidate in \
    "${DOTNET_ROOT:-}" \
    "$HOME/.dotnet" \
    /usr/local/share/dotnet \
    /opt/homebrew/share/dotnet \
    /usr/share/dotnet \
    /usr/lib/dotnet
  do
    if [ -n "$candidate" ] && [ -x "$candidate/dotnet" ]; then
      PATH="$candidate:$PATH"
      DOTNET_ROOT="$candidate"
      export PATH DOTNET_ROOT
      _prism_act "found dotnet at $candidate (was not on PATH)"
      return 0
    fi
  done

  return 1
}

# Reports on the runtime situation. Never fatal: the repo sets RollForward in
# backend/Directory.Build.props, so a 9.0-targeted app runs on a 10.0 runtime.
# This exists to explain the situation, and to belt-and-brace the environment
# variable in case someone builds with an older checkout of the props file.
prism_check_dotnet_runtime() {
  local runtimes
  runtimes="$(dotnet --list-runtimes 2>/dev/null)"

  # Both frameworks matter. A machine can carry Microsoft.NETCore.App 9 without
  # Microsoft.AspNetCore.App 9, and the test host needs the second one — which is
  # how this surfaces as a failing test suite rather than an obvious missing SDK.
  if printf '%s' "$runtimes" | grep -q 'Microsoft.NETCore.App 9\.' \
     && printf '%s' "$runtimes" | grep -q 'Microsoft.AspNetCore.App 9\.'; then
    return 0
  fi

  export DOTNET_ROLL_FORWARD=Major
  _prism_act "no complete .NET 9 runtime; rolling forward to the newest major"
  return 0
}

# ---------------------------------------------------------------------------
# Node
# ---------------------------------------------------------------------------

prism_find_node() {
  if command -v npm >/dev/null 2>&1; then
    return 0
  fi

  # nvm installs outside the default PATH and is not loaded by non-login shells.
  local nvm_dir="${NVM_DIR:-$HOME/.nvm}"
  if [ -s "$nvm_dir/nvm.sh" ]; then
    # shellcheck disable=SC1090
    . "$nvm_dir/nvm.sh" >/dev/null 2>&1
    if command -v npm >/dev/null 2>&1; then
      _prism_act "loaded node via nvm"
      return 0
    fi
  fi

  local candidate
  for candidate in /opt/homebrew/bin /usr/local/bin /usr/bin; do
    if [ -x "$candidate/npm" ]; then
      PATH="$candidate:$PATH"
      export PATH
      _prism_act "found npm at $candidate (was not on PATH)"
      return 0
    fi
  done

  return 1
}

# Installs frontend packages when they are missing or older than the lockfile.
prism_ensure_node_modules() {
  local root frontend
  root="$(prism_repo_root)"
  frontend="$root/frontend"

  [ -d "$frontend" ] || return 0

  local stamp="$frontend/node_modules/.package-lock.json"

  if [ -d "$frontend/node_modules" ] && [ -f "$stamp" ] \
     && [ ! "$frontend/package-lock.json" -nt "$stamp" ]; then
    return 0
  fi

  if [ -d "$frontend/node_modules" ]; then
    _prism_act "package-lock.json is newer than node_modules; reinstalling"
  else
    _prism_act "frontend/node_modules is missing; installing"
  fi

  ( cd "$frontend" && npm ci --silent ) >/dev/null 2>&1 && return 0

  _prism_warn "npm ci failed; run it yourself in frontend/ to see why"
  return 1
}

# ---------------------------------------------------------------------------
# Database
# ---------------------------------------------------------------------------

# Pulls a field out of an Npgsql connection string, case-insensitively.
_prism_conn_field() {
  printf '%s' "$1" \
    | tr ';' '\n' \
    | grep -i "^[[:space:]]*$2[[:space:]]*=" \
    | head -1 \
    | cut -d= -f2- \
    | sed 's/^[[:space:]]*//; s/[[:space:]]*$//'
}

# True when something accepts TCP connections at host/port.
_prism_port_open() {
  local host="$1" port="$2"
  ( exec 3<>"/dev/tcp/$host/$port" ) >/dev/null 2>&1
}

_prism_docker_up() {
  docker info >/dev/null 2>&1
}

# Waits for Postgres to accept connections, up to a bounded number of seconds.
_prism_wait_for_postgres() {
  local host="$1" port="$2" limit="${3:-45}" waited=0

  while [ "$waited" -lt "$limit" ]; do
    if _prism_port_open "$host" "$port"; then
      # The port opens slightly before the server will answer queries.
      if docker compose exec -T postgres pg_isready -U postgres >/dev/null 2>&1; then
        return 0
      fi
      # Not the compose container, or compose is unavailable — an open port is
      # the best signal we have.
      if ! _prism_docker_up; then
        return 0
      fi
    fi
    sleep 1
    waited=$((waited + 1))
  done

  return 1
}

# The test database itself is created by the test fixture, which connects to the
# maintenance database and issues CREATE DATABASE if needed. That keeps it
# working from a bare `dotnet test`, an editor's runner and CI — not only from
# the paths that happen to go through this script — and needs no psql on PATH.

# Makes sure the integration tests have a database, and that PRISM_TEST_DB
# points at it. Returns non-zero only when it genuinely cannot get there.
prism_ensure_database() {
  local root host port dbname
  root="$(prism_repo_root)"

  if [ -n "${PRISM_TEST_DB:-}" ]; then
    host="$(_prism_conn_field "$PRISM_TEST_DB" Host)"
    port="$(_prism_conn_field "$PRISM_TEST_DB" Port)"
    [ -n "$host" ] || host=localhost
    [ -n "$port" ] || port=5432

    if _prism_port_open "$host" "$port"; then
      return 0
    fi

    # Set but unreachable. Saying so beats letting sixty tests fail with a
    # connection error and calling that a red suite.
    _prism_warn "PRISM_TEST_DB points at $host:$port, which is not accepting connections"

    if [ "$host" != "localhost" ] && [ "$host" != "127.0.0.1" ]; then
      return 1
    fi
    # Local, so the compose stack below may well fix it.
  fi

  # Prefer a Postgres already listening on the compose port.
  if _prism_port_open localhost 5438; then
    dbname=prism_test
    export PRISM_TEST_DB="Host=localhost;Port=5438;Database=$dbname;Username=postgres;Password=postgres"
    _prism_act "using Postgres on localhost:5438 (database '$dbname', created if absent)"
    return 0
  fi

  if ! _prism_docker_up; then
    return 1
  fi

  _prism_act "starting Postgres (docker compose up -d postgres)"

  if ! ( cd "$root" && docker compose up -d postgres ) >/dev/null 2>&1; then
    _prism_warn "docker compose could not start the postgres service"
    return 1
  fi

  if ! ( cd "$root" && _prism_wait_for_postgres localhost 5438 45 ); then
    _prism_warn "Postgres did not become ready within 45 seconds"
    return 1
  fi

  dbname=prism_test
  export PRISM_TEST_DB="Host=localhost;Port=5438;Database=$dbname;Username=postgres;Password=postgres"
  _prism_act "Postgres ready; PRISM_TEST_DB set to database '$dbname'"
  return 0
}

# ---------------------------------------------------------------------------
# Entry point
#
# prism_env_prepare <backend?> <frontend?>  — pass 1 to prepare that half.
# Returns 0 when everything the requested halves need is present.
# ---------------------------------------------------------------------------

prism_env_prepare() {
  local want_backend="${1:-1}" want_frontend="${2:-1}" ok=0

  PRISM_ENV_MISSING=""

  if [ "$want_backend" = "1" ]; then
    if prism_find_dotnet; then
      prism_check_dotnet_runtime
      if ! prism_ensure_database; then
        PRISM_ENV_MISSING="$PRISM_ENV_MISSING database"
        ok=1
      fi
    else
      PRISM_ENV_MISSING="$PRISM_ENV_MISSING dotnet"
      ok=1
    fi
  fi

  if [ "$want_frontend" = "1" ]; then
    if prism_find_node; then
      prism_ensure_node_modules || { PRISM_ENV_MISSING="$PRISM_ENV_MISSING node_modules"; ok=1; }
    else
      PRISM_ENV_MISSING="$PRISM_ENV_MISSING node"
      ok=1
    fi
  fi

  export PRISM_ENV_MISSING
  return $ok
}

# Prints what to do about whatever prism_env_prepare could not fix.
prism_env_explain() {
  local item
  for item in ${PRISM_ENV_MISSING:-}; do
    case "$item" in
      dotnet)
        printf '\n  %s\n' "The .NET SDK is not installed, or is somewhere unusual."
        printf '  %s\n'   "Install it from https://dotnet.microsoft.com/download, or set DOTNET_ROOT"
        printf '  %s\n'   "to where it already lives. Verify with:  dotnet --info"
        ;;
      node)
        printf '\n  %s\n' "Node.js is not installed, or is not visible to this shell."
        printf '  %s\n'   "Install Node 22 from https://nodejs.org, or 'nvm use' in frontend/."
        printf '  %s\n'   "Verify with:  node --version"
        ;;
      node_modules)
        printf '\n  %s\n' "Frontend packages could not be installed."
        printf '  %s\n'   "Run it directly to see the error:  cd frontend && npm ci"
        ;;
      database)
        printf '\n  %s\n' "The backend tests need PostgreSQL and none could be reached or started."
        if ! docker info >/dev/null 2>&1; then
          printf '  %s\n' "Docker is not running. Start Docker Desktop, then either commit again"
          printf '  %s\n' "or run ./scripts/doctor.sh, which will bring Postgres up for you."
        else
          printf '  %s\n' "Docker is running but the postgres service would not start. Try:"
          printf '  %s\n' "    docker compose up -d postgres && docker compose logs postgres"
        fi
        printf '  %s\n' "Alternatively point the tests at any Postgres you already have:"
        printf '  %s\n' '    export PRISM_TEST_DB="Host=…;Port=…;Database=prism_test;Username=…;Password=…"'
        ;;
    esac
  done
}
