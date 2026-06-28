$ErrorActionPreference = "Stop"
$env:ANDROID_HOME = "C:\Program Files (x86)\Android\android-sdk"

Write-Output "=== Step 3: Check AVD / Start Emulator ==="
& "$env:ANDROID_HOME\emulator\emulator.exe" -list-avds

$avdName = "pixel_7_google_apis"
$devices = & "$env:ANDROID_HOME\platform-tools\adb.exe" devices
if ($devices -match "emulator-5554.*device") {
    Write-Output "Emulator already running"
} else {
    Write-Output "Starting emulator: $avdName"
    Start-Process -NoNewWindow -FilePath "$env:ANDROID_HOME\emulator\emulator.exe" -ArgumentList "-avd", $avdName, "-no-snapshot-load"
    Write-Output "Waiting for emulator..."
    & "$env:ANDROID_HOME\platform-tools\adb.exe" wait-for-device
    Start-Sleep 30
}

Write-Output "=== Step 4: Find and Install APK ==="
$apk = Get-ChildItem -Path "D:\Repos\DotNetCloud\src\Clients\DotNetCloud.Client.Android\bin\Debug\net10.0-android" -Filter "*.apk" -Recurse | Select-Object -First 1
if ($apk) {
    Write-Output "Found APK: $($apk.FullName)"
    & "$env:ANDROID_HOME\platform-tools\adb.exe" install -r $apk.FullName
    Write-Output "Launching app..."
    & "$env:ANDROID_HOME\platform-tools\adb.exe" shell am start -n "net.dotnetcloud.client/net.dotnetcloud.client.MainActivity"
} else {
    Write-Output "No APK found at expected path. Check build output."
    Get-ChildItem "D:\Repos\DotNetCloud\src\Clients\DotNetCloud.Client.Android\bin\Debug\net10.0-android" | Select-Object Name, Extension | Format-Table
}
