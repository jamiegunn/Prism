#!/usr/bin/env bash
# Exercises the REAL configure() out of dev.sh, with only the outside world faked.
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$HERE/harness.sh"

DEVSH="${DEVSH:-$REPO/dev.sh}"
CUT=$(( $(grep -n '^# Source dev-env early' "$DEVSH" | cut -d: -f1) - 1 ))
PREFIX="$WORK/dev_prefix.sh"
sed -n "1,${CUT}p" "$DEVSH" > "$PREFIX"

# run_menu <os> <container> <native-running> <saved-provider> <keystrokes...>
run_menu() {
  local os="$1" container="$2" native="$3" saved="$4"; shift 4
  world "$container" "$native" 1 1
  rm -f "$WORK/.prism-dev.conf"
  export FAKE_OS="$os"
  MENU_OUT="$(
    export FAKE_OS="$os" FAKE_ARCH="${ARCH:-arm64}" OPEN_PORTS="${OPEN_PORTS:-}" \
           PRISM_PROVIDER="$saved" STATE PATH WORK H="$HERE" P="$PREFIX"
    printf '%s\n' "$@" | bash -c '
      cd "$WORK"
      . "$H/harness.sh"   # stubs + PATH
      set --              # dev.sh parses "$@"; the harness must not leak args in
      . "$P"              # dev.sh, up to the end of configure()
      configure
      echo "CHOSE=$PRISM_PROVIDER"
    ' 2>&1
  )"
}

echo ""
echo "=== 1. macOS, container holds 11434, native Ollama installed ==="
echo "    (the reported scenario: user wants to switch to Metal)"
run_menu Darwin 1 0 later "" "1" "" ""
printf '%s\n' "$MENU_OUT" | sed -n '/Which model should Prism read/,/^ *> *$/p' | sed 's/^/        /'
check "the native option is offered at all"        "Start the Ollama you have installed" "$MENU_OUT"
check "it says it will stop the container"          "stops the container, which holds the port" "$MENU_OUT"
check "the detected option names it as a container" "Use the Ollama container already on 11434" "$MENU_OUT"
check "native is listed first (likely-fastest)"     "1) Start the Ollama you have installed" "$MENU_OUT"
check "picking option 1 selects start-ollama"       "CHOSE=start-ollama" "$MENU_OUT"

echo ""
echo "=== 2. Same, with start-ollama saved from last run ==="
run_menu Darwin 1 0 start-ollama "" "" ""
check "saved answer survives as the default"        "CHOSE=start-ollama" "$MENU_OUT"
refute "not silently downgraded to the container"   "CHOSE=detected-ollama" "$MENU_OUT"

echo ""
echo "=== 3. macOS, a NATIVE Ollama already holds 11434 ==="
run_menu Darwin 0 1 later "" "" ""
check "detected option says 'natively'"             "already running natively on port 11434" "$MENU_OUT"
refute "no redundant 'start it' option"             "Start the Ollama you have installed" "$MENU_OUT"

echo ""
echo "=== 4. macOS, nothing on 11434 ==="
run_menu Darwin 0 0 later "" "1" "" ""
check "native offered"                              "Start the Ollama you have installed" "$MENU_OUT"
check "with the Metal wording"                      "uses the Apple GPU via Metal" "$MENU_OUT"
refute "nothing claimed to be detected"             "detected-ollama" "$MENU_OUT"
check "and it is chosen"                            "CHOSE=start-ollama" "$MENU_OUT"

echo ""
echo "=== 5. Linux, container holds 11434, native installed ==="
run_menu Linux 1 0 later "" "" ""
check "native still offered"                        "Start the Ollama you have installed" "$MENU_OUT"
refute "no Metal wording off macOS"                 "Apple GPU" "$MENU_OUT"
check "container stays first (it has the GPU too)"  "1) Use the Ollama container already running" "$MENU_OUT"

echo ""
echo "=== 6. Intel Mac (x86_64), nothing on 11434 ==="
ARCH=x86_64 run_menu Darwin 0 0 later "" "1" "" ""
check "native still offered"                        "Start the Ollama you have installed" "$MENU_OUT"
refute "but no Metal claim — there is no Metal here" "Apple GPU via Metal" "$MENU_OUT"

echo ""
echo "=== 7. Apple Silicon still gets the Metal wording ==="
ARCH=arm64 run_menu Darwin 0 0 later "" "1" "" ""
check "Metal claimed on arm64"                      "uses the Apple GPU via Metal" "$MENU_OUT"

echo ""
echo "=== 8. LM Studio running on 1234, nothing else ==="
OPEN_PORTS="1234" run_menu Darwin 0 0 later "" "2" "" ""
printf '%s\n' "$MENU_OUT" | sed -n '/Which model should Prism read/,/^ *> *$/p' | sed 's/^/        /'
check "LM Studio is detected and offered"      "Use the LM Studio already running on port 1234" "$MENU_OUT"
check "and it is selectable"                   "CHOSE=detected-lmstudio" "$MENU_OUT"

echo ""
echo "=== 9. vLLM on 8000 still detected (no regression) ==="
OPEN_PORTS="8000" run_menu Linux 0 0 later "" "2" "" ""
check "vLLM detected"                          "Use the vLLM already running on port 8000" "$MENU_OUT"
check "and selectable"                         "CHOSE=detected-vllm" "$MENU_OUT"

echo ""
echo "-------------------------------------------"
echo "  menu: $PASS passed, $FAIL failed"
exit $(( FAIL > 0 ))
