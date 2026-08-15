#!/usr/bin/env bash
# Prepare the .NET toolchain + package graph for Claude's cloud sandbox.
#
# WHY: the sandbox can reach GitHub, PyPI, npm and Ubuntu's archive — but every
# Microsoft and NuGet host is blocked, and Ubuntu only ships .NET SDK 8 while
# Prism targets net9.0. So the bits have to travel through the connected folder.
#
# BOTH Linux architectures are produced, because there are two sandboxes and they
# do not match: the cloud container is x86_64, while the workspace VM that mounts
# your folders is aarch64. An x64 SDK in the VM fails with "cannot execute binary
# file", which reads like a corrupt download rather than an architecture mismatch.
#
# Run this on your Mac from anywhere. It writes into <repo>/toolchain/.
# Requires: .NET 9 SDK on your Mac (which you need for local dev anyway).

set -euo pipefail

REPO="${1:-$HOME/dev/Prism}"
OUT="$REPO/toolchain"
TMPPKG="$(mktemp -d)"
RIDS=(linux-x64 linux-arm64)   # both sandboxes; NOT your Mac's arch. Intentional.

step() { printf "\n\033[1;36m==> %s\033[0m\n" "$1"; }
fail() { printf "\n\033[1;31mFAIL: %s\033[0m\n" "$1" >&2; exit 1; }

step "Checking prerequisites"
command -v dotnet >/dev/null || fail "dotnet not found. Install a .NET SDK (9 or newer): https://dotnet.microsoft.com/download/dotnet"
[ -d "$REPO/backend/src" ] || fail "Not a Prism repo: $REPO"

# Any SDK >= 9 works here. This machine's restore is only a package *downloader*
# — the sandbox regenerates project.assets.json itself, so SDK skew is harmless.
SDK_MAJOR="$(dotnet --version | cut -d. -f1)"
[ "$SDK_MAJOR" -ge 9 ] 2>/dev/null || fail "Need SDK 9 or newer. Installed: $(dotnet --list-sdks | tr '\n' ' ')"
echo "Local SDK: $(dotnet --version) (major $SDK_MAJOR)"

# What the solution actually targets — determines which runtime the sandbox needs.
# Prism sets this in backend/Directory.Build.props, not in the individual csproj
# files — search both rather than assuming either.
TFM="$(grep -rho '<TargetFramework>net[0-9.]*</TargetFramework>' \
        "$REPO/backend" --include='*.csproj' --include='*.props' 2>/dev/null \
       | head -1 | sed 's/.*>net\(.*\)<.*/\1/')"
[ -n "$TFM" ] || fail "Could not determine the target framework under $REPO/backend"
echo "Project targets: net$TFM"

mkdir -p "$OUT"

# ---------------------------------------------------------------------------
# 1. The Linux SDK tarball
# ---------------------------------------------------------------------------
curl -sSL https://dot.net/v1/dotnet-install.sh -o "$TMPPKG/dotnet-install.sh"

fetch() {  # fetch <channel> <sdk|runtime> <outfile> <label> <arch>
  local channel="$1" kind="$2" out="$3" label="$4" arch="$5" url
  local args=(--channel "$channel" --os linux --arch "$arch" --dry-run)
  [ "$kind" = "runtime" ] && args+=(--runtime dotnet)
  url="$(bash "$TMPPKG/dotnet-install.sh" "${args[@]}" 2>/dev/null \
        | grep -oE "https://[^ ]*dotnet-${kind}-[^ ]*linux-${arch}\.tar\.gz" | head -1)"
  [ -n "$url" ] || fail "Could not resolve the $label URL. Grab 'Linux x64 Binaries' from https://dotnet.microsoft.com/download/dotnet/$channel"
  if [ -f "$out" ]; then
    echo "$label already downloaded — skipping."
  else
    step "Downloading $label"
    echo "$url"
    curl -L --progress-bar -o "$out" "$url"
  fi
  shasum -a 512 "$out" | awk '{print $1}' > "$out.sha512"
}

# The SDK: match this machine's major version so restore behaviour is identical
# on both sides — no version skew between what downloaded the packages and what
# consumes them.
for ARCH in x64 arm64; do
  step "Resolving the .NET $SDK_MAJOR.0 SDK for linux-$ARCH"
  fetch "$SDK_MAJOR.0" sdk "$OUT/dotnet-sdk-linux-$ARCH.tar.gz" ".NET $SDK_MAJOR.0 SDK linux-$ARCH (~210 MB)" "$ARCH"
done

