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

# ---------------------------------------------------------------------------
# Sizing
#
# On macOS and Windows the container runtime is a Linux VM with its own memory
# allocation, and that allocation — not the machine's RAM — is the ceiling a
# container actually hits. A 64 GB MacBook whose Docker VM is capped at 8 GB
# can only give a container 8 GB, so reading hw.memsize would size things
# wrongly in the one direction that hurts: an OOM-killed model load.
# ---------------------------------------------------------------------------

# Memory the container runtime can hand out, in whole GiB. Empty if unknown.
prism_runtime_memory_gib() {
  local bytes
  bytes="$(docker info --format '{{.MemTotal}}' 2>/dev/null)"

  if [ -n "$bytes" ] && [ "$bytes" -gt 0 ] 2>/dev/null; then
    printf '%d' $((bytes / 1073741824))
    return 0
  fi

  return 1
}

# CPUs the container runtime can hand out. Empty if unknown.
prism_runtime_cpus() {
  local cpus
  cpus="$(docker info --format '{{.NCPU}}' 2>/dev/null)"
  [ -n "$cpus" ] && [ "$cpus" -gt 0 ] 2>/dev/null && printf '%d' "$cpus"
}

# Memory of the host itself, as a fallback when no runtime is up.
prism_host_memory_gib() {
  case "$(uname -s)" in
    Darwin)
      local bytes
      bytes="$(sysctl -n hw.memsize 2>/dev/null)"
      [ -n "$bytes" ] && printf '%d' $((bytes / 1073741824))
      ;;
    Linux)
      local kb
      kb="$(awk '/^MemTotal:/ {print $2}' /proc/meminfo 2>/dev/null)"
      [ -n "$kb" ] && printf '%d' $((kb / 1048576))
      ;;
  esac
}

# A model that will actually load in the memory available.
#
# Sizes are the q4 quantisations Ollama pulls by default. The headroom matters:
# a 7B q4 is about 4.4 GB of weights and wants roughly 6 GB in practice once
# the KV cache and runtime are counted, so the thresholds sit above the naive
# file size rather than at it.
prism_recommended_model() {
  local gib="${1:-0}"

  if   [ "$gib" -ge 24 ] 2>/dev/null; then printf 'qwen2.5:14b-instruct'
  elif [ "$gib" -ge 10 ] 2>/dev/null; then printf 'mistral:7b-instruct'
  elif [ "$gib" -ge 6 ]  2>/dev/null; then printf 'llama3.2:3b'
  else                                     printf 'qwen2.5:1.5b'
  fi
}

# Memory limit to give the Ollama container, in GiB.
#
# Most of what is available, with a floor so a small VM still gets enough to
# load the small model, and a ceiling because handing a single container 60 GB
# helps nothing and starves everything else.
prism_recommended_memory_gib() {
  local available="${1:-0}" want

  want=$(( available * 3 / 4 ))

  [ "$want" -lt 4 ] && want=4
  [ "$want" -gt 24 ] && want=24
  [ "$want" -gt "$available" ] && [ "$available" -gt 0 ] && want="$available"

  printf '%d' "$want"
}

# ---------------------------------------------------------------------------
# Database options
#
# "No database available" is a diagnosis, not a decision. What a person can
# actually do about it depends on what is already on their machine, so the list
# below is assembled from what is there rather than printed from a template.
#
# Each option is numbered so scripts/doctor.sh can offer to run it, and each
# carries the literal command, so it is useful even when nothing offers.
#
# PRISM_DB_OPTIONS is set to the space-separated ids of the options printed, in
# the order they were printed.
# ---------------------------------------------------------------------------

# Identifies which container runtime is installed, since "start Docker" means a
# different thing depending on which one it is. Docker Desktop is not the only
# way to get a daemon, and telling a Rancher Desktop user to open Docker.app
# sends them looking for something they deliberately did not install.
#
# Echoes "<id>|<display name>|<start command>", or nothing when none is found.
_prism_container_runtime() {
  if [ -d "/Applications/Rancher Desktop.app" ]; then
    printf 'rancher|Rancher Desktop|open -a "Rancher Desktop"'
  elif [ -d /Applications/OrbStack.app ]; then
    printf 'orbstack|OrbStack|open -a OrbStack'
  elif [ -d /Applications/Docker.app ]; then
    printf 'docker-desktop|Docker Desktop|open -a Docker'
  elif command -v colima >/dev/null 2>&1; then
    printf 'colima|Colima|colima start'
  elif command -v podman >/dev/null 2>&1 && ! command -v docker >/dev/null 2>&1; then
    printf 'podman|Podman|podman machine start'
  elif command -v systemctl >/dev/null 2>&1 && [ -S /var/run/docker.sock -o -f /lib/systemd/system/docker.service ]; then
    printf 'systemd|Docker|sudo systemctl start docker'
  fi
}

# Names whatever holds a TCP port, when the tools to find out are present.
_prism_port_owner() {
  local port="$1"
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null | awk 'NR==2 {print $1}'
  fi
}

# The Homebrew postgresql formula installed on this machine, if any.
_prism_brew_postgres_formula() {
  command -v brew >/dev/null 2>&1 || return 1
  brew list --formula 2>/dev/null | grep -E '^postgresql(@[0-9]+)?$' | tail -1
}

