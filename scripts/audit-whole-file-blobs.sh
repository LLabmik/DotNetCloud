#!/usr/bin/env bash
#
# Audit whole-file media blobs.
#
# A "whole-file" FileVersion (IsChunked = 0) keeps its content in a single blob under
# <storage-root>/files/xx/yy/<hash>. The blob is then the ONLY copy of that content, so if it goes
# missing the file still lists normally and only fails when it is read — which surfaces to the user as
# a 404 at playback time. This script finds those versions before a user hits them.
#
# For each live whole-file version it checks:
#   * the blob exists on disk
#   * its on-disk size matches the version's recorded size
#   * whether chunk mappings survive (content rebuildable) or not (unrecoverable)
#
# Usage:
#   scripts/audit-whole-file-blobs.sh [storage-root] [--all]
#
#   storage-root  Override the storage root (default: /var/lib/dotnetcloud/storage).
#   --all         Include files whose blob is present but has the wrong size (noisier).
#
# Exit codes:
#   0  every whole-file version has its content
#   1  some content is missing but still rebuildable from chunks
#   2  some content is missing with no way to rebuild it (needs re-upload / restore)
#   3  the audit could not run
#
set -uo pipefail

CONFIG_FILE="/etc/dotnetcloud/config.json"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
DEFAULT_STORAGE_ROOT="/var/lib/dotnetcloud/storage"

STORAGE_ROOT="$DEFAULT_STORAGE_ROOT"
INCLUDE_SIZE_MISMATCH=0

for arg in "$@"; do
  case "$arg" in
    --all) INCLUDE_SIZE_MISMATCH=1 ;;
    --help|-h) sed -n '2,26p' "$0"; exit 0 ;;
    *) STORAGE_ROOT="$arg" ;;
  esac
done

if [[ ! -r "$CONFIG_FILE" ]]; then
  echo "ERROR: cannot read $CONFIG_FILE" >&2
  exit 3
fi

if [[ ! -x "$SQLCMD" ]]; then
  echo "ERROR: $SQLCMD not found (this audit targets the SQL Server deployment)" >&2
  exit 3
fi

PROVIDER="$(python3 -c "import json;d=json.load(open('$CONFIG_FILE'));print(d.get('database',{}).get('provider',''))" 2>/dev/null)"
CONNECTION_STRING="$(python3 -c "import json;d=json.load(open('$CONFIG_FILE'));print(d.get('connectionString',''))" 2>/dev/null)"

if [[ -z "$CONNECTION_STRING" ]]; then
  echo "ERROR: no connectionString in $CONFIG_FILE" >&2
  exit 3
fi

case "$(echo "$PROVIDER" | tr '[:upper:]' '[:lower:]')" in
  sqlserver|mssql) ;;
  "") echo "WARNING: database.provider is not set in $CONFIG_FILE; assuming SqlServer." >&2 ;;
  *) echo "ERROR: this audit only supports SQL Server (found provider '$PROVIDER')." >&2; exit 3 ;;
esac

if [[ ! -d "$STORAGE_ROOT" ]]; then
  echo "ERROR: storage root '$STORAGE_ROOT' does not exist" >&2
  exit 3
fi

SERVER="$(echo "$CONNECTION_STRING" | sed -n 's/.*[Ss]erver=\([^;]*\).*/\1/p')"
DATABASE="$(echo "$CONNECTION_STRING" | sed -n 's/.*[Dd]atabase=\([^;]*\).*/\1/p')"
DB_USER="$(echo "$CONNECTION_STRING" | sed -n 's/.*[Uu]ser [Ii][Dd]=\([^;]*\).*/\1/p')"
DB_PASS="$(echo "$CONNECTION_STRING" | sed -n 's/.*[Pp]assword=\([^;]*\).*/\1/p')"

if [[ -z "$SERVER" || -z "$DATABASE" ]]; then
  echo "ERROR: could not parse Server/Database from the connection string" >&2
  exit 3
fi

echo "Whole-file blob audit"
echo "  storage root : $STORAGE_ROOT"
echo "  database     : $DATABASE @ $SERVER"
echo

