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
  exec "$@"
fi
exit 0
DOCKER

cat > "$BIN/ollama" <<'OLLAMA'
#!/usr/bin/env bash
. "$STATE"
case "$1" in
  serve) sed -i.bak 's/^NATIVE=0/NATIVE=1/' "$STATE"; sleep 30 ;;
  list)
    echo "NAME  ID  SIZE"
    [ "$HAS_MODEL" = 1 ] && echo "mistral:7b  abc  4GB"
    ;;
  pull) echo "PULLED $2" ;;
esac
exit 0
OLLAMA

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
