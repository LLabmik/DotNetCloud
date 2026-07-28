#!/usr/bin/env bash
# deploy.sh — Deploy DotNetCloud server and module hosts to production
#
# Usage:
#   sudo ./scripts/deploy.sh                    # Incremental (changed modules only)
#   sudo ./scripts/deploy.sh --force             # Full rebuild of everything
#   sudo ./scripts/deploy.sh --config Debug      # Build configuration
#   sudo ./scripts/deploy.sh --skip-modules "About,AI"    # Skip specific modules
#   sudo ./scripts/deploy.sh --skip-stop         # Don't restart the service
#   sudo ./scripts/deploy.sh --skip-build        # Skip build step (publish existing build output)
#   sudo ./scripts/deploy.sh --dry-run           # Show what would happen
#   sudo ./scripts/deploy.sh --verify            # Hash-check assemblies after deploy
#   sudo ./scripts/deploy.sh --help              # This help
#
# Legacy: positional arg is config:
#   sudo ./scripts/deploy.sh Debug
set -euo pipefail

# ============================================================================
# Configuration
# ============================================================================
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
DEPLOY_DIR="/opt/dotnetcloud/server"
MODULES_DIR="$DEPLOY_DIR/modules"
SERVICE_USER="dotnetcloud"
STATE_FILE="$DEPLOY_DIR/.last-deploy-commit"

# Must run as root (directly or via sudo) — systemctl, chown, and writing to
# DEPLOY_DIR all require elevated privileges and prompt via polkit otherwise.
if [ "$(id -u)" -ne 0 ]; then
    echo "Error: This script must be run as root (use sudo)."
    echo "  sudo $0 $*"
    exit 1
fi

# ============================================================================
# Module registry — all known modules with publishable hosts
# ============================================================================
MODULES=(Contacts Calendar Chat Files Notes Tracks Music Photos Video Search Bookmarks Email About AI)

# Core project paths (relative to REPO_ROOT)
CORE_PROJECTS=(
    "src/Core/DotNetCloud.Core"
    "src/Core/DotNetCloud.Core.Auth"
    "src/Core/DotNetCloud.Core.Data"
    "src/Core/DotNetCloud.Core.Data.SqlServer"
    "src/Core/DotNetCloud.Core.Grpc"
    "src/Core/DotNetCloud.Core.Schema"
    "src/Core/DotNetCloud.Core.Server"
    "src/Core/DotNetCloud.Core.ServiceDefaults"
)

# UI project paths
UI_PROJECTS=(
    "src/UI/DotNetCloud.UI.Shared"
    "src/UI/DotNetCloud.UI.Web"
    "src/UI/DotNetCloud.UI.Web.Client"
)

# ============================================================================
# Default option values
# ============================================================================
CONFIG="Release"
FORCE=false
SKIP_MODULES=""
SKIP_STOP=false
SKIP_BUILD=false
DRY_RUN=false
VERIFY=false

# ============================================================================
# Parse arguments
# ============================================================================
POSITIONAL_ARGS=()
while [ $# -gt 0 ]; do
    case "$1" in
        --force|-f)    FORCE=true; shift ;;
        --config)      CONFIG="$2"; shift 2 ;;
        --config=*)    CONFIG="${1#*=}"; shift ;;
        --skip-modules) SKIP_MODULES="$2"; shift 2 ;;
        --skip-modules=*) SKIP_MODULES="${1#*=}"; shift ;;
        --skip-stop)   SKIP_STOP=true; shift ;;
        --skip-build)  SKIP_BUILD=true; shift ;;
        --dry-run)     DRY_RUN=true; shift ;;
        --verify)      VERIFY=true; shift ;;
        --help|-h)     head -30 "$0" | grep '^#' | sed 's/^# \?//'; exit 0 ;;
        --*)           echo "Error: Unknown option '$1'"; exit 1 ;;
        *)             POSITIONAL_ARGS+=("$1"); shift ;;
    esac
done

