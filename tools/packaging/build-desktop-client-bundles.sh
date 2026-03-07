#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.0-alpha}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-./artifacts/installers}"

if ! command -v pwsh >/dev/null 2>&1; then
  echo "Error: pwsh (PowerShell 7+) is required to run this script."
  echo "Install PowerShell and re-run, or invoke the .ps1 script directly on Windows."
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pwsh -NoProfile -File "$SCRIPT_DIR/build-desktop-client-bundles.ps1" \
  -Version "$VERSION" \
  -Configuration "$CONFIGURATION" \
  -OutputDir "$OUTPUT_DIR"

echo
echo "Desktop client installers generated in: $OUTPUT_DIR"
