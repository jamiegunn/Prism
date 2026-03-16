#!/bin/bash
# JupyterLite Setup for Prism AI Research Workbench
#
# This script builds JupyterLite static assets and deploys them
# to the frontend's public directory for iframe embedding.
#
# Prerequisites:
#   pip install jupyterlite-core jupyterlite-pyodide-kernel
#
# Usage:
#   cd frontend/public/jupyterlite
#   bash setup.sh

set -e

echo "Building JupyterLite..."

# Build JupyterLite with Pyodide kernel
jupyter lite build --output-dir ./output

echo ""
echo "JupyterLite built successfully!"
echo ""
echo "The JupyterLite app is now available at /jupyterlite/output/"
echo "Update the frontend to point to /jupyterlite/output/lab/index.html"
echo ""
echo "To include the workbench module, copy workbench.py into a notebook"
echo "or configure JupyterLite contents to auto-include it."