# First positional arg is config (legacy support)
if [ "${#POSITIONAL_ARGS[@]}" -gt 0 ]; then
    CONFIG="${POSITIONAL_ARGS[0]}"
fi

# Validate config
if [ "$CONFIG" != "Release" ] && [ "$CONFIG" != "Debug" ]; then
    echo "Error: Invalid configuration '$CONFIG'. Must be 'Release' or 'Debug'."
    exit 1
fi

# Rebuild BUILD_FLAGS now that CONFIG is known
BUILD_FLAGS=(-c "$CONFIG" -p:DebugType=None -p:DebugSymbols=false)

# Temp deploy solution filter (generated for full builds); cleaned up on exit.
DEPLOY_SLNF=""
trap '[ -n "${DEPLOY_SLNF:-}" ] && rm -f "$DEPLOY_SLNF"' EXIT

# Parse --skip-modules into an array for fast lookup
declare -A SKIP_MODULE_LOOKUP
if [ -n "$SKIP_MODULES" ]; then
    IFS=',' read -ra SKIP_LIST <<< "$SKIP_MODULES"
    for m in "${SKIP_LIST[@]}"; do
        # Trim whitespace
        m="$(echo "$m" | xargs)"
        SKIP_MODULE_LOOKUP["$m"]=1
    done
fi

# ============================================================================
# Helper functions
# ============================================================================

# Log with timestamp prefix
log() { echo "[$(date '+%H:%M:%S')] $*"; }

# Print a step header (auto-numbered)
STEP_NUM=0
step() {
    STEP_NUM=$((STEP_NUM + 1))
    echo ""
    log "[$STEP_NUM/$TOTAL_STEPS] $*"
}

# Get the last deployed commit hash from state file
get_last_deployed_commit() {
    if [ -f "$STATE_FILE" ]; then
        cat "$STATE_FILE"
    else
        echo ""
    fi
}

# Get list of changed files compared to last deployed commit
get_changed_files() {
    local last_deploy
    last_deploy=$(get_last_deployed_commit)

    cd "$REPO_ROOT"
    {
        if [ -n "$last_deploy" ]; then
            # Committed changes since last deploy
            git diff --name-only "$last_deploy..HEAD" 2>/dev/null || true
        fi
        # Uncommitted working-tree changes (unstaged modifications, new files)
        git diff --name-only HEAD 2>/dev/null || true
        git ls-files --others --exclude-standard 2>/dev/null || true
    } | sort -u
}

# Check whether a directory path has any changed files
dir_has_changes() {
    local dir="$1"
    echo "$CHANGED_FILES" | grep -q "^${dir}/" && return 0 || return 1
}

# Check whether a module (any of its sub-projects) has changed files
module_has_changes() {
    local module="$1"
    echo "$CHANGED_FILES" | grep -q "^src/Modules/${module}/" && return 0 || return 1
}

# Check whether a module's Data or Data.SqlServer project has changed
module_data_changed() {
    local module="$1"
    echo "$CHANGED_FILES" | grep -qE "^src/Modules/${module}/DotNetCloud.Modules.${module}\.Data" && return 0 || return 1
}

# Check whether a module's Host project has changed
module_host_changed() {
    local module="$1"
    echo "$CHANGED_FILES" | grep -q "^src/Modules/${module}/DotNetCloud.Modules.${module}.Host/" && return 0 || return 1
}

# Check whether a module's Client project has changed
module_client_changed() {
    local module="$1"
    echo "$CHANGED_FILES" | grep -q "^src/Modules/${module}/DotNetCloud.Modules.${module}.Client/" && return 0 || return 1
}

# Build a single project (or solution filter) with shared flags
do_build() {
    local target="$1"
    if $DRY_RUN; then
        echo "    [DRY-RUN] dotnet build $(basename "$target")"
        return 0
    fi
    log "Building $target..."
    dotnet build "$target" "${BUILD_FLAGS[@]}"
}

