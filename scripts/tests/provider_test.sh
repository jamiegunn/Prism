#!/usr/bin/env bash
# Exercises the REAL provider case-block out of dev.sh, sliced verbatim.
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$HERE/harness.sh"

DEVSH="${DEVSH:-$REPO/dev.sh}"
CUT=$(( $(grep -n '^# Source dev-env early' "$DEVSH" | cut -d: -f1) - 1 ))
PREFIX="$WORK/dev_prefix.sh"
BLOCK="$WORK/dev_block.sh"
sed -n "1,${CUT}p" "$DEVSH" > "$PREFIX"

START=$(grep -n '^if ! \$FRONTEND_ONLY; then' "$DEVSH" | head -1 | cut -d: -f1)
END=$(awk -v s="$START" 'NR>s && /^  esac$/ {print NR; exit}' "$DEVSH")
sed -n "${START},$((END + 1))p" "$DEVSH" > "$BLOCK"
bash -n "$BLOCK" || { echo "extracted block does not parse"; exit 1; }

# run_provider <provider> <container> <native> <stop_works> <has_model>
run_provider() {
  local provider="$1"
  world "$2" "$3" "$4" "$5"
  RUN_OUT="$(
    export STATE PATH WORK H="$HERE" B="$BLOCK" P="$PREFIX" PROV="$provider"
    bash -c '
      cd "$WORK"
      . "$H/harness.sh"
      set --
      . "$P"
      sleep() { command sleep 0.2; }   # keep the retry loops quick
      FRONTEND_ONLY=false
      PRISM_PROVIDER="$PROV"
      PRISM_MODEL="mistral:7b-instruct"
      PROVIDER_ENDPOINT=""; PROVIDER_TYPE=""
      . "$B"
      echo "ENDPOINT=$PROVIDER_ENDPOINT TYPE=$PROVIDER_TYPE"
      . "$STATE"; echo "FINAL_CONTAINER=$CONTAINER FINAL_NATIVE=$NATIVE"
    ' 2>&1
  )"
  pkill -f 'bin/ollama serve' 2>/dev/null || true
}

echo ""
echo "=== A. start-ollama with the container holding the port (stop succeeds) ==="
run_provider start-ollama 1 0 1 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "notices the container has the port"   "container is holding port 11434" "$RUN_OUT"
check "stops it"                             "FINAL_CONTAINER=0" "$RUN_OUT"
check "starts the native one"                "FINAL_NATIVE=1" "$RUN_OUT"
check "endpoint registered"                  "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"

echo ""
echo "=== B. start-ollama, but stopping the container FAILS ==="
run_provider start-ollama 1 0 0 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
refute "must NOT claim it is running natively" "already running natively" "$RUN_OUT"
check  "says plainly this is the container"    "still holds port 11434, so this is the container, not Metal" "$RUN_OUT"
check  "tells you how to fix it"               "docker compose --profile ollama stop ollama" "$RUN_OUT"
check  "still registers a usable endpoint"     "ENDPOINT=http://localhost:11434" "$RUN_OUT"

echo ""
echo "=== C. container-ollama while a NATIVE Ollama holds the port (no model yet) ==="
run_provider container-ollama 0 1 1 0
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "warns about the conflict"             "Something already holds port 11434" "$RUN_OUT"
check "pulls the model into the native one"  "PULLED mistral:7b-instruct" "$RUN_OUT"
check "endpoint registered"                  "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"
refute "does not start a container it cannot reach" "FINAL_CONTAINER=1" "$RUN_OUT"

echo ""
echo "=== D. container-ollama with nothing running (the normal path) ==="
run_provider container-ollama 0 0 1 1
check "starts the container"                 "FINAL_CONTAINER=1" "$RUN_OUT"
check "endpoint registered"                  "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"
refute "no spurious conflict warning"        "Something already holds" "$RUN_OUT"

echo ""
echo "=== E. start-ollama with nothing running (the normal path) ==="
run_provider start-ollama 0 0 1 1
check "starts native"                        "FINAL_NATIVE=1" "$RUN_OUT"
check "endpoint registered"                  "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"
refute "no spurious container talk"          "container is holding" "$RUN_OUT"

