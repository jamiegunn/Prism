#!/usr/bin/env bash
# Exercises the REAL model-provisioning functions out of dev.sh.
#
# The bug these are written against: ensure_ollama_model asked whether the
# server had *any* model and returned early if it did. Prism needs two different
# things — one that chats and one that embeds — so on every machine that already
# had a chat model, the embedding model was never fetched. RAG could not answer
# a single semantic query on any install: the sample collection was seeded
# unembedded, vector search filtered every chunk out, and the screen said
# nothing had matched.
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$HERE/harness.sh"

DEVSH="${DEVSH:-$REPO/dev.sh}"

# Slice the provisioning functions out of dev.sh and run the real code.
FUNCS="$WORK/model_funcs.sh"
START=$(grep -n '^# Whether a specific model is on the server' "$DEVSH" | cut -d: -f1)
END=$(grep -n '^# Whether the Ollama container Prism starts' "$DEVSH" | cut -d: -f1)
sed -n "${START},$((END - 1))p" "$DEVSH" > "$FUNCS"
bash -n "$FUNCS" || { echo "extracted functions do not parse"; exit 1; }

# prism_version_at_least lives further up and the logprobs warning calls it.
sed -n "/^prism_version_at_least() {/,/^}/p" "$DEVSH" >> "$FUNCS"

# run_models <has_model 0|1> [chat] [embed]
run_models() {
  world 0 1 1 "$1"
  rm -f "$WORK/pulled"
  RUN_OUT="$(
    export STATE PATH WORK H="$HERE" F="$FUNCS" CHAT="${2-}" EMBED="${3-}"
    bash -c '
      cd "$WORK"
      . "$H/harness.sh"
      set --
      step() { echo "=> $1"; }; ok() { echo "   $1"; }; warn() { echo "   $1"; }
      PRISM_MODEL="$CHAT"
      PRISM_EMBED_MODEL="$EMBED"
      . "$F"
      ensure_ollama_models ""
      echo "PULLED_LIST=$(tr "\n" "," < "$WORK/pulled" 2>/dev/null)"
    ' 2>&1
  )"
}

echo ""
echo "=== A. an empty server gets both a chat model and an embedding model ==="
run_models 0 "mistral:7b-instruct" "nomic-embed-text"
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "pulls the chat model"      "PULLED mistral:7b-instruct" "$RUN_OUT"
check "pulls the embedding model" "PULLED nomic-embed-text"    "$RUN_OUT"

echo ""
echo "=== B. a server that already has a chat model still gets the embedding one ==="
# The original defect exactly: one model present, so nothing was ever pulled.
run_models 1 "mistral:7b-instruct" "nomic-embed-text"
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check  "leaves the chat model alone"    "mistral:7b-instruct is already available" "$RUN_OUT"
check  "still pulls the embedding model" "PULLED nomic-embed-text" "$RUN_OUT"
refute "does not re-pull the chat model" "PULLED mistral:7b-instruct" "$RUN_OUT"

echo ""
echo "=== C. nothing is pulled twice when both are already there ==="
run_models 0 "mistral:7b-instruct" "nomic-embed-text"
FIRST="$RUN_OUT"
RUN_OUT="$(
  export STATE PATH WORK H="$HERE" F="$FUNCS"
  bash -c '
    cd "$WORK"
    . "$H/harness.sh"
    set --
    step() { echo "=> $1"; }; ok() { echo "   $1"; }; warn() { echo "   $1"; }
    PRISM_MODEL="mistral:7b-instruct"
    PRISM_EMBED_MODEL="nomic-embed-text"
    . "$F"
    ensure_ollama_models ""
  ' 2>&1
)"
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
refute "pulls nothing on the second run" "PULLED" "$RUN_OUT"
check  "recognises the tagged embedding model" "nomic-embed-text is already available" "$RUN_OUT"

echo ""
echo "=== D. an unset embedding model still gets a working default ==="
# A blank here used to mean "no embeddings", silently. RAG has no fallback for
# that, so the default is part of the contract rather than a convenience.
run_models 1 "mistral:7b-instruct" ""
printf '%s\n' "$RUN_OUT" | sed 's/^/        /'
check "defaults to nomic-embed-text" "PULLED nomic-embed-text" "$RUN_OUT"

echo ""
echo "-------------------------------------------"
echo "  models: $PASS passed, $FAIL failed"
exit $(( FAIL > 0 ))
