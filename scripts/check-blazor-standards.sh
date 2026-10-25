#!/usr/bin/env bash
# check-blazor-standards.sh — Enforce the Blazor code & style separation standard.
#
# Rule (docs/BLAZOR_CODE_STANDARDS_PLAN.md): a .razor file contains markup and
# Razor directives only.
#   * no @code / @functions blocks        -> Component.razor.cs
#   * no <style> blocks                   -> Component.razor.css
#   * no static inline style="..." with MORE THAN ONE declaration -> Component.razor.css
#     (a single declaration, e.g. style="display:none", is allowed)
#
# Files listed in scripts/blazor-standards-allowlist.txt (if it exists) are treated as
# grandfathered. The allowlist was deleted on 2026-10-24 when the burn-down finished — do not
# recreate it.
#
# Usage:
#   ./scripts/check-blazor-standards.sh           # check; exit 1 on violations
#   ./scripts/check-blazor-standards.sh --list    # print current violations
#   ./scripts/check-blazor-standards.sh --help

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ALLOWLIST="$SCRIPT_DIR/blazor-standards-allowlist.txt"

cd "$ROOT" || exit 2

case "${1:-}" in
  --help|-h)
    sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
esac

LIST_ONLY=0
[ "${1:-}" = "--list" ] && LIST_ONLY=1

# ── file discovery ──────────────────────────────────────────────────────────
if command -v rg >/dev/null 2>&1; then
  find_code()   { rg -l --glob '*.razor' '^[[:space:]]*@(code|functions)[[:space:]]*\{' || true; }
  find_style()  { rg -l --glob '*.razor' '<style[[:space:]>]' || true; }
  find_inline() { rg -n -o --glob '*.razor' 'style="[^"]*"' || true; }
elif command -v grep >/dev/null 2>&1; then
  GREP_EXCLUDES=(--exclude-dir=.git --exclude-dir=bin --exclude-dir=obj
                 --exclude-dir=node_modules --exclude-dir=artifacts)
  find_code()   { grep -rlE "${GREP_EXCLUDES[@]}" --include='*.razor' \
                    '^[[:space:]]*@(code|functions)[[:space:]]*\{' . 2>/dev/null | sed 's#^\./##' || true; }
  find_style()  { grep -rlE "${GREP_EXCLUDES[@]}" --include='*.razor' \
                    '<style[[:space:]>]' . 2>/dev/null | sed 's#^\./##' || true; }
  find_inline() { grep -rnoE "${GREP_EXCLUDES[@]}" --include='*.razor' \
                    'style="[^"]*"' . 2>/dev/null | sed 's#^\./##' || true; }
else
  echo "ERROR: neither ripgrep (rg) nor grep is available." >&2
  exit 2
fi

is_allowlisted() {
  [ -f "$ALLOWLIST" ] && grep -qxF -- "$1" "$ALLOWLIST"
}

# GitHub Actions annotation helper
annotate() {
  if [ "${GITHUB_ACTIONS:-}" = "true" ]; then printf '::error file=%s::%s\n' "$1" "$2"; fi
}
annotate_warning() {
  if [ "${GITHUB_ACTIONS:-}" = "true" ]; then printf '::warning file=%s::%s\n' "$1" "$2"; fi
}

code_files="$(find_code | sort -u)"
style_files="$(find_style | sort -u)"

# ── --list: print what the allowlist would need to contain ─────────────────
if [ "$LIST_ONLY" = "1" ]; then
  { printf '%s\n' "$code_files"; printf '%s\n' "$style_files"; } \
    | sed '/^$/d' | sort -u
  exit 0
fi

fail=0
code_new=0
style_new=0
code_grandfathered=0
style_grandfathered=0

if [ -n "$code_files" ]; then
  while IFS= read -r f; do
    [ -z "$f" ] && continue
    if is_allowlisted "$f"; then
      code_grandfathered=$((code_grandfathered + 1))
    else
      code_new=$((code_new + 1))
      fail=1
      annotate "$f" "@code/@functions block must move to ${f%.razor}.razor.cs"
      echo "✗ @code block in $f  →  move to ${f%.razor}.razor.cs"
    fi
  done <<< "$code_files"
fi

if [ -n "$style_files" ]; then
  while IFS= read -r f; do
    [ -z "$f" ] && continue
    if is_allowlisted "$f"; then
      style_grandfathered=$((style_grandfathered + 1))
    else
      style_new=$((style_new + 1))
      fail=1
      annotate "$f" "<style> block must move to ${f%.razor}.razor.css"
      echo "✗ <style> block in $f  →  move to ${f%.razor}.razor.css"
    fi
  done <<< "$style_files"
fi

# ── static inline styles: only MULTI-declaration values are violations ─────
# A single declaration (style="display:none") is explicitly allowed — no need to
# crowd .razor.css with one-liners. Two or more declarations must become a class.
inline_hits="$(find_inline)"
inline_multi="$(printf '%s\n' "$inline_hits" | awk '
  {
    if (!match($0, /style="[^"]*"/)) next
    v = substr($0, RSTART + 7, RLENGTH - 8)
    if (v ~ /[@$]/) next                     # Razor expression -> allowed
    n = split(v, a, ";"); c = 0
    for (i = 1; i <= n; i++) if (a[i] ~ /[^ \t]/) c++
    if (c > 1) print
  }')"
inline_single=0
if [ -n "$inline_hits" ]; then
  inline_total="$(printf '%s\n' "$inline_hits" | sed '/^$/d' | wc -l | tr -d ' ')"
  inline_single=$inline_total
fi
inline_multi_count=0
if [ -n "$inline_multi" ]; then
  while IFS= read -r hit; do
    [ -z "$hit" ] && continue
    inline_multi_count=$((inline_multi_count + 1))
    inline_single=$((inline_single - 1))
    fail=1
    hit_file="${hit%%:*}"
    annotate "$hit_file" "inline style has multiple declarations — move them to a class in ${hit_file%.razor}.razor.css"
    echo "✗ multi-declaration inline style: $hit"
  done <<< "$inline_multi"
fi

echo
echo "Blazor standards: ${code_new} new @code, ${style_new} new <style>, ${inline_multi_count} multi-declaration inline style(s)."
echo "Grandfathered via allowlist: ${code_grandfathered} @code file(s), ${style_grandfathered} <style> file(s)."
echo "Single-declaration / dynamic inline styles (allowed): ${inline_single}."

if [ "$fail" -ne 0 ]; then
  echo
  echo "FAIL — code and styles must not live in .razor files."
  echo "Standard: .github/copilot-instructions.md (Blazor Code & Style Separation)"
  echo "Burn-down plan: docs/BLAZOR_CODE_STANDARDS_PLAN.md"
  exit 1
fi

echo "OK — no new Blazor code/style violations."
exit 0
