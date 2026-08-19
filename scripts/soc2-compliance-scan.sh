#!/usr/bin/env bash
# =============================================================================
# SOC 2 Type II Compliance Scanner
#
# Crawls the DotNetCloud repository and reports findings mapped to the SOC 2
# Trust Services Criteria. Every check is tagged with a criterion ID (CC*, A*,
# PI*, C*, P*). The scanner also produces a module-coverage report that asserts
# every required module and every client/UI/CLI project is present and scanned.
#
# Requirements: ripgrep (rg)
#
# Usage:
#   soc2-compliance-scan.sh [--markdown|--txt|--ci] [target-directory]
#
# Output modes:
#   --markdown  (default) writes soc2-compliance-report-<timestamp>.md
#   --txt       writes soc2-compliance-report-<timestamp>.txt (legacy format)
#   --ci        writes soc2-compliance-report.json and exits 1 if any
#               untriaged finding exists, 0 otherwise
#
# The module-coverage section exits non-zero if any of the 15 modules or any
# client/UI/CLI project is missing or has zero scanned files.
# =============================================================================
set -uo pipefail

MODE="markdown"
TARGET=""
JSON_OUT="soc2-compliance-report.json"

usage() {
  printf '%s\n' \
    'Usage: soc2-compliance-scan.sh [--markdown|--txt|--ci] [target-directory]' \
    '' \
    'Scans a code repository for SOC 2 relevant findings mapped to TSC criteria.' \
    '' \
    'Options:' \
    '  --markdown   (default) write soc2-compliance-report-<timestamp>.md' \
    '  --txt        write soc2-compliance-report-<timestamp>.txt (legacy)' \
    '  --ci         write soc2-compliance-report.json; exit 1 on untriaged findings' \
    '  -h, --help   show this help' \
    '' \
    'Requires: ripgrep (rg)'
}

# --- Argument parsing -------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --markdown) MODE="markdown"; shift ;;
    --txt)      MODE="txt";      shift ;;
    --ci)       MODE="ci";       shift ;;
    -h|--help)  usage; exit 0 ;;
    -*) echo "ERROR: unknown option '$1'" >&2; usage >&2; exit 2 ;;
    *)  TARGET="$1"; shift ;;
  esac
done

if ! command -v rg >/dev/null 2>&1; then
  echo "ERROR: ripgrep (rg) is required but was not found on PATH." >&2
  echo "       This scanner (soc2-compliance-scan.sh) runs on Linux/macOS and needs ripgrep." >&2
  echo "" >&2
  echo "       Install ripgrep:" >&2
  echo "         Debian/Ubuntu: sudo apt-get install ripgrep" >&2
  echo "         macOS:         brew install ripgrep" >&2
  echo "         Windows:       winget install BurntSushi.ripgrep.MSVC" >&2
  echo "" >&2
  echo "       On Windows hosts (including the server via tools/install-windows.ps1), use the" >&2
  echo "       native PowerShell scanner instead — it needs NO ripgrep:" >&2
  echo "         pwsh scripts/soc2-compliance-scan.ps1 --markdown" >&2
  exit 1
fi

TARGET="${TARGET:-.}"
if ! TARGET="$(cd "$TARGET" 2>/dev/null && pwd)"; then
  echo "ERROR: cannot access target directory '${TARGET:-.}'" >&2
  exit 1
fi

STAMP="$(date +%Y%m%d-%H%M%S)"

# Report output directory. SOC2_REPORT_DIR (set by the server) overrides the scan
# target, which is read-only for the service account when auditing a repo it does
# not own. The scan itself always reads from $TARGET.
OUT_DIR="${SOC2_REPORT_DIR:-$TARGET}"
mkdir -p "$OUT_DIR"
trap 'rm -f "$OUT_DIR/.soc2-counts.tmp" 2>/dev/null' EXIT

case "$MODE" in
  markdown) REPORT="$OUT_DIR/soc2-compliance-report-$STAMP.md" ;;
  txt)      REPORT="$OUT_DIR/soc2-compliance-report-$STAMP.txt" ;;
  ci)       REPORT="$OUT_DIR/$JSON_OUT" ;;
esac