# Generate a deploy-scoped solution filter from the CI filter by removing
# projects that never ship to this server: the CLI, the desktop/mobile clients,
# the Example module, and all test projects. Their transitive server-side deps
# are still built because the kept projects reference them. Emits the temp file
# path on stdout. Self-maintaining — derived from DotNetCloud.CI.slnf each run.
generate_deploy_slnf() {
    local src="$REPO_ROOT/DotNetCloud.CI.slnf"
    # Must live in REPO_ROOT: the .slnf's solution/project paths are relative and
    # MSBuild resolves them against the filter file's own directory. Writing to
    # /tmp would make it look for /tmp/DotNetCloud.sln. Removed via the EXIT trap.
    local out="$REPO_ROOT/.deploy-generated.slnf"
    local exclude='DotNetCloud\.CLI|DotNetCloud\.Client|Modules\.Example|\.Tests\.|Benchmark'
    local -a kept=()
    local line
    while IFS= read -r line; do
        line="${line#"${line%%[![:space:]]*}"}"   # strip leading whitespace
        line="${line%,}"                            # strip trailing comma
        kept+=("$line")
    done < <(grep -E '\.csproj"' "$src" | grep -vE "$exclude")

    {
        echo '{'
        echo '  "solution": {'
        echo '    "path": "DotNetCloud.sln",'
        echo '    "projects": ['
        local n=${#kept[@]} i
        for i in "${!kept[@]}"; do
            if [ "$i" -lt "$((n - 1))" ]; then
                echo "      ${kept[$i]},"
            else
                echo "      ${kept[$i]}"
            fi
        done
        echo '    ]'
        echo '  }'
        echo '}'
    } > "$out"
    echo "$out"
}

# Publish a project to a target directory
do_publish() {
    local csproj="$1"
    local out_dir="$2"
    if $DRY_RUN; then
        echo "    [DRY-RUN] dotnet publish $(basename "$csproj") → $out_dir"
        return 0
    fi
    # --no-restore is safe because build step (if any) already restored everything.
    # Prevents implicit restore failures (e.g. browser-wasm RID not in cache).
    dotnet publish "$csproj" "${BUILD_FLAGS[@]}" -o "$out_dir" --no-self-contained --no-build --no-restore
}

# Print the deploy summary
print_summary() {
    local elapsed=$1
    local failures=$2
    local total=$3
    local success_count=$((total - failures))

    echo ""
    echo "════════════════════════════════════════════"
    echo "  Deploy summary"
    echo "════════════════════════════════════════════"
    echo "  Elapsed: ${elapsed}s"
    if [ "$failures" -eq 0 ]; then
        echo "  Result:  ✓ All $total targets succeeded"
    else
        echo "  Result:  ⚠ $success_count/$total succeeded, $failures failed"
    fi
    echo "════════════════════════════════════════════"
}

# Verify deployed assemblies match build output
verify_assemblies() {
    echo ""
    log "Verifying assembly hashes..."

    local verify_failures=0
    verify_file() {
        local deployed="$1"
        local built="$2"
        if [ ! -f "$deployed" ]; then
            echo "  ✗ MISSING: $deployed"
            ((verify_failures++))
            return
        fi
        if [ ! -f "$built" ]; then
            echo "  ⚠ SKIP (no build output): $built"
            return
        fi
        local deployed_hash
        local built_hash
        deployed_hash=$(md5sum "$deployed" | cut -d' ' -f1)
        built_hash=$(md5sum "$built" | cut -d' ' -f1)
        if [ "$deployed_hash" = "$built_hash" ]; then
            echo "  ✓ $(basename "$deployed")"
        else
            echo "  ✗ $(basename "$deployed") — HASH MISMATCH"
            ((verify_failures++))
        fi
    }

    # Check Core.Server DLLs
    verify_file "$DEPLOY_DIR/DotNetCloud.Core.Server.dll" \
        "$REPO_ROOT/src/Core/DotNetCloud.Core.Server/bin/$CONFIG/net10.0/DotNetCloud.Core.Server.dll"

    # Check module host DLLs
    for module in "${PUBLISHED_MODULES[@]}"; do
        local module_lower
        module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
        # Read the AssemblyName from the .csproj (defaults to project filename if not set)
        local csproj="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj"
        local assembly_name
        if [ -f "$csproj" ]; then
            assembly_name=$(grep -oP '<AssemblyName>\K[^<]+' "$csproj" || echo "DotNetCloud.Modules.$module.Host")
        else
            assembly_name="DotNetCloud.Modules.$module.Host"
        fi
        verify_file "$MODULES_DIR/dotnetcloud.$module_lower/$assembly_name.dll" \
            "$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/bin/$CONFIG/net10.0/$assembly_name.dll"
    done

    if [ "$verify_failures" -gt 0 ]; then
        echo ""
        log "⚠ $verify_failures assembly(ies) have mismatched or missing hashes!"
        return 1
    else
        echo ""
        log "✓ All assemblies verified."
    fi
}

# ============================================================================
# Startup banner
# ============================================================================
echo ""
echo "╔══════════════════════════════════════════════════╗"
echo "║   DotNetCloud Deploy                            ║"
echo "╚══════════════════════════════════════════════════╝"
echo "  Config:   $CONFIG"
echo "  Mode:     $([ "$FORCE" = true ] && echo 'FULL (--force)' || echo 'Incremental')"
$DRY_RUN && echo "  DRY RUN:  No changes will be applied"
$SKIP_BUILD && echo "  Build:    skipped (--skip-build)"
[ -n "$SKIP_MODULES" ] && echo "  Skip:     $SKIP_MODULES"
echo ""

# ============================================================================
# Phase 1: Determine what changed
# ============================================================================
log "Checking last deployed state..."
LAST_DEPLOYED=$(get_last_deployed_commit)
CURRENT_HEAD=$(cd "$REPO_ROOT" && git rev-parse HEAD 2>/dev/null || echo "unknown")

if [ -n "$LAST_DEPLOYED" ]; then
    log "Last deployed: ${LAST_DEPLOYED:0:12}  Current HEAD: ${CURRENT_HEAD:0:12}"
else
    log "No previous deploy state found — will perform full build."
fi

# Get changed files (empty means either no changes or no state file)
CHANGED_FILES=""
if [ -n "$LAST_DEPLOYED" ]; then
    CHANGED_FILES=$(get_changed_files)
fi

# Determine if we're doing a full solution build or incremental
FULL_BUILD=false
if [ "$FORCE" = true ] || [ -z "$LAST_DEPLOYED" ]; then
    FULL_BUILD=true
fi

if [ -z "$CHANGED_FILES" ] && [ -n "$LAST_DEPLOYED" ] && [ "$FORCE" = false ]; then
    echo ""
    log "✓ No code changes detected since last deploy."
    if $SKIP_STOP; then
        log "  (--skip-stop set, no restart needed)"
        echo ""
        echo "=== Nothing to deploy ==="
        exit 0
    fi
    log "  Restarting service (config/env may have changed)..."
    systemctl restart dotnetcloud 2>/dev/null || true
    log "✓ Service restarted."
    echo ""
    echo "=== Nothing to deploy, service restarted ==="
    exit 0
fi

# Determine which modules have changes
CHANGED_MODULES=()
CHANGED_DATA_MODULES=()
for module in "${MODULES[@]}"; do
    if module_has_changes "$module"; then
        CHANGED_MODULES+=("$module")
        if module_data_changed "$module"; then
            CHANGED_DATA_MODULES+=("$module")
        fi
    fi
done

# Check core/UI changes
CORE_CHANGED=false
for core_dir in "${CORE_PROJECTS[@]}"; do
    if dir_has_changes "$core_dir"; then
        CORE_CHANGED=true
        break
    fi
done

UI_CHANGED=false
for ui_dir in "${UI_PROJECTS[@]}"; do
    if dir_has_changes "$ui_dir"; then
        UI_CHANGED=true
        break
    fi
done

# If full build already determined (--force or no state file), skip further checks
if [ "$FULL_BUILD" = true ]; then
    log "Full build mode."
elif [ "$CORE_CHANGED" = true ] || [ "$UI_CHANGED" = true ]; then
    FULL_BUILD=true
    log "Core or UI changes detected — full build required."
elif [ "${#CHANGED_MODULES[@]}" -gt 0 ]; then
    log "Module changes detected: ${CHANGED_MODULES[*]}"
else
    log "Non-code changes detected (scripts, docs, etc.) — no build needed."
fi

# ============================================================================
# Dry-run display
# ============================================================================
if $DRY_RUN; then
    echo ""
    log "═══ Dry Run ═══"
    if [ -n "$LAST_DEPLOYED" ]; then
        echo "  Last deploy:  ${LAST_DEPLOYED:0:12}"
        echo "  Current HEAD: ${CURRENT_HEAD:0:12}"
    fi
    if [ -n "$CHANGED_FILES" ]; then
        echo ""
        echo "  Changed files:"
        echo "$CHANGED_FILES" | sed 's/^/    /'
    fi
    echo ""
    if $FULL_BUILD; then
        echo "  Build: FULL (solution filter)"
    else
        echo "  Build: Incremental"
        if [ "${#CHANGED_MODULES[@]}" -gt 0 ]; then
            echo "  Changed modules: ${CHANGED_MODULES[*]}"
        fi
    fi
    echo ""
    echo "  Steps:"
    echo "    [build] $($FULL_BUILD && echo 'deploy.slnf (server + module hosts)' || echo "${#CHANGED_MODULES[@]} module(s)")"
    if ! $SKIP_STOP; then echo "    [stop]  dotnetcloud service (after build succeeds)"; fi
    if $FULL_BUILD; then
        echo "    [publish] Core.Server + all ${#MODULES[@]} module hosts"
    else
        _publish_count=${#CHANGED_MODULES[@]}
        [ "$_publish_count" -eq 0 ] && _publish_count="${#MODULES[@]}"
        _core_note=$( { $CORE_CHANGED || $UI_CHANGED || [ "${#CHANGED_DATA_MODULES[@]}" -gt 0 ]; } && echo "Core.Server + " || echo "(Core.Server unchanged, skipped) ")
        echo "    [publish] ${_core_note}$_publish_count module host(s)"
    fi
    if ! $SKIP_STOP; then echo "    [start]  dotnetcloud service"; fi
    if [ -n "$LAST_DEPLOYED" ]; then echo "    [save]   .last-deploy-commit = ${CURRENT_HEAD:0:12}"; fi
    if $VERIFY; then echo "    [verify] Assembly hash checks"; fi
    echo ""
    echo "=== Dry run complete — no changes applied ==="
    exit 0
fi

# ============================================================================
# Phase 2: Build (BEFORE stopping the service)
#
# Building is the long pole. Doing it while the old version still serves traffic
# means downtime is only the publish + restart window (seconds), not the whole
# compile. With `set -e`, a failed build exits here — the live service is left
# running untouched.
# ============================================================================
TOTAL_STEPS=7
if $VERIFY; then ((TOTAL_STEPS++)); fi

START_TIME=$(date +%s)

# Decide whether Core.Server's publish output needs refreshing. It only changes
# when its own inputs change (core/UI/data/module-library code) or on a full
# build; a module-host-only change leaves the deployed Core.Server copy current.
CORE_NEEDS_PUBLISH=false
if $FULL_BUILD || $CORE_CHANGED || $UI_CHANGED || [ "${#CHANGED_DATA_MODULES[@]}" -gt 0 ]; then
    CORE_NEEDS_PUBLISH=true
fi

if $SKIP_BUILD; then
    step "Building..."
    log "  (--skip-build, using existing build output)"
    # When skipping the build, force full publish of all modules (we can't
    # determine what changed without diffing build timestamps).
    FULL_BUILD=true
    CORE_NEEDS_PUBLISH=true
elif $FULL_BUILD; then
    step "Building..."
    DEPLOY_SLNF="$(generate_deploy_slnf)"
    log "  Building deploy solution filter (server + module hosts; CLI/clients/tests/Example excluded)..."
    do_build "$DEPLOY_SLNF"
else
    step "Building..."
    BUILD_TARGETS=()
    HAS_DATA_CHANGES=false
    HAS_MODULE_LIB_CHANGES=false

    for module in "${CHANGED_MODULES[@]}"; do
        if [ -n "${SKIP_MODULE_LOOKUP[$module]:-}" ]; then
            log "  Skipping $module (--skip-modules)"
            continue
        fi
        if module_data_changed "$module"; then
            HAS_DATA_CHANGES=true
        fi
        _host_csproj="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj"
        if [ -f "$_host_csproj" ]; then
            BUILD_TARGETS+=("$_host_csproj")
        fi
        # If module has changes outside Host/Data/Client (i.e. its Razor class
        # library referenced by Core.Server), build it so publish picks up the
        # new assembly, and flag Core.Server for rebuild.
        if ! module_host_changed "$module" && ! module_data_changed "$module" && ! module_client_changed "$module"; then
            _module_csproj="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module/DotNetCloud.Modules.$module.csproj"
            if [ -f "$_module_csproj" ]; then
                BUILD_TARGETS+=("$_module_csproj")
                HAS_MODULE_LIB_CHANGES=true
            fi
        fi
    done

    # If data or module-library projects changed, also rebuild Core.Server so its
    # publish picks up the new DLLs — and flag it for republish in Phase 4.
    if $HAS_DATA_CHANGES || $HAS_MODULE_LIB_CHANGES; then
        BUILD_TARGETS+=("$REPO_ROOT/src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj")
        CORE_NEEDS_PUBLISH=true
    fi

    if [ "${#BUILD_TARGETS[@]}" -eq 0 ]; then
        log "  No module builds needed (skipping build step)."
    else
        for target in "${BUILD_TARGETS[@]}"; do
            do_build "$target"
        done
    fi
fi

# ============================================================================
# Phase 3: Stop service (only now that the build has succeeded)
# ============================================================================
step "Stopping service..."
if $SKIP_STOP; then
    log "  (--skip-stop, leaving service running)"
else
    systemctl stop dotnetcloud 2>/dev/null || true
    log "  Service stopped."
fi

# ============================================================================
# Phase 4: Publish
# ============================================================================
step "Publishing Core.Server..."

# Ensure the CI solution filter is restored (including browser-wasm RID for
# Web.Client) before any publish step. Without this, implicit restore during
# publish can fail with NETSDK1112 on incremental/no-build deploys.
if ! $FULL_BUILD; then
    dotnet restore "$REPO_ROOT/DotNetCloud.CI.slnf" -r browser-wasm --no-dependencies 2>/dev/null || true
fi

CORE_SERVER_CSPROJ="$REPO_ROOT/src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj"
CORE_PUBLISHED=false
if $CORE_NEEDS_PUBLISH || [ ! -f "$DEPLOY_DIR/DotNetCloud.Core.Server.dll" ]; then
    do_publish "$CORE_SERVER_CSPROJ" "$DEPLOY_DIR"
    CORE_PUBLISHED=true
else
    # Core.Server's inputs are unchanged, so the deployed copy is already current.
    # Skipping the republish avoids re-copying its full (~hundreds of MB) closure.
    log "  Core.Server unchanged — skipping republish (deployed copy is current)."
fi

# Determine which module hosts to publish
PUBLISH_MODULES=()
if $FULL_BUILD; then
    PUBLISH_MODULES=("${MODULES[@]}")
else
    PUBLISH_MODULES=("${CHANGED_MODULES[@]}")
fi

# Filter out skipped modules
FILTERED_MODULES=()
for module in "${PUBLISH_MODULES[@]}"; do
    if [ -z "${SKIP_MODULE_LOOKUP[$module]:-}" ]; then
        FILTERED_MODULES+=("$module")
    else
        log "  Skipping $module (--skip-modules)"
    fi
done
PUBLISH_MODULES=("${FILTERED_MODULES[@]}")

step "Publishing module hosts (parallel)..."
PUBLISHED_MODULES=()

if [ "${#PUBLISH_MODULES[@]}" -eq 0 ]; then
    log "  No module hosts to publish."
else
    pids=()
    module_names=()

    for module in "${PUBLISH_MODULES[@]}"; do
        module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
        host_csproj="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj"
        out_dir="$MODULES_DIR/dotnetcloud.$module_lower"

        if [ ! -f "$host_csproj" ]; then
            log "  ⚠ $module.Host.csproj not found — skipping."
            continue
        fi

        (
            set -o pipefail
            if $DRY_RUN; then
                echo "    [DRY-RUN] publish $module → $out_dir"
            else
                dotnet publish "$host_csproj" "${BUILD_FLAGS[@]}" -o "$out_dir" --no-self-contained --no-build --no-restore 2>&1 | sed "s/^/[$module] /"
            fi
        ) &
        pids+=($!)
        module_names+=("$module")
        PUBLISHED_MODULES+=("$module")
    done

    # Track results
    publish_failures=0
    publish_success=0
    for i in "${!pids[@]}"; do
        exit_code=0
        wait "${pids[$i]}" || exit_code=$?
        if [ "$exit_code" -eq 0 ]; then
            echo "  ✓ ${module_names[$i]}.Host"
            publish_success=$((publish_success + 1))
        else
            echo "  ✗ ${module_names[$i]}.Host FAILED (exit code $exit_code)"
            publish_failures=$((publish_failures + 1))
        fi
    done

    log "Module hosts: $publish_success published, $publish_failures failed."

    # Phase 4b: Sync transitive dependency assemblies for each published module
    #
    # dotnet publish --no-build copies the module host assembly and its direct
    # NuGet package outputs, but may not reliably update ALL transitive .dll
    # files (e.g. Microsoft.IdentityModel.* when adding JwtBearer auth). This
    # step rsyncs the complete build output directory over the module directory
    # to catch any new/updated dependency assemblies that publish missed.
    #
    # We skip .pdb files (debug symbols are not deployed) and .config files
    # (module host config is managed separately).
    log "Syncing dependency assemblies..."
    for module in "${PUBLISHED_MODULES[@]}"; do
        module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
        build_dir="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/bin/$CONFIG/net10.0"
        out_dir="$MODULES_DIR/dotnetcloud.$module_lower"

        if [ ! -d "$build_dir" ]; then
            log "  ⚠ Build output not found for $module — skipping dependency sync."
            continue
        fi

        if ! $DRY_RUN; then
            # rsync all .dll and .json files, skip .pdb, .config, .xml
            rsync -a --checksum \
                --include="*.dll" --include="*.json" \
                --exclude="*.pdb" --exclude="*.config" --exclude="*.xml" \
                --exclude="*.nuspec" --exclude="*.pri" --exclude="*.rsp" \
                "$build_dir/" "$out_dir/" 2>/dev/null || true
        fi
    done
    log "  Dependency sync complete."

    # Phase 4c: Sync static web assets (JS, CSS, etc.) from module RCL wwwroot
    #
    # dotnet publish --no-build copies DLLs and NuGet packages but doesn't
    # always propagate wwwroot files (JS, CSS, images) to all required locations.
    # Module RCLs like DotNetCloud.Modules.Video ship video-player.js and other
    # static assets in their wwwroot/ directory. These must be available in:
    #  (a) the module host's wwwroot (served by module API controllers),
    #  (b) the core server's wwwroot (served as _content static assets).
    #
    # This step rsyncs wwwroot from the source RCL project into both locations.
    log "Syncing static web assets..."
    for module in "${PUBLISHED_MODULES[@]}"; do
        module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
        rcl_wwwroot="$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module/wwwroot"

        if [ ! -d "$rcl_wwwroot" ]; then
            continue
        fi

        # Destination 1: module host wwwroot
        host_wwwroot="$MODULES_DIR/dotnetcloud.$module_lower/wwwroot/_content/DotNetCloud.Modules.$module"
        # Destination 2: core server wwwroot
        core_wwwroot="$DEPLOY_DIR/wwwroot/_content/DotNetCloud.Modules.$module"

        if ! $DRY_RUN; then
            mkdir -p "$host_wwwroot" "$core_wwwroot"
            # Copy static assets as-is (no compression — ASP.NET Core handles
            # response compression at runtime).  Errors are surfaced so we
            # catch permission or disk-full issues before the deploy "succeeds".
            rsync -a --checksum "$rcl_wwwroot/" "$host_wwwroot/"
            rsync -a --checksum "$rcl_wwwroot/" "$core_wwwroot/"
        fi
        echo "  ✓ $module static assets"
    done
    log "  Static asset sync complete."
fi

# ============================================================================
# Phase 5: Fix permissions
# ============================================================================
step "Fixing permissions..."
# Only chown what we actually wrote this run, rather than recursively re-owning
# the whole multi-GB deploy tree every time. Core output lives at the top level
# of DEPLOY_DIR (everything except modules/); each module host lives in its own
# dir. A module-only deploy thus chowns one ~50 MB dir instead of ~3 GB.
if $CORE_PUBLISHED; then
    find "$DEPLOY_DIR" -maxdepth 1 -mindepth 1 ! -name modules \
        -exec chown -R "$SERVICE_USER:$SERVICE_USER" {} + 2>/dev/null \
        || log "  ⚠ Some core permissions could not be set (non-critical)."
fi
for module in "${PUBLISHED_MODULES[@]}"; do
    module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
    chown -R "$SERVICE_USER:$SERVICE_USER" "$MODULES_DIR/dotnetcloud.$module_lower" 2>/dev/null || true
done
# Ensure the container dirs themselves are owned correctly (cheap, non-recursive).
chown "$SERVICE_USER:$SERVICE_USER" "$DEPLOY_DIR" "$MODULES_DIR" 2>/dev/null || true
# Revert repo build artifacts back to the original user
ORIGINAL_USER="${SUDO_USER:-$(who am i | awk '{print $1}')}"
if [ -n "$ORIGINAL_USER" ] && [ "$ORIGINAL_USER" != "root" ]; then
    chown -R "$ORIGINAL_USER:$ORIGINAL_USER" "$REPO_ROOT/src" "$REPO_ROOT/tests" "$REPO_ROOT/tools" 2>/dev/null || true
fi
log "  Permissions set."

# ============================================================================
# Phase 6: Start service
# ============================================================================
step "Starting service..."
if $SKIP_STOP; then
    log "  (--skip-stop, service already running)"
else
    systemctl start dotnetcloud || log "  ⚠ Service start returned $? (may already be running or failed)"
    log "  Service started."
fi

# ============================================================================
# Phase 7: Save deploy state (optional)
# ============================================================================
if [ -n "$CURRENT_HEAD" ] && [ "$CURRENT_HEAD" != "unknown" ]; then
    echo "$CURRENT_HEAD" > "$STATE_FILE" 2>/dev/null || log "  ⚠ Could not save deploy commit to $STATE_FILE"
    chown "$SERVICE_USER:$SERVICE_USER" "$STATE_FILE" 2>/dev/null || true
    if [ -f "$STATE_FILE" ]; then
        log "  Saved deploy commit: ${CURRENT_HEAD:0:12}"
    fi
fi

# ============================================================================
# Phase 8: Verify (optional)
# ============================================================================
if $VERIFY; then
    step "Verifying deployed assemblies..."
    verify_assemblies || true
fi

# ============================================================================
# Done
# ============================================================================
END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

PUBLISH_COUNT=${#PUBLISHED_MODULES[@]}
TOTAL_FAILURES=${publish_failures:-0}

echo ""
print_summary "$ELAPSED" "$TOTAL_FAILURES" "$((PUBLISH_COUNT + 1))"

if [ "$TOTAL_FAILURES" -gt 0 ]; then
    exit 1
fi