echo ""
echo "=== F. detected-ollama, but it is gone and a native one is installed ==="
# The saved answer names a server that answered last time. Warning and carrying
# on left Prism with no inference at all, which is the launcher failing at the
# one job it exists to do.
run_provider detected-ollama 0 0 1 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check  "says what went missing"        "Nothing is answering on port 11434" "$RUN_OUT"
check  "starts the installed Ollama"   "FINAL_NATIVE=1" "$RUN_OUT"
check  "ends with a usable endpoint"   "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"
refute "does not send you to the UI"   "connect a model from the Models page" "$RUN_OUT"

echo ""
echo "=== G. detected-ollama gone, nothing installed, but a container runtime is there ==="
# A machine with a container runtime and no ollama of its own: the fake binary
# is removed from PATH, and the container reaches its own copy the way a real
# `docker exec` would.
run_provider_no_native() {
  world 0 0 1 "$2"
  RUN_OUT="$(
    export STATE PATH WORK H="$HERE" B="$BLOCK" P="$PREFIX" PROV="$1"
    bash -c '
      cd "$WORK"
      . "$H/harness.sh"
      rm -f "$WORK/bin/ollama"
      set --
      . "$P"
      sleep() { command sleep 0.2; }
      FRONTEND_ONLY=false
      PRISM_PROVIDER="$PROV"
      PRISM_MODEL="mistral:7b-instruct"
      PROVIDER_ENDPOINT=""; PROVIDER_TYPE=""
      . "$B"
      echo "ENDPOINT=$PROVIDER_ENDPOINT TYPE=$PROVIDER_TYPE"
      . "$STATE"; echo "FINAL_CONTAINER=$CONTAINER FINAL_NATIVE=$NATIVE"
    ' 2>&1
  )"
}
run_provider_no_native detected-ollama 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "falls back to the container"    "FINAL_CONTAINER=1" "$RUN_OUT"
check "ends with a usable endpoint"    "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"

echo ""
echo "=== H. detected-ollama present but empty — a server with no model ==="
# "Already running" is exactly the case nobody checked for a model, and an
# Ollama with an empty model list gives the same empty screens as no Ollama.
run_provider detected-ollama 0 1 1 0
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check  "uses the running server"       "ENDPOINT=http://localhost:11434 TYPE=Ollama" "$RUN_OUT"
check  "pulls a model into it"         "PULLED mistral:7b-instruct" "$RUN_OUT"
refute "does not restart anything"     "Starting Ollama in a container" "$RUN_OUT"

echo ""
echo "=== I. detected-ollama present with a model — nothing to do ==="
run_provider detected-ollama 1 0 1 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check  "keeps the model it has"        "A model is already available" "$RUN_OUT"
refute "pulls nothing"                 "PULLED" "$RUN_OUT"

echo ""
echo "=== J. container-ollama on a machine that already has the models ==="
# A network that inspects TLS breaks the container's download and leaves the
# host's alone, so the models sitting in ~/.ollama are the way in. Mounting
# them beats copying gigabytes, and beats failing.
run_with_host_store() {
  local blobs="$1"
  RUN_OUT="$(
    export STATE PATH WORK H="$HERE" B="$BLOCK" P="$PREFIX" BLOBS="$blobs"
    bash -c '
      cd "$WORK"
      . "$H/harness.sh"
      set --
      . "$P"
      sleep() { command sleep 0.2; }
      export HOME="$WORK/fakehome"
      rm -rf "$HOME"; mkdir -p "$HOME/.ollama/models/blobs"
      [ "$BLOBS" = 1 ] && : > "$HOME/.ollama/models/blobs/sha256-abc"
      FRONTEND_ONLY=false
      PRISM_PROVIDER="container-ollama"
      PRISM_MODEL="mistral:7b-instruct"
      PROVIDER_ENDPOINT=""; PROVIDER_TYPE=""
      . "$B"
      echo "MOUNTED=${PRISM_OLLAMA_MODELS:-none}"
    ' 2>&1
  )"
}
run_with_host_store 1
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "says it is using what is here"  "Using the models already on this machine" "$RUN_OUT"
check "mounts the host store"          "/fakehome/.ollama" "$RUN_OUT"

echo ""
echo "=== K. container-ollama on a machine with an empty model store ==="
run_with_host_store 0
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check  "keeps the named volume"        "MOUNTED=none" "$RUN_OUT"
refute "claims nothing about models"   "Using the models already on this machine" "$RUN_OUT"

echo ""
echo "-------------------------------------------"
echo "  provider: $PASS passed, $FAIL failed"
exit $(( FAIL > 0 ))