# ----------------------------------------------------------------------------
# Exclusions (see docs/SOC2_TYPE_II_COMPLIANCE_PLAN.md §3.3). Explicit -g globs
# still apply even with --no-ignore. The root deploy-artifact directory is
# excluded with '!modules/**' — this does NOT affect 'src/Modules/**'.
# ----------------------------------------------------------------------------
EXCLUDE=(
  -g '!tests/**'              -g '!**/tests/**'
  -g '!bin/**'                -g '!**/bin/**'
  -g '!obj/**'                -g '!**/obj/**'
  -g '!node_modules/**'       -g '!**/node_modules/**'
  -g '!.git/**'               -g '!**/.git/**'
  -g '!modules/**'            # root deploy artifacts (NOT src/Modules)
  -g '!wwwroot/**'            -g '!**/wwwroot/**'     # compiled/static publish output
  -g '!packages/**'           -g '!**/packages/**'
  -g '!.vs/**'                -g '!**/.vs/**'
  -g '!dist/**'               -g '!**/dist/**'
  -g '!build/**'              -g '!**/build/**'
  -g '!artifacts/**'          -g '!**/artifacts/**'
  -g '!.terraform/**'         -g '!**/.terraform/**'
  -g '!vendor/**'             -g '!**/vendor/**'
  -g '!.idea/**'              -g '!**/.idea/**'
  -g '!*.deps.json'           -g '!**/*.deps.json'
  -g '!*.min.js'              -g '!**/*.min.js'
  -g '!*.min.css'             -g '!**/*.min.css'
  -g '!*.map'                 -g '!**/*.map'
  -g '!*Designer.cs'          -g '!**/*Designer.cs'
  -g '!soc2-compliance-report-*' -g '!**/soc2-compliance-report-*'
  -g '!soc2-compliance-report.json'
  -g '!sixlabors.lic'         -g '!**/sixlabors.lic'
  -g '!soc2-compliance-scan.*' -g '!**/soc2-compliance-scan.*'
)

# Base rg options: include hidden + gitignored files, output "path:line:content".
RG=(rg --no-ignore --hidden --no-heading --line-number --color never)

# File extensions treated as "code" for TODO/FIXME + PII scans.
CODE_GLOBS=(
  -g '*.cs' -g '*.ts' -g '*.tsx' -g '*.js' -g '*.jsx' -g '*.mjs' -g '*.cjs'
  -g '*.razor' -g '*.cshtml' -g '*.html'
  -g '*.ps1' -g '*.psm1' -g '*.sh' -g '*.bash'
  -g '*.py' -g '*.go' -g '*.java' -g '*.kt' -g '*.kts' -g '*.swift' -g '*.dart'
  -g '*.cpp' -g '*.c' -g '*.h' -g '*.hpp' -g '*.cc'
  -g '*.rs' -g '*.rb' -g '*.php' -g '*.vue' -g '*.scss' -g '*.css'
)

# ----------------------------------------------------------------------------
# Result accumulator. Each finding row is "TAG|LABEL|path:line:content".
# ----------------------------------------------------------------------------
declare -a CHECK_NAMES=()
declare -a CHECK_TAGS=()
declare -a CHECK_COUNTS=()
declare -a FINDING_ROWS=()

add_check() {
  local tag="$1" label="$2" count="$3"
  CHECK_NAMES+=("$label")
  CHECK_TAGS+=("$tag")
  CHECK_COUNTS+=("$count")
}

# Run an rg-based check and record its findings. Usage: run_check <tag> <label> <rg args...>
run_check() {
  local tag="$1" label="$2"; shift 2
  local -a results
  # Exclusions go AFTER the per-check include globs so they take precedence
  # (ripgrep: the last matching glob wins). Otherwise e.g. '*.js' re-includes
  # '*.min.js', and the scanner's own regex literals self-match.
  mapfile -t results < <("${RG[@]}" "$@" "${EXCLUDE[@]}" "$TARGET" 2>/dev/null)
  local count="${#results[@]}"
  add_check "$tag" "$label" "$count"
  for line in "${results[@]}"; do
    FINDING_ROWS+=("$tag|$label|$line")
  done
  printf '%s\t%d\n' "$tag" "$count"
}

section_md() { printf '\n## %s\n' "$1"; }
section_txt() {
  printf '\n================================================================\n'
  printf '%s\n' "$1"
  printf '================================================================\n'
}