# The runtime: SDK 10 can *build* net9.0, but running a net9.0 test assembly
# needs the 9.0 runtime present. Skip when the SDK major already matches the TFM.
RT_MAJOR="${TFM%%.*}"
if [ "$SDK_MAJOR" != "$RT_MAJOR" ]; then
  for ARCH in x64 arm64; do
    step "SDK is $SDK_MAJOR.x but the project targets net$TFM — also fetching the $TFM runtime for linux-$ARCH"
    fetch "$TFM" runtime "$OUT/dotnet-runtime-linux-$ARCH.tar.gz" ".NET $TFM runtime linux-$ARCH (~32 MB)" "$ARCH"
  done
else
  echo "SDK major matches the target framework — no separate runtime needed."
fi
echo "SHA512s recorded (verify against the checksums on Microsoft's download page)."

# ---------------------------------------------------------------------------
# 2. The NuGet package graph, restored for linux-x64
# ---------------------------------------------------------------------------
step "Restoring NuGet packages for ${RIDS[*]} (this pulls Linux-specific runtime packs)"
# There is a solution file — restore it once rather than walking projects.
TARGET="$(find "$REPO/backend" -maxdepth 1 -name '*.sln' | head -1)"
[ -n "$TARGET" ] || TARGET="$(find "$REPO/backend/src" -name '*.csproj' | head -1)"
[ -n "$TARGET" ] || fail "No .sln or .csproj found under $REPO/backend"
echo "  target: $(basename "$TARGET")"

for RID in "${RIDS[@]}"; do
  echo "  restoring for $RID"
  dotnet restore "$TARGET" --runtime "$RID" --packages "$TMPPKG/pkgs" \
    || fail "restore failed for $RID — fix this locally first; it is assumption A0 in the plan"
done
# Again RID-agnostic, so both asset sets land in the feed.
dotnet restore "$TARGET" --packages "$TMPPKG/pkgs" >/dev/null || true

# The net9.0 reference pack, which SDK 10 needs in order to build net$TFM.
step "Ensuring the net$TFM targeting pack is in the feed"
grep -rl "Microsoft.NETCore.App.Ref" "$TMPPKG/pkgs" >/dev/null 2>&1 \
  && echo "  present" \
  || echo "  WARNING: no Microsoft.NETCore.App.Ref found — the sandbox build may need it"

step "Collecting .nupkg files into a flat local feed"
# A folder of .nupkg files is a valid NuGet source and is far smaller than the
# extracted package cache.
mkdir -p "$TMPPKG/feed"
find "$TMPPKG/pkgs" -name '*.nupkg' -exec cp -n {} "$TMPPKG/feed/" \;
COUNT=$(find "$TMPPKG/feed" -name '*.nupkg' | wc -l | tr -d ' ')
[ "$COUNT" -gt 0 ] || fail "No .nupkg files collected — restore produced nothing"
echo "$COUNT packages"

tar -czf "$OUT/nuget-feed.tgz" -C "$TMPPKG" feed

# ---------------------------------------------------------------------------
# 3. Size check — the transfer caps are 400 MB/file, 500 MB/call
# ---------------------------------------------------------------------------
step "Sizes"
ls -lh "$OUT" | tail -n +2

split_if_big() {
  local f="$1" bytes
  bytes=$(stat -f%z "$f" 2>/dev/null || stat -c%s "$f")
  if [ "$bytes" -gt 390000000 ]; then
    echo "Splitting $(basename "$f") ($((bytes/1000000)) MB) into 350 MB parts…"
    split -b 350m "$f" "$f.part-"
    rm "$f"
    ls -lh "$f".part-*
  fi
}
split_if_big "$OUT/nuget-feed.tgz"
for ARCH in x64 arm64; do
  split_if_big "$OUT/dotnet-sdk-linux-$ARCH.tar.gz"
  [ -f "$OUT/dotnet-runtime-linux-$ARCH.tar.gz" ] && split_if_big "$OUT/dotnet-runtime-linux-$ARCH.tar.gz"
done

TOTAL=$(du -sh "$OUT" | awk '{print $1}')
rm -rf "$TMPPKG"

cat <<EOF

──────────────────────────────────────────────────────────────
Done. $TOTAL in $OUT

Tell Claude: "the toolchain is in toolchain/" and it will stage
them, extract the SDK, restore offline against the local feed, and
run dotnet build / dotnet test in the sandbox against the native
PostgreSQL 16 + pgvector already running there on port 5438.

Note: toolchain/ is gitignored already.
──────────────────────────────────────────────────────────────
EOF
