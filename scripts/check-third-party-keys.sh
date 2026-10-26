#!/usr/bin/env bash
# Fails when a third-party API key literal is committed to the repository.
#
# Why: a provider key committed to source is written into every generated systemd unit and
# shipped to every operator, so exposure is not limited to this repository. Keys are rotated at
# the provider; this check stops a replacement from being committed again.
#
# Usage: scripts/check-third-party-keys.sh [path-to-scan]
# Exit codes: 0 = clean, 1 = a key literal was found.
set -euo pipefail

root="${1:-.}"

# Literal shapes only — a generic high-entropy scan produces too much noise on a codebase with
# this many hashes and GUIDs.
patterns=(
  # TMDB v3 API keys are 32 lowercase hex characters assigned to a TMDB-named setting.
  'TmdbApiKey[^A-Za-z0-9]{1,4}[0-9a-f]{32}'
  # The provider host with a key in the query string.
  'api\.themoviedb\.org[^[:space:]]*api_key=[0-9a-f]{32}'
  # Same key shape pushed through a systemd/Environments assignment.
  'Video__Enrichment__TmdbApiKey=[0-9a-f]{32}'
)

found=0

for pattern in "${patterns[@]}"; do
  # Exclude build output, docs (which describe the policy) and this script itself.
  while IFS= read -r match; do
    [ -z "$match" ] && continue
    echo "::error::Third-party API key literal found: $match"
    found=1
  done < <(grep -rInE --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git \
             --exclude-dir=node_modules --exclude-dir=docs \
             --exclude='check-third-party-keys.sh' \
             "$pattern" "$root" || true)
done

if [ "$found" -ne 0 ]; then
  echo "Third-party key literals must not be committed. Rotate the key at the provider and read it from configuration instead."
  exit 1
fi

echo "No third-party API key literals found."