# ----------------------------------------------------------------------------
# Patterns (each check tagged with a criterion)
# ----------------------------------------------------------------------------
SECRET_PATTERN='Password|ApiKey|ApiSecret|ClientSecret|ClientId|ConnectionString|Bearer [A-Za-z0-9]'

IPV4_PATTERN='(?<![0-9.])(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}(?![0-9.])'

SECURITY_WORDS='\b(security|auth[a-z]*|encrypt[a-z]*)\b'
TODO_PATTERN="(TODO|FIXME)[^\r\n]*${SECURITY_WORDS}|${SECURITY_WORDS}[^\r\n]*(TODO|FIXME)"

WEAK_CRYPTO_PATTERN='\b(MD5|SHA1|DES|RC4|AesManaged|RijndaelManaged|TripleDES)\b'
TLS_BYPASS_PATTERN='DangerousAcceptAnyServerCertificateValidator|ServerCertificateCustomValidationCallback|AllowInsecureTls'
INSECURE_GRPC_PATTERN='ChannelCredentials\.Insecure'
RAW_SQL_PATTERN='FromSqlRaw|ExecuteSqlRaw|SqlQueryRaw'
OPEN_REDIRECT_PATTERN='Redirect\(|RedirectToAction|RedirectToPage|Url\.IsLocalUrl'
PII_PATTERN='\b(Email|Phone|Address|BirthDate|Ssn|SocialSecurity|Passport|Latitude|Longitude|Gps|Geolocation)\b'
UPLOAD_PATTERN='IFormFile|FromForm|RequestSizeLimit'
RETENTION_PATTERN='RetentionDays|Purge|RetainedFileCountLimit|AuditFilePath'

