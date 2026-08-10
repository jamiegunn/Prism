#!/bin/bash
# JupyterLite build for the Prism Notebooks page.
#
# The Notebooks page embeds JupyterLite at /jupyterlite/lab/index.html. That build is generated,
# not committed — it is tens of megabytes — so CI runs this before `npm run build` and the
# assets ship with the bundle. Run it locally if you want the embed to work in `npm run dev`.
#
#   pip install jupyterlite-core jupyterlite-pyodide-kernel
#   bash frontend/public/jupyterlite/setup.sh
#
# It builds in place rather than into ./output. The previous version built to ./output and then
# told you to repoint the frontend at /jupyterlite/output/lab/index.html — advice that
# contradicted the two paths hardcoded in the page, so following either left the iframe broken.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE"

if ! command -v jupyter >/dev/null 2>&1; then
  echo "jupyter not found. Install the build tools first:" >&2
  echo "  pip install jupyterlite-core jupyterlite-pyodide-kernel" >&2
  exit 1
fi

echo "Building JupyterLite into $HERE ..."

# --contents ships workbench.py inside the environment, so `import workbench` resolves in a
# notebook instead of being a snippet nobody can run.
jupyter lite build --output-dir . --contents workbench.py

if [ ! -f "lab/index.html" ]; then
  echo "Build finished but lab/index.html is missing — the page embeds that exact path." >&2
  exit 1
fi

echo "Done. The page embeds /jupyterlite/lab/index.html, which now exists."
