#!/usr/bin/env bash
set -euo pipefail

# ── Derive version from latest git tag, fall back to Directory.Build.props ──
_get_version() {
  local repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.."; pwd)"
  local tag="$(git -C "$repo_root" describe --tags --match 'v*' --abbrev=0 2>/dev/null || true)"
  if [[ -n "$tag" ]]; then
    echo "${tag#v}"
    return
  fi
  local props_file="$repo_root/Directory.Build.props"
  local major="$(grep -oP '<MajorVersion>\K[^<]+' "$props_file")"
  local minor="$(grep -oP '<MinorVersion>\K[^<]+' "$props_file")"
  local patch="$(grep -oP '<PatchVersion>\K[^<]+' "$props_file")"
  local prerelease="$(grep -oP '<PreReleaseVersion>\K[^<]+' "$props_file")"
  echo "${major}.${minor}.${patch}-${prerelease}"
}

VERSION="${1:-$(_get_version)}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-./artifacts/installers}"
BUILD_MSIX="${4:-false}"

if ! command -v pwsh >/dev/null 2>&1; then
  echo "Error: pwsh (PowerShell 7+) is required to run this script."
  echo "Install PowerShell and re-run, or invoke the .ps1 script directly on Windows."
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pwsh -NoProfile -File "$SCRIPT_DIR/build-desktop-client-bundles.ps1" \
  -Version "$VERSION" \
  -Configuration "$CONFIGURATION" \
  -OutputDir "$OUTPUT_DIR" \
  -BuildMsix:"$BUILD_MSIX"

echo
echo "Desktop client installers generated in: $OUTPUT_DIR"
