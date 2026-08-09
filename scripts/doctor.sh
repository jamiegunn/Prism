#!/usr/bin/env bash
#
# Checks everything Prism needs to build and test, and fixes what it can.
#
#     ./scripts/doctor.sh
#
# Unlike the pre-commit hook, this is allowed to be slow and to start Docker
# Desktop, because you are sitting in front of it. Run it after cloning, after
# a machine rebuild, or any time the hook tells you something is missing.

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
  if [ "$(uname -s)" = "Darwin" ] && [ -d /Applications/Docker.app ]; then
    printf '    %s' "starting Docker Desktop"
    open -a Docker >/dev/null 2>&1
    waited=0
    while [ "$waited" -lt 90 ]; do
      if docker info >/dev/null 2>&1; then break; fi
      printf '.'
      sleep 3
      waited=$((waited + 3))
    done
    printf '\n'
    if docker info >/dev/null 2>&1; then
      pass "daemon is running"
    else
      warn "Docker did not come up within 90 seconds"
    fi
  else
    printf '    %s\n' "${DIM}Start it if the database check below fails.${RESET}"
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
  prism_env_explain
  PROBLEMS=$((PROBLEMS + 1))
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
