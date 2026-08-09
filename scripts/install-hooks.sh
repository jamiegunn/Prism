#!/usr/bin/env bash
#
# Points git at the version-controlled hooks in .githooks/.
#
# Hooks live in the repo rather than in .git/hooks so that everyone gets the
# same gate and it can be reviewed like any other code. Git does not do this
# automatically — core.hooksPath has to be set once per clone, which is what
# this script does.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null || true

cat <<'EOF'

Hooks installed.

  pre-commit   builds, format-checks and tests whichever half of the repo you
               touched. A docs-only commit runs nothing.

The backend tests need a database. Either:

    docker compose up -d
    export PRISM_TEST_DB="Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres"

or leave PRISM_TEST_DB unset and let the tests start their own container,
which needs a running Docker daemon.

Put the export in your shell profile so it survives new terminals, and so it
is set when you commit from an editor or GUI client.

To bypass the gate for one commit:  git commit --no-verify
To uninstall:                       git config --unset core.hooksPath

EOF