prism_database_options() {
  local n=0 os owner brew_pg
  os="$(uname -s)"
  PRISM_DB_OPTIONS=""

  _opt() {
    n=$((n + 1))
    PRISM_DB_OPTIONS="$PRISM_DB_OPTIONS $1"
    printf '\n  %s) %s\n' "$n" "$2"
  }
  _cmd()  { printf '        %s\n' "$1"; }
  _note() { printf '     %s\n' "$1"; }

  printf '\n  %s\n' "The integration tests need a PostgreSQL with the pgvector extension."
  printf '  %s\n'   "Here is what this machine can do about it."

  # --- Something is squatting on the port -----------------------------------
  owner="$(_prism_port_owner 5438)"
  if [ -n "$owner" ]; then
    printf '\n  %s\n' "! Port 5438 is already held by '$owner', which is not answering as"
    printf '  %s\n'   "  PostgreSQL. That will block the container from binding. Check with:"
    _cmd "lsof -nP -iTCP:5438 -sTCP:LISTEN"
  fi

  # --- Docker ---------------------------------------------------------------
  if command -v docker >/dev/null 2>&1; then
    if docker info >/dev/null 2>&1; then
      _opt docker-compose "Start the bundled Postgres (Docker is already running)"
      _cmd "docker compose up -d postgres"
      _note "The compose file pins pgvector/pgvector:pg16, so the extension is there."
      _note "If it fails, the reason is in:  docker compose logs postgres"
    else
      local rt rt_name rt_cmd
      rt="$(_prism_container_runtime)"
      rt_name="$(printf '%s' "$rt" | cut -d'|' -f2)"
      rt_cmd="$(printf '%s' "$rt" | cut -d'|' -f3)"

      if [ -n "$rt_name" ]; then
        _opt docker-start "Start $rt_name, then the bundled Postgres  (recommended)"
        _cmd "$rt_cmd"
        _cmd "docker compose up -d postgres"
        _note "The daemon takes up to a minute to come up the first time."
      else
        _opt docker-start "Start your container runtime, then the bundled Postgres"
        _cmd "docker compose up -d postgres"
        _note "The docker command is here but no daemon is answering, and none of the"
        _note "usual runtimes were recognised. Start whichever one you use."
      fi
    fi
  fi

  # --- Homebrew -------------------------------------------------------------
  if [ "$os" = "Darwin" ] && command -v brew >/dev/null 2>&1; then
    brew_pg="$(_prism_brew_postgres_formula)"

    if [ -n "$brew_pg" ]; then
      _opt brew-start "Start the Homebrew PostgreSQL you already have ($brew_pg)"
      _cmd "brew services start $brew_pg"
      _cmd "export PRISM_TEST_DB=\"Host=localhost;Port=5432;Database=prism_test;Username=\$USER;Password=\""
      _note "Needs pgvector as well:  brew install pgvector"
    else
      _opt brew-install "Install PostgreSQL 16 and pgvector with Homebrew (no Docker needed)"
      _cmd "brew install postgresql@16 pgvector && brew services start postgresql@16"
      _cmd "export PRISM_TEST_DB=\"Host=localhost;Port=5432;Database=prism_test;Username=\$USER;Password=\""
      _note "A permanent local install rather than a container. Roughly 300 MB."
    fi
  elif [ "$os" = "Linux" ] && command -v apt-get >/dev/null 2>&1; then
    _opt apt-install "Install PostgreSQL 16 and pgvector with apt (no Docker needed)"
    _cmd "sudo apt-get install -y postgresql-16 postgresql-16-pgvector"
    _cmd "export PRISM_TEST_DB=\"Host=localhost;Port=5432;Database=prism_test;Username=postgres;Password=postgres\""
  fi

  # --- A server that is already running somewhere ---------------------------
  local found=""
  for candidate_port in 5432 5433 5438 54320; do
    if _prism_port_open localhost "$candidate_port"; then
      found="$found $candidate_port"
    fi
  done

  if [ -n "$found" ]; then
    _opt use-existing "Use a PostgreSQL already listening here — found on port(s):$found"
    for candidate_port in $found; do
      _cmd "export PRISM_TEST_DB=\"Host=localhost;Port=$candidate_port;Database=prism_test;Username=postgres;Password=postgres\""
    done
    _note "Adjust the username and password to match that server. The database"
    _note "itself is created for you; pgvector must already be installed on it."
  else
    _opt use-existing "Point the tests at any PostgreSQL you have, anywhere"
    _cmd "export PRISM_TEST_DB=\"Host=…;Port=…;Database=prism_test;Username=…;Password=…\""
    _note "It needs the pgvector extension available. The database named in the"
    _note "string is created for you if it does not exist."
  fi

  # --- Escape hatches -------------------------------------------------------
  _opt unit-only "Skip the tests that need a database, just this once"
  _cmd "cd backend && dotnet test Prism.sln --filter FullyQualifiedName~Unit"
  _note "Around two thirds of the suite. Nothing covering job claiming, analytics"
  _note "aggregation or vector search, which is where the interesting bugs live."

  _opt no-verify "Commit without the gate"
  _cmd "git commit --no-verify"
  _note "CI will still run the full suite."

  printf '\n  %s\n' "Whichever you pick, ./scripts/doctor.sh will confirm it worked."

  unset -f _opt _cmd _note
  export PRISM_DB_OPTIONS
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
        prism_database_options
        ;;
    esac
  done
}