# --- Run all checks (counts also written to a temp file for later assembly) --
{
  # 1. Secrets in config-like files (CC6/CC7)
  run_check 'CC6/CC7' \
    '1. Secrets in config files (appsettings*, *.env*, Dockerfile, docker-compose, workflows)' \
    -i \
    -g 'appsettings*.json' -g '*.env*' -g 'Dockerfile*' -g 'docker-compose*.yml' -g 'docker-compose*.yaml' -g '.github/workflows/**' \
    -e "$SECRET_PATTERN"

  # 1b. .env files present (CC6/CC7)
  env_count=0
  while IFS= read -r f; do
    FINDING_ROWS+=("CC6/CC7|.env file present|$f")
    env_count=$((env_count + 1))
  done < <(rg --files --hidden --no-ignore -g '*.env*' "${EXCLUDE[@]}" "$TARGET" 2>/dev/null)
  add_check 'CC6/CC7' '1b. .env files found' "$env_count"
  printf 'CC6/CC7\t%d\n' "$env_count"

  # 2. Private keys (C1)
  run_check 'C1' \
    '2. Private key material (-----BEGIN ... PRIVATE KEY-----)' \
    -i \
    -e '-----BEGIN (RSA|EC|OPENSSH )?PRIVATE KEY-----'

  # 3. Weak crypto (C1) — non-test code
  run_check 'C1' \
    '3. Weak/hostile crypto algorithms (MD5, SHA1, DES, RC4, AesManaged, RijndaelManaged, TripleDES)' \
    -i \
    "${CODE_GLOBS[@]}" \
    -e "$WEAK_CRYPTO_PATTERN"

  # 4. TLS bypass (C1)
  run_check 'C1' \
    '4. TLS validation bypass (DangerousAcceptAnyServerCertificateValidator / AllowInsecureTls / callback)' \
    -i \
    "${CODE_GLOBS[@]}" \
    -e "$TLS_BYPASS_PATTERN"

  # 5. Insecure gRPC channel (C1)
  run_check 'C1' \
    '5. Insecure gRPC channel (ChannelCredentials.Insecure)' \
    "${CODE_GLOBS[@]}" \
    -e "$INSECURE_GRPC_PATTERN"

  # 6. Raw SQL (PI1)
  run_check 'PI1' \
    '6. Raw SQL with potential user input (FromSqlRaw / ExecuteSqlRaw / SqlQueryRaw)' \
    "${CODE_GLOBS[@]}" \
    -e "$RAW_SQL_PATTERN"

  # 7. AllowedHosts present in appsettings*.json (CC6) — reports files MISSING it
  missing_hosts=0
  while IFS= read -r f; do
    if ! rg -q 'AllowedHosts' "$f" 2>/dev/null; then
      FINDING_ROWS+=("CC6|appsettings missing AllowedHosts|$f:1:(file lacks AllowedHosts)")
      missing_hosts=$((missing_hosts + 1))
    fi
  done < <(rg --files --hidden --no-ignore -g 'appsettings*.json' "${EXCLUDE[@]}" "$TARGET" 2>/dev/null)
  add_check 'CC6' '7. appsettings*.json missing AllowedHosts' "$missing_hosts"
  printf 'CC6\t%d\n' "$missing_hosts"

  # 8. Open redirect (CC6) — manual review list
  run_check 'CC6' \
    '8. Open-redirect patterns (Redirect( / RedirectToAction / RedirectToPage / Url.IsLocalUrl) — REVIEW' \
    "${CODE_GLOBS[@]}" \
    -e "$OPEN_REDIRECT_PATTERN"

  # 9. TODO/FIXME mentioning security/auth/encryption (CC7) — minified JS excluded
  run_check 'CC7' \
    '9. TODO/FIXME comments mentioning security/auth/encryption' \
    "${CODE_GLOBS[@]}" \
    -i \
    -e "$TODO_PATTERN"

  # 10. PII field names in entity/DTO/model files (P6)
  run_check 'P6' \
    '10. PII field names in Entities/DTOs/Models' \
    -i \
    -g '**/Entities/**' -g '**/DTOs/**' -g '**/Models/**' \
    -g '*.cs' -g '*.razor' -g '*.ts' \
    -e "$PII_PATTERN"

  # 11. Upload entry points (PI1)
  run_check 'PI1' \
    '11. Upload entry points (IFormFile / FromForm / RequestSizeLimit) — verify validation wired' \
    "${CODE_GLOBS[@]}" \
    -e "$UPLOAD_PATTERN"

  # 12. Retention / disposal markers (C2/P6)
  run_check 'C2/P6' \
    '12. Retention/disposal markers (RetentionDays / Purge / RetainedFileCountLimit / AuditFilePath)' \
    -i \
    "${CODE_GLOBS[@]}" \
    -e "$RETENTION_PATTERN"

  # 13. Hardcoded IPv4 (CC7) — version/assembly declarations filtered; *.deps.json excluded up front
  ip_count=0
  while IFS= read -r line; do
    FINDING_ROWS+=("CC7|Hardcoded IPv4|$line")
    ip_count=$((ip_count + 1))
  done < <("${RG[@]}" -P -e "$IPV4_PATTERN" "${EXCLUDE[@]}" "$TARGET" 2>/dev/null \
    | rg --color never -v 'Version=|AssemblyVersion|FileVersion|InformationalVersion|PackageVersion|PublicKeyToken|Culture=')
  add_check 'CC7' '13. Hardcoded IPv4 addresses' "$ip_count"
  printf 'CC7\t%d\n' "$ip_count"
} > "$OUT_DIR/.soc2-counts.tmp"

# ----------------------------------------------------------------------------
# Module & project coverage
# ----------------------------------------------------------------------------
MODULE_IDS=(
  files chat search contacts calendar notes about
  ai bookmarks email example music photos tracks video
)

declare -a MODULE_COVERAGE=()
declare -a CLIENT_COVERAGE=()
total_scanned=0
missing_modules=0