QUERY="SET NOCOUNT ON;
SELECT CONVERT(nvarchar(40), v.Id) + '|' + CONVERT(nvarchar(40), v.FileNodeId) + '|' + ISNULL(n.Name, '?') + '|' + CONVERT(nvarchar(20), v.Size) + '|' + ISNULL(v.StoragePath, '') + '|' + CONVERT(nvarchar(10), (SELECT COUNT(*) FROM core.FileVersionChunks vc WHERE vc.FileVersionId = v.Id))
FROM core.FileVersions v
JOIN core.FileNodes n ON n.Id = v.FileNodeId
WHERE v.IsChunked = 0 AND n.IsDeleted = 0
ORDER BY n.Name;"

ROWS="$("$SQLCMD" -S "$SERVER" -d "$DATABASE" -U "$DB_USER" -P "$DB_PASS" -C -h -1 -W -Q "$QUERY" 2>&1)"
SQL_EXIT=$?

if [[ $SQL_EXIT -ne 0 ]]; then
  echo "ERROR: database query failed:" >&2
  echo "$ROWS" >&2
  exit 3
fi

TOTAL=0
OK=0
MISSING_RECOVERABLE=0
MISSING_UNRECOVERABLE=0
SIZE_MISMATCH=0

while IFS='|' read -r version_id node_id name expected_size storage_path mappings; do
  [[ -z "${version_id// }" ]] && continue
  TOTAL=$((TOTAL + 1))

  # Trim the padding sqlcmd's -W can leave on numeric columns.
  expected_size="${expected_size// /}"
  mappings="${mappings// /}"

  if [[ -z "$storage_path" || ! -f "$STORAGE_ROOT/$storage_path" ]]; then
    if [[ "${mappings:-0}" -gt 0 ]]; then
      MISSING_RECOVERABLE=$((MISSING_RECOVERABLE + 1))
      printf 'MISSING (rebuildable)  %s  %s bytes  mappings=%s  version=%s  blob=%s\n' \
        "$name" "$expected_size" "$mappings" "$version_id" "${storage_path:-<null>}"
    else
      MISSING_UNRECOVERABLE=$((MISSING_UNRECOVERABLE + 1))
      printf 'MISSING (UNRECOVERABLE) %s  %s bytes  version=%s  blob=%s\n' \
        "$name" "$expected_size" "$version_id" "${storage_path:-<null>}"
      printf '                       -> re-upload or restore from a backup; there are no chunks left to rebuild from\n'
    fi
    continue
  fi

  actual_size="$(stat -c %s "$STORAGE_ROOT/$storage_path" 2>/dev/null || echo 0)"
  if [[ "$expected_size" =~ ^[0-9]+$ ]] && [[ "$expected_size" -gt 0 ]] && [[ "$actual_size" != "$expected_size" ]]; then
    if [[ $INCLUDE_SIZE_MISMATCH -eq 1 ]]; then
      SIZE_MISMATCH=$((SIZE_MISMATCH + 1))
      printf 'SIZE MISMATCH          %s  expected=%s actual=%s  version=%s\n' \
        "$name" "$expected_size" "$actual_size" "$version_id"
    else
      MISSING_UNRECOVERABLE=$((MISSING_UNRECOVERABLE + 1))
      printf 'TRUNCATED              %s  expected=%s actual=%s  version=%s\n' \
        "$name" "$expected_size" "$actual_size" "$version_id"
    fi
    continue
  fi

  OK=$((OK + 1))
done <<< "$ROWS"

echo
echo "Summary: $TOTAL whole-file version(s) — $OK healthy, $MISSING_RECOVERABLE missing-but-rebuildable, $MISSING_UNRECOVERABLE unrecoverable"
[[ $SIZE_MISMATCH -gt 0 ]] && echo "         $SIZE_MISMATCH size mismatch(es) reported (--all)"

if [[ $MISSING_UNRECOVERABLE -gt 0 ]]; then
  exit 2
elif [[ $MISSING_RECOVERABLE -gt 0 ]]; then
  exit 1
else
  exit 0
fi
