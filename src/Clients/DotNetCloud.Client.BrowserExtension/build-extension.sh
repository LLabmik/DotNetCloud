#!/usr/bin/env bash
# ─── DotNetCloud Browser Extension — Build Script (Bash/Linux) ─────────────
# Builds both Chrome and Firefox extensions and packages them as ZIP archives.
# Usage: ./build-extension.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Building DotNetCloud Browser Extension..."

# Install dependencies if needed
if [ ! -d "node_modules" ]; then
    echo "Installing dependencies..."
    npm install
fi

# Build Chrome
echo ""
echo "Building Chrome extension (MV3)..."
npm run build:chrome

# Build Firefox
echo ""
echo "Building Firefox extension (MV3)..."
npm run build:firefox

# Package
echo ""
echo "Packaging extensions..."

DIST_DIR="$SCRIPT_DIR/dist"

if [ -d "$DIST_DIR/chrome" ]; then
    cd "$DIST_DIR/chrome"
    zip -r "$DIST_DIR/dotnetcloud-bookmarks-chrome.zip" . -x ".*"
    echo "  ✓ Created: dist/dotnetcloud-bookmarks-chrome.zip"
    cd "$SCRIPT_DIR"
fi

if [ -d "$DIST_DIR/firefox" ]; then
    cd "$DIST_DIR/firefox"
    zip -r "$DIST_DIR/dotnetcloud-bookmarks-firefox.zip" . -x ".*"
    echo "  ✓ Created: dist/dotnetcloud-bookmarks-firefox.zip"
    cd "$SCRIPT_DIR"
fi

echo ""
echo "Done!"
