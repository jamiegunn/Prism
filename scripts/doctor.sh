#!/usr/bin/env bash
#
# Checks everything Prism needs to build and test, and fixes what it can.
#
#     ./scripts/doctor.sh
#
# Unlike the pre-commit hook, this is allowed to be slow and to start your
# container runtime, because you are sitting in front of it. Run it after
# cloning, after a machine rebuild, or when the hook says something is missing.

set -uo pipefail

# Prefer git's idea of the root; fall back to this script's parent directory so
# the doctor still works in a tarball or a copy that is not a git checkout.
if ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" && [ -n "$ROOT" ]; then
  :
else
  ROOT="$(cd "$(dirname "$0")/.." && pwd)"
fi
cd "$ROOT" || { echo "cannot enter $ROOT" >&2; exit 1; }

BOLD=''; DIM=''; RED=''; GREEN=''; YELLOW=''; RESET=''
if [ -t 1 ]; then
  BOLD=$'\033[1m'; DIM=$'\033[2m'; RED=$'\033[31m'
  GREEN=$'\033[32m'; YELLOW=$'\033[33m'; RESET=$'\033[0m'
fi

pass() { printf '%s\n' "  ${GREEN}✓${RESET} $*"; }
warn() { printf '%s\n' "  ${YELLOW}!${RESET} $*"; }
bad()  { printf '%s\n' "  ${RED}✗${RESET} $*"; }
head_() { printf '\n%s\n' "${BOLD}$*${RESET}"; }

PROBLEMS=0

printf '%s\n' "${BOLD}Prism environment check${RESET}"
printf '%s\n' "${DIM}$ROOT${RESET}"

# shellcheck disable=SC1091
. "$ROOT/scripts/dev-env.sh"

# ---------------------------------------------------------------------------
# Docker — started here, because this script is interactive and can wait.
# ---------------------------------------------------------------------------

# Docker is only a means of getting a database. It is checked first so that the
# database step below can use it, but a missing Docker is not itself a failure —
# if Postgres turns out to be reachable some other way, nothing here matters.
head_ "Docker"

if docker info >/dev/null 2>&1; then
  pass "daemon is running"
elif ! command -v docker >/dev/null 2>&1; then
  warn "not installed"
  printf '    %s\n' "${DIM}Only needed to run the test database. Not a problem if Postgres${RESET}"
  printf '    %s\n' "${DIM}is available another way — checked below.${RESET}"
else
  warn "installed but not running"
  RT="$(_prism_container_runtime)"
  RT_NAME="$(printf '%s' "$RT" | cut -d'|' -f2)"
  RT_CMD="$(printf '%s' "$RT" | cut -d'|' -f3)"

  if [ -n "$RT_CMD" ]; then
    printf '    %s' "starting $RT_NAME"
    eval "$RT_CMD" >/dev/null 2>&1
    waited=0
    while [ "$waited" -lt 90 ]; do
      if docker info >/dev/null 2>&1; then break; fi
      printf '.'
      sleep 3
      waited=$((waited + 3))
    done
    printf '\n'
    if docker info >/dev/null 2>&1; then
      pass "daemon is running ($RT_NAME)"
    else
      warn "$RT_NAME did not come up within 90 seconds"
    fi
  else
    printf '    %s\n' "${DIM}Start your container runtime if the database check below fails.${RESET}"
  fi
fi

# ---------------------------------------------------------------------------
# .NET
# ---------------------------------------------------------------------------

head_ ".NET"

if prism_find_dotnet; then
  pass "SDK $(dotnet --version 2>/dev/null)"

  if dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.NETCore.App 9\.'; then
    pass "9.0 runtime present"
  else
    newest="$(dotnet --list-runtimes 2>/dev/null | grep 'Microsoft.NETCore.App' | tail -1 | awk '{print $2}')"
    pass "no 9.0 runtime; rolling forward to ${newest:-the newest major}"
    printf '    %s\n' "${DIM}backend/Directory.Build.props sets RollForward=Major, so net9.0 projects${RESET}"
    printf '    %s\n' "${DIM}run on it. Nothing to install.${RESET}"
  fi
else
  bad "no .NET SDK found"
  printf '    %s\n' "Install from https://dotnet.microsoft.com/download, or set DOTNET_ROOT."
  PROBLEMS=$((PROBLEMS + 1))
fi

# ---------------------------------------------------------------------------
# Node
# ---------------------------------------------------------------------------

head_ "Node"

if prism_find_node; then
  node_version="$(node --version 2>/dev/null)"
  major="$(printf '%s' "$node_version" | sed 's/^v//' | cut -d. -f1)"

  if [ -n "$major" ] && [ "$major" -ge 20 ] 2>/dev/null; then
    pass "node $node_version"
  else
    warn "node $node_version — 20 or newer is expected, CI runs 22"
  fi

  if prism_ensure_node_modules; then
    pass "frontend packages installed"
  else
    bad "frontend packages could not be installed"
    PROBLEMS=$((PROBLEMS + 1))
  fi
else
  bad "no Node.js found"
  printf '    %s\n' "Install Node 22 from https://nodejs.org, or run 'nvm use' in frontend/."
  PROBLEMS=$((PROBLEMS + 1))
