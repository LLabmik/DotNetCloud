param(
    [string]$DeviceSerial = "R5CWC356B2K",
    [string]$ApkPath,
    [string]$RuntimeIdentifier = "android-arm64",
    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"
$adb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
$repoRoot = $PSScriptRoot
$remote = "/data/local/tmp/dotnetcloud-install.apk"

function Log([string]$msg) {
    Write-Output ("[{0}] {1}" -f (Get-Date -Format "HH:mm:ss.fff"), $msg)
}

# ── Resolve APK ────────────────────────────────────────────────────────
if (-not $ApkPath) {
    # Prefer the RID-specific publish output (single-ABI APK). A stale
    # multi-ABI Signed APK sometimes lingers in the parent folder from an
    # interrupted build and is NOT valid for install — never pick it.
    $baseDir = Join-Path $repoRoot "src\Clients\DotNetCloud.Client.Android\bin\Debug\net10.0-android"
    $ridDir = Join-Path $baseDir $RuntimeIdentifier
    if (Test-Path $ridDir) {
        $ApkPath = (Get-ChildItem -Path $ridDir -Filter "*-Signed.apk" -Recurse -Depth 2 | Select-Object -First 1).FullName
    }
    if (-not $ApkPath) {
        $ApkPath = (Get-ChildItem -Path $baseDir -Filter "*-Signed.apk" -Recurse -Depth 2 | Select-Object -First 1).FullName
    }
}
if (-not $ApkPath -or -not (Test-Path $ApkPath)) { throw "APK not found: $ApkPath" }
$apkSizeMB = [math]::Round((Get-Item $ApkPath).Length / 1MB, 1)
Log "APK: $ApkPath ($apkSizeMB MB)"

# ── Device readiness ───────────────────────────────────────────────────
Log "Checking device: $DeviceSerial"
$devList = @(& $adb devices)
$readyLine = $devList | Where-Object { $_ -match "^$DeviceSerial\s+device" }
if (-not $readyLine) {
    Log "ERROR: device '$DeviceSerial' not ready. Devices attached:"
    $devList | ForEach-Object { Log "  $_" }
    throw "Device '$DeviceSerial' is not in a ready state (is it unlocked? USB debugging authorized?)."
}
Log "Device ready."

$free = & $adb -s $DeviceSerial shell df -h /data
Log "Device storage:"
$free | ForEach-Object { Log "  $_" }

# ── Push APK (shows progress) ──────────────────────────────────────────
Log "Pushing APK to $remote ..."
& $adb -s $DeviceSerial push $ApkPath $remote
if ($LASTEXITCODE -ne 0) { throw "adb push failed (exit $LASTEXITCODE)." }
Log "Push complete."

# ── Install via package manager ────────────────────────────────────────
Log "Running: pm install -r -t $remote"
& $adb -s $DeviceSerial shell pm install -r -t $remote
if ($LASTEXITCODE -ne 0) { throw "pm install failed (exit $LASTEXITCODE)." }
Log "Install complete."

# ── Cleanup temp APK ───────────────────────────────────────────────────
if (-not $KeepTemp) {
    & $adb -s $DeviceSerial shell rm -f $remote | Out-Null
    Log "Cleaned up temp APK."
}

# ── Launch app ─────────────────────────────────────────────────────────
$aapt = Get-ChildItem "C:\Program Files (x86)\Android\android-sdk\build-tools\*\aapt2.exe" | Select-Object -First 1 -ExpandProperty FullName
if ($aapt) {
    $badging = & $aapt dump badging $ApkPath
    $activity = ($badging | Select-String "launchable-activity:\s*name='([^']+)'").Matches.Groups[1].Value
    $pkg = ($badging | Select-String "package: name='([^']+)'").Matches.Groups[1].Value
    if ($activity -and $pkg) {
        Log "Launching: $pkg/$activity"
        & $adb -s $DeviceSerial shell am start -n "$pkg/$activity"
        Log "Launch command sent."
    }
    else {
        Log "Warning: could not parse APK manifest; app installed but not auto-launched."
    }
}
else {
    Log "Warning: aapt2 not found; app installed but not auto-launched."
}

Log "DONE."
