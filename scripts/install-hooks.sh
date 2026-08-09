#!/usr/bin/env bash
#
# Points git at the version-controlled hooks in .githooks/, then checks that the
# machine can actually run them.
#
# Hooks live in the repo rather than in .git/hooks so that everyone gets the
# same gate and it can be reviewed like any other code. Git does not do this
# automatically — core.hooksPath has to be set once per clone, which is what
# this script does.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

git config core.hooksPath .githooks
chmod +x .githooks/* scripts/*.sh 2>/dev/null || true

echo "Hooks installed. The pre-commit gate builds, format-checks and tests"
echo "whichever half of the repo you touched; a docs-only commit runs nothing."
echo ""
echo "Checking this machine can run them..."

# Running the doctor here means the first failure you see is on install, when
# you are expecting to configure things — not three days later, halfway through
# a commit you were in a hurry to make.
if ./scripts/doctor.sh; then
  echo "To bypass the gate for one commit:  git commit --no-verify"
  echo "To uninstall:                       git config --unset core.hooksPath"
else
  echo ""
  echo "The hooks are installed, but the checks above need attention first."
  echo "Re-run ./scripts/doctor.sh once you have dealt with them."
  exit 1
fi