fi

# ---------------------------------------------------------------------------
# Database
# ---------------------------------------------------------------------------

head_ "Test database"

if prism_ensure_database; then
  pass "reachable"
  printf '    %s\n' "${DIM}PRISM_TEST_DB=$PRISM_TEST_DB${RESET}"
  printf '\n    %s\n' "Add this to your shell profile so editors and GUI git clients see it too:"
  printf '      %s\n' "export PRISM_TEST_DB=\"$PRISM_TEST_DB\""
  printf '\n    %s\n' "${DIM}The fixture empties this database on every run. Do not point it at anything${RESET}"
  printf '    %s\n' "${DIM}you care about; a database named 'prism' is refused outright.${RESET}"
else
  bad "no database available"
  prism_database_options

  # The options are printed either way. When someone is actually sitting here,
  # offer to run the ones that are safe to run unattended — starting something
  # already installed. Installing a database server is left to the human, and
  # so is anything that needs credentials only they know.
  if [ -t 0 ] && [ -t 1 ]; then
    printf '\n'
    printf '  %s' "Run one of these now? Enter its number, or press Enter to skip: "
    read -r choice || choice=""

    # Only a plain positive integer is a choice. Without this guard, sed treats
    # arbitrary input as an address expression and returns something that looks
    # like an answer.
    case "$choice" in
      ''|*[!0-9]*) picked="" ;;
      *) picked="$(printf '%s\n' $PRISM_DB_OPTIONS | sed -n "${choice}p")" ;;
    esac

    if [ -n "$choice" ]; then

      case "$picked" in
        docker-start)
          printf '\n'
          RT_CMD="$(_prism_container_runtime | cut -d'|' -f3)"
          RT_NAME="$(_prism_container_runtime | cut -d'|' -f2)"
          if [ -n "$RT_CMD" ]; then
            printf '  %s' "starting ${RT_NAME:-the container runtime}"
            eval "$RT_CMD" >/dev/null 2>&1
            waited=0
            while [ "$waited" -lt 120 ] && ! docker info >/dev/null 2>&1; do
              printf '.'; sleep 3; waited=$((waited + 3))
            done
            printf '\n'
          fi
          if docker info >/dev/null 2>&1; then
            docker compose up -d postgres && sleep 3
          else
            bad "No daemon is responding yet — start it by hand and re-run"
          fi
          ;;
        docker-compose)
          printf '\n'
          docker compose up -d postgres && sleep 3
          ;;
        brew-start)
          formula="$(_prism_brew_postgres_formula)"
          printf '\n'
          brew services start "$formula"
          printf '  %s\n' "pgvector is a separate formula; installing it if it is missing."
          brew list --formula 2>/dev/null | grep -qx pgvector || brew install pgvector
          sleep 3
          ;;
        brew-install|apt-install)
          printf '\n  %s\n' "That one installs software, so it is left to you deliberately."
          printf '  %s\n'   "Copy the commands above, then re-run this script."
          ;;
        use-existing)
          printf '\n  %s\n' "That one needs credentials only you have, so it is yours to run."
          printf '  %s\n'   "Put the export in your shell profile too, so editors and GUI git"
          printf '  %s\n'   "clients see it and not only this terminal."
          ;;
        unit-only|no-verify)
          printf '\n  %s\n' "That is a command to run when you need it, not a setup step."
          printf '  %s\n'   "The database is still missing; this script will keep saying so."
          ;;
        *)
          printf '\n  %s\n' "Not one of the numbers listed; nothing done."
          ;;
      esac

      # Re-check, so the answer to "did that work" comes from the same run.
      printf '\n'
      if prism_ensure_database; then
        pass "database is now reachable"
        printf '    %s\n' "${DIM}PRISM_TEST_DB=$PRISM_TEST_DB${RESET}"
        printf '\n    %s\n' "Add this to your shell profile:"
        printf '      %s\n' "export PRISM_TEST_DB=\"$PRISM_TEST_DB\""
        DB_FIXED=1
      fi
    fi
  fi

  [ "${DB_FIXED:-0}" = "1" ] || PROBLEMS=$((PROBLEMS + 1))
fi

# ---------------------------------------------------------------------------
# Hooks
# ---------------------------------------------------------------------------

head_ "Git hooks"

configured="$(git config core.hooksPath 2>/dev/null)"
if [ "$configured" = ".githooks" ]; then
  pass "installed"
else
  warn "not installed — commits are not gated"
  printf '    %s\n' "Run:  ./scripts/install-hooks.sh"
fi

# ---------------------------------------------------------------------------

printf '\n'
if [ "$PROBLEMS" -eq 0 ]; then
  printf '%s\n\n' "${GREEN}${BOLD}Everything is ready.${RESET}"
  exit 0
fi

if [ "$PROBLEMS" -eq 1 ]; then
  printf '%s\n\n' "${RED}${BOLD}One thing needs your attention — see above.${RESET}"
else
  printf '%s\n\n' "${RED}${BOLD}$PROBLEMS things need your attention — see above.${RESET}"
fi
exit 1
