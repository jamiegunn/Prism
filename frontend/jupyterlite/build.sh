#!/bin/bash
# Builds the JupyterLite kernel the Notebooks page embeds.
#
# The page loads /jupyterlite/lab/index.html, which means the build has to land in
# frontend/public/jupyterlite/. That directory is generated in its entirety and is gitignored:
# `jupyter lite build` **wipes its output directory first**, so anything hand-written in there is
# destroyed. That is why the sources live here, in frontend/jupyterlite/, and not beside the
# output — an earlier version kept them together and the first successful build deleted itself.
#
# Run it with `npm run jupyterlite`, or let `dev.sh` run it on first start.
#
# It manages its own virtual environment. Homebrew and most current distributions mark the
# system Python as externally managed, so a bare `pip install` is refused — and installing build
# tools for one optional feature into someone's global Python is rude even where it is allowed.
#
# Everything except running notebook cells works without this. The page detects an absent build
# and says so, so skipping it is a degraded feature rather than a silent failure.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONTEND="$(cd "$HERE/.." && pwd)"
OUTPUT="$FRONTEND/public/jupyterlite"
VENV="$HERE/.venv"

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required to build the JupyterLite kernel." >&2
  exit 1
fi

if [ ! -x "$VENV/bin/jupyter" ]; then
  echo "Creating a build environment in $VENV ..."
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install --quiet --upgrade pip
  # jupyter-server is what lets --contents ship workbench.py into the environment; without it
  # the build fails outright rather than quietly omitting the file.
  "$VENV/bin/pip" install --quiet jupyterlite-core jupyterlite-pyodide-kernel jupyter-server
fi

mkdir -p "$OUTPUT"

echo "Building JupyterLite into $OUTPUT ..."

# Run from here so --contents picks up workbench.py, which ships it inside the environment and
# makes `import workbench` resolve in a notebook rather than being a snippet with no runtime.
cd "$HERE"
"$VENV/bin/jupyter" lite build --output-dir "$OUTPUT" --contents workbench.py

if [ ! -f "$OUTPUT/lab/index.html" ]; then
  echo "Build finished but $OUTPUT/lab/index.html is missing — the page embeds that path." >&2
  exit 1
fi

echo "Done. The page embeds /jupyterlite/lab/index.html, which now exists."
