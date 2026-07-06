param(
    [string]$AvdName,
    [string]$DeviceSerial,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$env:ANDROID_HOME = "C:\Program Files (x86)\Android\android-sdk"

# Maps Android CPU ABIs to .NET RuntimeIdentifiers
$abiToRid = @{
    "armeabi-v7a" = "android-arm"
    "arm64-v8a"   = "android-arm64"
    "x86"         = "android-x86"
    "x86_64"      = "android-x64"
}

# --- Step 1: Resolve target device ---
Write-Output "=== Step 1: Resolve Target Device ==="
$devices = & "$env:ANDROID_HOME\platform-tools\adb.exe" devices 2>$null
$deviceLines = $devices -replace "`r", "" -split "`n" | Where-Object { $_ -match "^\S+\s+device$" }

if (-not $DeviceSerial) {
    # Auto-detect: prefer emulator, fall back to single physical device
    $emulatorLine = $deviceLines | Where-Object { $_ -match "^emulator-" }
    $physicalLines = $deviceLines | Where-Object { $_ -notmatch "^emulator-" }

    if ($emulatorLine) {
        $DeviceSerial = ($emulatorLine -split "\s+")[0]
        Write-Output "Using running emulator: $DeviceSerial"
    }
    elseif ($physicalLines.Count -eq 1) {
        $DeviceSerial = ($physicalLines[0] -split "\s+")[0]
        Write-Output "Using physical device: $DeviceSerial"
    }
    elseif ($physicalLines.Count -gt 1) {
        Write-Output "Multiple devices found. Please specify -DeviceSerial:"
        $deviceLines | ForEach-Object { Write-Output "  $($_ -split '\s+')[0]" }
        throw "Multiple devices — use -DeviceSerial to pick one."
    }
}

# If no device found yet, try starting an emulator
if (-not $DeviceSerial) {
    $avds = & "$env:ANDROID_HOME\emulator\emulator.exe" -list-avds 2>$null
    if (-not $avds) {
        throw "No AVDs found. Create one first with 'dotnet avd create' or Android Studio."
    }

    if (-not $AvdName) {
        $AvdName = $avds[0]
        Write-Output "No AVD specified. Using first available: $AvdName"
    }
    elseif ($avds -notcontains $AvdName) {
        Write-Output "Warning: Specified AVD '$AvdName' not found. Available AVDs:"
        $avds | ForEach-Object { Write-Output "  - $_" }
        $AvdName = $avds[0]
        Write-Output "Falling back to: $AvdName"
    }

    Write-Output "Starting emulator: $AvdName"
    Start-Process -NoNewWindow -FilePath "$env:ANDROID_HOME\emulator\emulator.exe" `
        -ArgumentList "-avd", $AvdName, "-no-snapshot-load"
    & "$env:ANDROID_HOME\platform-tools\adb.exe" wait-for-device

    $booted = $false
    $maxWait = 180
    $elapsed = 0
    while (-not $booted -and $elapsed -lt $maxWait) {
        Start-Sleep 5
        $elapsed += 5
        $prop = & "$env:ANDROID_HOME\platform-tools\adb.exe" shell getprop sys.boot_completed 2>$null
        if ($prop.Trim() -eq "1") {
            $booted = $true
            Write-Output "Emulator boot completed."
        }
    }
    if (-not $booted) {
        throw "Emulator did not finish booting within ${maxWait}s."
    }
    $DeviceSerial = "emulator-5554"
}

Write-Output "Target device: $DeviceSerial"

# --- Step 2: Detect device ABI and select RuntimeIdentifier ---
Write-Output "=== Step 2: Detect Device ABI ==="
$deviceAbi = & "$env:ANDROID_HOME\platform-tools\adb.exe" -s $DeviceSerial shell getprop ro.product.cpu.abi 2>$null
$deviceAbi = $deviceAbi.Trim()
Write-Output "Device CPU ABI: $deviceAbi"

$rid = $abiToRid[$deviceAbi]
if (-not $rid) {
    $rid = "android-arm64"
    Write-Output "Warning: Unknown ABI '$deviceAbi', defaulting to $rid"
}
else {
    Write-Output "Selected RuntimeIdentifier: $rid"
}

# android-arm (32-bit) requires Mono runtime
$useMono = ($rid -eq "android-arm")

# --- Step 3: Build the Android APK with correct ABI ---
Write-Output "=== Step 3: Build Android APK ($rid) ==="
$projectPath = Join-Path $repoRoot "src\Clients\DotNetCloud.Client.Android\DotNetCloud.Client.Android.csproj"

$publishFlags = @(
    "-f", "net10.0-android"
    "-c", $Configuration
    "-p:RuntimeIdentifier=$rid"
    "-p:RuntimeIdentifiers=$rid"
)
if ($useMono) {
    $publishFlags += "-p:UseMonoRuntime=true"
}

dotnet publish $projectPath $publishFlags
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

# --- Step 4: Locate the APK ---
Write-Output "=== Step 4: Locate APK ==="
$apkDir = Join-Path $repoRoot "src\Clients\DotNetCloud.Client.Android\bin\$Configuration\net10.0-android"
if ($rid -ne "android-arm64") {
    # Non-default RIDs go into a subfolder (android-arm, android-x64, etc.)
    $apkDir = Join-Path $apkDir $rid
}
$apk = Get-ChildItem -Path $apkDir -Filter "*-Signed.apk" -Recurse -Depth 2 | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem -Path $apkDir -Filter "*.apk" -Recurse -Depth 2 | Where-Object { $_.Name -notmatch "-Signed" } | Select-Object -First 1
}

if (-not $apk) {
    throw "No APK found in $apkDir after successful build."
}
Write-Output "Found APK: $($apk.FullName)"

# --- Step 5: Install APK on device ---
Write-Output "=== Step 5: Install APK ==="
& "$env:ANDROID_HOME\platform-tools\adb.exe" -s $DeviceSerial install -r $apk.FullName
if ($LASTEXITCODE -ne 0) {
    throw "APK install failed."
}

# --- Step 6: Launch App ---
Write-Output "=== Step 6: Launch App ==="
$aapt = Get-ChildItem "$env:ANDROID_HOME\build-tools\*\aapt2.exe" | Select-Object -First 1 -ExpandProperty FullName
if (-not $aapt) {
    throw "aapt2 not found under ANDROID_HOME\build-tools."
}
$badging = & $aapt dump badging $apk.FullName
$launchActivity = ($badging | Select-String "launchable-activity:\s*name='([^']+)'").Matches.Groups[1].Value
$packageName = ($badging | Select-String "package: name='([^']+)'").Matches.Groups[1].Value

if ($launchActivity -and $packageName) {
    Write-Output "Launching: $packageName/$launchActivity"
    & "$env:ANDROID_HOME\platform-tools\adb.exe" -s $DeviceSerial shell am start -n "$packageName/$launchActivity"
}
else {
    Write-Output "Warning: Could not parse APK manifest. App installed but not auto-launched."
}
