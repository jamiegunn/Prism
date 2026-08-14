#!/usr/bin/env bash
# Shared test harness: fake `docker` and `ollama` binaries on PATH plus a
# mutable world-state file, so the real dev.sh code decides what to do.

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${REPO:-$(cd "$HERE/../.." && pwd)}"
WORK="${WORK:-$(mktemp -d)}"
export WORK
STATE="$WORK/state.env"
BIN="$WORK/bin"

# world <container 0|1> <native 0|1> <stop_works 0|1> <has_model 0|1>
world() {
  # Pulled models are world state too: without this they leak from one scenario
  # into the next, and a test that sets up an empty server silently gets one
  # holding whatever the previous scenario downloaded.
  rm -f "$WORK/pulled"

  cat > "$STATE" <<EOF
CONTAINER=$1
NATIVE=$2
STOP_WORKS=$3
HAS_MODEL=$4
EOF
}

mkdir -p "$BIN"

cat > "$BIN/docker" <<'DOCKER'
#!/usr/bin/env bash
. "$STATE"
case "$1 $2" in
  "ps --filter")
    [ "$CONTAINER" = 1 ] && echo prism-ollama
    exit 0 ;;
esac
if [ "$1" = compose ]; then
  case "$*" in
    *"stop ollama"*)
      if [ "$STOP_WORKS" = 1 ]; then
        sed -i.bak 's/^CONTAINER=1/CONTAINER=0/' "$STATE"; exit 0
      fi
      exit 1 ;;
    *"up -d ollama"*)
      sed -i.bak 's/^CONTAINER=0/CONTAINER=1/' "$STATE"; exit 0 ;;
  esac
  exit 0
fi
if [ "$1" = exec ]; then          # docker exec prism-ollama ollama <cmd>
  shift 2
  # Resolved by path rather than through PATH, so a test can model a machine
  # that has a container runtime and no ollama binary of its own — which is
  # what "nothing installed, Docker available" actually looks like.
  if [ "$1" = ollama ]; then shift; exec "$WORK/ollama-impl" "$@"; fi
  exec "$@"
fi
exit 0
DOCKER

cat > "$WORK/ollama-impl" <<'OLLAMA'
#!/usr/bin/env bash
. "$STATE"
PULLED="$WORK/pulled"
case "$1" in
  serve) sed -i.bak 's/^NATIVE=0/NATIVE=1/' "$STATE"; sleep 30 ;;
  list)
    echo "NAME  ID  SIZE"
    [ "$HAS_MODEL" = 1 ] && echo "mistral:7b-instruct  abc  4GB"
    # A pulled model is one the server has from then on, which is what makes
    # "did it pull the same thing twice" an answerable question.
    [ -f "$PULLED" ] && cat "$PULLED"
    ;;
  pull)
    echo "PULLED $2"
    # Ollama stores an untagged pull as name:latest; recording it any other way
    # would let a bug that ignores the tag pass here and fail on a real machine.
    case "$2" in
      *:*) printf '%s  abc  1GB\n' "$2" >> "$PULLED" ;;
      *)   printf '%s:latest  abc  1GB\n' "$2" >> "$PULLED" ;;
    esac
    ;;
  --version) echo "ollama version is 0.32.6" ;;
esac
exit 0
OLLAMA

chmod +x "$WORK/ollama-impl"
cp "$WORK/ollama-impl" "$BIN/ollama"

cat > "$BIN/uname" <<'UNAME'
#!/usr/bin/env bash
if [ "$1" = "-m" ]; then echo "${FAKE_ARCH:-arm64}"; else echo "${FAKE_OS:-Darwin}"; fi
UNAME

chmod +x "$BIN/docker" "$BIN/ollama" "$BIN/uname"
export STATE
export PATH="$BIN:$PATH"

# Ports cannot be opened for real here, so this is the one stub standing in for
# the outside world. Everything else is dev.sh's own code.
_prism_port_open() {
  # shellcheck disable=SC1090
  . "$STATE"
  if [ "$2" = 11434 ]; then
    [ "$CONTAINER" = 1 ] || [ "$NATIVE" = 1 ]
    return
  fi
  # Any other port is open only if listed in OPEN_PORTS.
  case " ${OPEN_PORTS:-} " in *" $2 "*) return 0 ;; esac
  return 1
}

# dev-env.sh sizing helpers, not under test here.
prism_runtime_memory_gib() { echo 16; }
prism_host_memory_gib()    { echo 16; }
prism_recommended_model()  { echo "mistral:7b-instruct"; }
prism_recommended_memory_gib() { echo 8; }
prism_runtime_cpus()       { echo 4; }

PASS=0; FAIL=0
check() { # check <description> <expected-substring> <haystack>
  if printf '%s' "$3" | grep -qF -- "$2"; then
    echo "   PASS  $1"; PASS=$((PASS + 1))
  else
    echo "   FAIL  $1"; echo "         wanted to find: $2"; FAIL=$((FAIL + 1))
  fi
}
refute() {
  if printf '%s' "$3" | grep -qF -- "$2"; then
    echo "   FAIL  $1"; echo "         should NOT contain: $2"; FAIL=$((FAIL + 1))
  else
    echo "   PASS  $1"; PASS=$((PASS + 1))
  fi
}
