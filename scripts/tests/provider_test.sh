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
echo "-------------------------------------------"
echo "  provider: $PASS passed, $FAIL failed"
exit $(( FAIL > 0 ))