for id in "${MODULE_IDS[@]}"; do
  # Module directories are capitalized (Files, Chat, ...) while module IDs are
  # lowercase (dotnetcloud.files). Resolve the directory case-insensitively.
  dir=""
  while IFS= read -r d; do
    dir="$d"
    break
  done < <(find "$TARGET/src/Modules" -maxdepth 1 -type d -iname "$id" 2>/dev/null)
  if [[ -z "$dir" ]]; then
    MODULE_COVERAGE+=("$id|0|MISSING")
    missing_modules=$((missing_modules + 1))
    continue
  fi
  count=$(rg --files --hidden --no-ignore -g '*.cs' -g '*.csproj' "$dir" 2>/dev/null | wc -l)
  count=${count//[[:space:]]/}
  MODULE_COVERAGE+=("$id|$count|ok")
  total_scanned=$((total_scanned + count))
  if [[ "$count" -eq 0 ]]; then
    missing_modules=$((missing_modules + 1))
  fi
done

CLIENT_DIRS=(
  "$TARGET/src/Clients/DotNetCloud.Client.Core"
  "$TARGET/src/Clients/DotNetCloud.Client.SyncTray"
  "$TARGET/src/Clients/DotNetCloud.Client.Android"
  "$TARGET/src/Clients/DotNetCloud.Client.Updater"
  "$TARGET/src/Clients/DotNetCloud.Client.BrowserExtension"
  "$TARGET/src/UI/DotNetCloud.UI.Web"
  "$TARGET/src/UI/DotNetCloud.UI.Web.Client"
  "$TARGET/src/UI/DotNetCloud.UI.Shared"
  "$TARGET/src/UI/DotNetCloud.UI.Android"
  "$TARGET/src/CLI/DotNetCloud.CLI"
  "$TARGET/src/Core/DotNetCloud.Core"
  "$TARGET/src/Core/DotNetCloud.Core.Data"
  "$TARGET/src/Core/DotNetCloud.Core.ServiceDefaults"
  "$TARGET/src/Core/DotNetCloud.Core.Grpc"
  "$TARGET/src/Core/DotNetCloud.Core.Auth"
  "$TARGET/src/Core/DotNetCloud.Core.Server"
)

for dir in "${CLIENT_DIRS[@]}"; do
  name=$(basename "$dir")
  if [[ ! -d "$dir" ]]; then
    CLIENT_COVERAGE+=("$name|0|MISSING")
    missing_modules=$((missing_modules + 1))
    continue
  fi
  count=$(rg --files --hidden --no-ignore -g '*.cs' -g '*.csproj' -g '*.ts' -g '*.js' "$dir" 2>/dev/null | wc -l)
  count=${count//[[:space:]]/}
  CLIENT_COVERAGE+=("$name|$count|ok")
  total_scanned=$((total_scanned + count))
  if [[ "$count" -eq 0 ]]; then
    missing_modules=$((missing_modules + 1))
  fi
done

# ----------------------------------------------------------------------------
# Report assembly
# ----------------------------------------------------------------------------
total_findings="${#FINDING_ROWS[@]}"

if [[ "$MODE" == "ci" ]]; then
  {
    printf '{\n'
    printf '  "generated": "%s",\n' "$(date -Is)"
    printf '  "target": "%s",\n' "$TARGET"
    printf '  "total_findings": %d,\n' "$total_findings"
    printf '  "missing_coverage": %d,\n' "$missing_modules"
    printf '  "checks": [\n'
    i=0
    for tag in "${CHECK_TAGS[@]}"; do
      sep=","
      [[ $i -eq $((${#CHECK_TAGS[@]} - 1)) ]] && sep=""
      printf '    { "tag": "%s", "label": "%s", "count": %d }%s\n' \
        "$tag" "${CHECK_NAMES[$i]}" "${CHECK_COUNTS[$i]}" "$sep"
      i=$((i + 1))
    done
    printf '  ],\n'
    printf '  "coverage": [\n'
    j=0
    ALL_COV=("${MODULE_COVERAGE[@]}" "${CLIENT_COVERAGE[@]}")
    for row in "${ALL_COV[@]}"; do
      sep=","
      [[ $j -eq $((${#ALL_COV[@]} - 1)) ]] && sep=""
      name="${row%%|*}"
      rest="${row#*|}"
      count="${rest%%|*}"
      status="${rest##*|}"
      printf '    { "name": "%s", "files": %d, "status": "%s" }%s\n' \
        "$name" "$count" "$status" "$sep"
      j=$((j + 1))
    done
    printf '  ],\n'
    printf '  "findings": [\n'
    k=0
    for row in "${FINDING_ROWS[@]}"; do
      sep=","
      [[ $k -eq $((${#FINDING_ROWS[@]} - 1)) ]] && sep=""
      tag="${row%%|*}"
      rest="${row#*|}"
      label="${rest%%|*}"
      detail="${rest#*|}"
      printf '    { "tag": "%s", "label": "%s", "detail": "%s" }%s\n' \
        "$tag" "$label" "$detail" "$sep"
      k=$((k + 1))
    done
    printf '  ]\n'
    printf '}\n'
  } > "$REPORT"

  if [[ "$total_findings" -gt 0 || "$missing_modules" -gt 0 ]]; then
    echo "SOC2 CI: $total_findings finding(s), $missing_modules missing coverage item(s) — FAIL" >&2
    exit 1
  fi
  echo "SOC2 CI: clean ($total_findings findings, $missing_modules missing) — PASS"
  exit 0
fi

# --- Markdown / txt report body ---------------------------------------------
{
  if [[ "$MODE" == "markdown" ]]; then
    printf '# SOC 2 Type II Compliance Scan Report\n\n'
    printf '**Generated:** %s  \n' "$(date)"
    printf '**Target:** %s  \n' "$TARGET"
    printf '**Tool:** %s  \n' "$(rg --version | head -n1)"
    printf '**Total findings:** %d  \n' "$total_findings"
    printf '**Missing coverage:** %d\n' "$missing_modules"

    printf '\n## Summary by criterion\n\n'
    printf '| Criterion | Check | Count |\n'
    printf '| --------- | ----- | ----- |\n'
    i=0
    for tag in "${CHECK_TAGS[@]}"; do
      printf '| %s | %s | %d |\n' "$tag" "${CHECK_NAMES[$i]}" "${CHECK_COUNTS[$i]}"
      i=$((i + 1))
    done
  else
    printf 'SOC 2 Compliance Scan Report\n'
    printf 'Generated: %s\n' "$(date)"
    printf 'Target:    %s\n' "$TARGET"
    printf 'Tool:      %s\n' "$(rg --version | head -n1)"
    printf 'Total findings: %d\n' "$total_findings"
    printf 'Missing coverage: %d\n' "$missing_modules"
  fi

  # Per-check detail sections
  i=0
  for tag in "${CHECK_TAGS[@]}"; do
    label="${CHECK_NAMES[$i]}"
    count="${CHECK_COUNTS[$i]}"
    if [[ "$MODE" == "markdown" ]]; then
      section_md "${label}  [${tag}] — ${count} match(es)"
    else
      section_txt "${label}  [${tag}] — ${count} match(es)"
    fi
    printed=0
    for row in "${FINDING_ROWS[@]}"; do
      if [[ "$row" == "$tag|$label|"* ]]; then
        rest="${row#*|}"
        rest="${rest#*|}"
        rest="${rest#*|}"
        printf '%s\n' "$rest"
        printed=$((printed + 1))
      fi
    done
    if [[ "$printed" -eq 0 ]]; then
      printf '_No findings._\n'
    fi
    i=$((i + 1))
  done

  # Module coverage section
  if [[ "$MODE" == "markdown" ]]; then
    printf '\n## Module & project coverage\n\n'
    printf '**Total scanned files:** %d  \n' "$total_scanned"
    printf '**Missing/empty projects:** %d\n\n' "$missing_modules"
    printf '| Project | Scanned files | Status |\n'
    printf '| ------- | ------------- | ------ |\n'
  else
    section_txt 'Module & project coverage'
    printf 'Total scanned files: %d\n' "$total_scanned"
    printf 'Missing/empty projects: %d\n' "$missing_modules"
  fi
  for row in "${MODULE_COVERAGE[@]}" "${CLIENT_COVERAGE[@]}"; do
    name="${row%%|*}"; rest="${row#*|}"
    count="${rest%%|*}"; status="${rest##*|}"
    printf '| %s | %s | %s |\n' "$name" "$count" "$status"
  done

  printf '\n'
  if [[ "$MODE" == "markdown" ]]; then
    printf '## Notes\n\n'
    printf -- '- Every hit is `path:line:content`; a match is not automatically a violation. Triage each one.\n'
    printf -- '- Secrets, weak crypto, and TLS findings must be verified as env-gated or dev-only before the audit.\n'
    printf -- '- Raw SQL and open-redirect findings are manual-review lists.\n'
    printf -- '- PII findings feed `docs/security/PII_INVENTORY.md`.\n'
    printf -- '- Module coverage asserts all 15 modules + clients/UI/CLI are scanned.\n'
  else
    printf 'Notes:\n'
    printf '  - Every hit is path:line:content; a match is not automatically a violation.\n'
    printf '  - Secrets, weak crypto, and TLS findings must be verified as env-gated or dev-only.\n'
    printf '  - Raw SQL and open-redirect findings are manual-review lists.\n'
    printf '  - Module coverage asserts all 15 modules + clients/UI/CLI are scanned.\n'
  fi
  printf '\nReport saved to: %s\n' "$REPORT"
} > "$REPORT"

echo "SOC2 scan complete: $REPORT"
exit 0
