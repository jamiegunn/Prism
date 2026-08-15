#!/usr/bin/env bash
# Remove Claude from this repository's contributors.
#
# Rewrites every commit to (a) reattribute the 40 commits Claude authored or committed
# to you, and (b) strip the Co-Authored-By and Claude-Session trailers from the 75 that
# carry them. File content is never touched — the tree of every commit is identical
# before and after, which the script verifies before it finishes.
#
# This rewrites history. Every commit gets a new SHA, so the remote has to be
# force-pushed and anyone else holding a clone has to re-clone. You are the only
# contributor, so that is a one-line consequence rather than a coordination problem.
#
# Usage:  ./scrub-claude-attribution.sh /path/to/Prism
# Then, after reviewing the result:  git push --force-with-lease origin main

set -euo pipefail

REPO="${1:-$PWD}"
cd "$REPO"

# Who the reattributed commits become.
NEW_NAME="JG"
NEW_EMAIL="18316356+jamiegunn@users.noreply.github.com"

# Identities to replace. Matched on email OR exact name, because Claude committed under
# two different addresses and three different display names across the project's life.
#
# To also fold the misconfigured "Jamie Gunn <your_noreply@users.noreply.github.com>"
# identity into JG — it is a separate contributor on GitHub today, and that address is
# a placeholder that was never edited — add it to this list.
REWRITE_EMAILS="noreply@anthropic.com claude@anthropic.com"

step() { printf "\n\033[1;36m==> %s\033[0m\n" "$1"; }
fail() { printf "\n\033[1;31mFAIL: %s\033[0m\n" "$1" >&2; exit 1; }

[ -d .git ] || fail "Not a git repository: $REPO"
[ -z "$(git status --porcelain)" ] || fail "Working tree is dirty. Commit or stash first — a rewrite must start from a clean tree."

BEFORE_COUNT=$(git rev-list --count HEAD)
BEFORE_TREE=$(git rev-parse HEAD^{tree})
BEFORE_HEAD=$(git rev-parse HEAD)

step "Before"
echo "  commits: $BEFORE_COUNT"
echo "  HEAD:    $BEFORE_HEAD"
echo "  tree:    $BEFORE_TREE"
git log --format='%an <%ae>' | sort | uniq -c | sort -rn | sed 's/^/  /'

# Record the pre-rewrite tip. Deliberately NOT a tag: filter-branch rewrites every ref
# under refs/ when given `-- --all`, tags included, so a safety tag would quietly follow
# the rewrite and restore nothing. A plain file and the literal SHA cannot be rewritten.
echo "$BEFORE_HEAD" > .git/PRE_CLAUDE_SCRUB_HEAD
echo "  original HEAD recorded in .git/PRE_CLAUDE_SCRUB_HEAD"

# Every commit's tree, in order, so the verification below can prove that nothing in any
# commit changed — not just the tip.
git rev-list --reverse HEAD | while read -r c; do git rev-parse "$c^{tree}"; done > /tmp/.trees-before

step "Rewriting"

export REWRITE_EMAILS NEW_NAME NEW_EMAIL
export FILTER_BRANCH_SQUELCH_WARNING=1

git filter-branch -f --tag-name-filter cat \
  --env-filter '
    for e in $REWRITE_EMAILS; do
      if [ "$GIT_AUTHOR_EMAIL" = "$e" ]; then
        export GIT_AUTHOR_NAME="$NEW_NAME"
        export GIT_AUTHOR_EMAIL="$NEW_EMAIL"
      fi
      if [ "$GIT_COMMITTER_EMAIL" = "$e" ]; then
        export GIT_COMMITTER_NAME="$NEW_NAME"
        export GIT_COMMITTER_EMAIL="$NEW_EMAIL"
      fi
    done
  ' \
  --msg-filter '
    # Drop the attribution trailers, then collapse the trailing blank lines they leave
    # behind. sed on the whole message rather than per line so the tail is tidy.
    grep -v -e "^Co-Authored-By: Claude" \
            -e "^Claude-Session: " \
            -e "^🤖 Generated with \[Claude Code\]" \
      | awk "BEGIN{blank=0} {if (\$0 ~ /^[[:space:]]*\$/) {blank++} else {while(blank>0){print \"\"; blank--}; print}}"
  ' \
  -- --all

step "After"
AFTER_COUNT=$(git rev-list --count HEAD)
AFTER_TREE=$(git rev-parse HEAD^{tree})
echo "  commits: $AFTER_COUNT"
echo "  tree:    $AFTER_TREE"
git log --format='%an <%ae>' | sort | uniq -c | sort -rn | sed 's/^/  /'

step "Verifying"

[ "$AFTER_COUNT" = "$BEFORE_COUNT" ] \
  || fail "Commit count changed: $BEFORE_COUNT -> $AFTER_COUNT. History was not meant to be reshaped, only relabelled."
echo "  commit count unchanged ($AFTER_COUNT)"

# The whole point: relabelling must not alter a single byte of content, in any commit.
[ "$AFTER_TREE" = "$BEFORE_TREE" ] \
  || fail "HEAD tree changed: $BEFORE_TREE -> $AFTER_TREE. Content was modified — do not push; run: git reset --hard $BEFORE_HEAD"
echo "  HEAD tree identical"

git rev-list --reverse HEAD | while read -r c; do git rev-parse "$c^{tree}"; done > /tmp/.trees-after
if ! diff -q /tmp/.trees-before /tmp/.trees-after >/dev/null; then
  rm -f /tmp/.trees-before /tmp/.trees-after
  fail "A commit's tree changed. Content was modified — do not push; run: git reset --hard $BEFORE_HEAD"
fi
rm -f /tmp/.trees-before /tmp/.trees-after
echo "  all $AFTER_COUNT commit trees identical — no file content changed anywhere"

remaining_authors=$(git log --format='%an <%ae>%n%cn <%ce>' | grep -ci "claude\|anthropic" || true)
[ "$remaining_authors" = "0" ] \
  || fail "$remaining_authors author/committer fields still name Claude."
echo "  no Claude author or committer remains"

remaining_trailers=$(git log --format='%B' | grep -c "^Co-Authored-By: Claude\|^Claude-Session: " || true)
[ "$remaining_trailers" = "0" ] \
  || fail "$remaining_trailers attribution trailers remain."
echo "  no attribution trailers remain"

cat <<EOF

──────────────────────────────────────────────────────────────
Done, and verified: same commits, same content, no Claude.

Review it:
  git log --format='%h %an <%ae> %s' | head -20

Then publish. --force-with-lease refuses if the remote moved since your
last fetch, which plain --force would happily overwrite:
  git push --force-with-lease origin main

If anything looks wrong, this undoes all of it (before you push, and before gc):
  git reset --hard $BEFORE_HEAD

Once you are happy, clean up:
  rm -f .git/PRE_CLAUDE_SCRUB_HEAD
  git for-each-ref --format='%(refname)' refs/original | xargs -n1 git update-ref -d
  git reflog expire --expire=now --all && git gc --prune=now --aggressive
──────────────────────────────────────────────────────────────
EOF
