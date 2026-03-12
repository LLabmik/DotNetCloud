# Android Development Environment Setup

> This guide walks through setting up the development environment for the DotNetCloud Android client on Windows 11 and Linux.

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | [dot.net/download](https://dot.net/download) |
| Android workload | Latest | Installed via .NET CLI |
| Android SDK | API 35 | Installed via workload or Android Studio |
| IDE | VS 2026 / Rider 2026+ | MAUI plugin required |
| JDK | 17+ | Bundled with Android workload |

## Step 1: Install .NET SDK

Download and install the .NET 10 SDK from [dot.net/download](https://dot.net/download).

Verify installation:

```powershell
dotnet --version
# Expected: 10.0.x
```

## Step 2: Install Android Workload

```powershell
dotnet workload install android
```

This installs the Android SDK, build tools, and platform tools. On Linux, you may need `sudo`:

```bash
sudo dotnet workload install android
```

Verify:

```powershell
dotnet workload list
# Should show: android   10.0.x/10.0.100   SDK 10.0.x
```

## Step 3: Configure Android SDK Path

The Android SDK is installed automatically by the workload. If you need a custom path (e.g., using Android Studio's SDK):

### Windows

```powershell
# In .csproj or Directory.Build.props:
$env:ANDROID_HOME = "C:\Users\<user>\AppData\Local\Android\Sdk"

# Or set MSBuild property:
dotnet build -p:AndroidSdkDirectory="C:\Users\<user>\AppData\Local\Android\Sdk"
```

### Linux

```bash
export ANDROID_HOME="$HOME/Android/Sdk"
# Or set MSBuild property:
dotnet build -p:AndroidSdkDirectory="$HOME/Android/Sdk"
```

## Step 4: Accept SDK Licenses

```powershell
# Windows (PowerShell)
& "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" --licenses

# Linux
$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager --licenses
```

## Step 5: Clone and Restore

```powershell
git clone https://github.com/LLabmik/DotNetCloud.git
cd DotNetCloud
dotnet restore
```

## Step 6: Build

```powershell
# Google Play build (default)
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj

# F-Droid build
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj -p:BuildFlavor=fdroid
```

## Step 7: Run on Emulator or Device

### Emulator

1. Create an emulator via Android Studio or `avdmanager`:
   ```powershell
   # List available system images
   & "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" --list | Select-String "system-images"

   # Install API 35 system image
   & "$env:ANDROID_HOME\cmdline-tools\latest\bin\sdkmanager.bat" "system-images;android-35;google_apis;x86_64"
   ```

2. Start the emulator.

3. Deploy:
   ```powershell
   dotnet build -t:Run src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj
   ```

### Physical Device

1. Enable **Developer Options** and **USB Debugging** on the device.
2. Connect via USB.
3. Verify connection:
   ```powershell
   & "$env:ANDROID_HOME\platform-tools\adb.exe" devices
   ```
4. Deploy:
   ```powershell
   dotnet build -t:Run src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj
   ```

## IDE Setup

### Visual Studio 2026

1. Install the **.NET MAUI** workload in the Visual Studio Installer.
2. Open `DotNetCloud.sln`.
3. Set `DotNetCloud.Client.Android` as the startup project.
4. Select an Android emulator or device target from the toolbar.
5. Press F5 to build and deploy.

### JetBrains Rider

1. Install the **MAUI** plugin from the plugin marketplace.
2. Open `DotNetCloud.sln`.
3. Select the Android run configuration.
4. Press Shift+F10 to run.

## Firebase Configuration (Google Play Build Only)

For push notifications in the Google Play build, you need a Firebase project:

1. Go to [Firebase Console](https://console.firebase.google.com/).
2. Create a project or use an existing one.
3. Add an Android app with package name `net.dotnetcloud.client`.
4. Download `google-services.json`.
5. Place it in `src/Clients/DotNetCloud.Client.Android/Platforms/Android/`.
6. Build with the default flavor (no `-p:BuildFlavor` flag needed).

> The `google-services.json` file is **not** checked into source control. The F-Droid build does not require it.

## UnifiedPush Configuration (F-Droid Build)

For push notifications in the F-Droid build:

1. Install a UnifiedPush distributor app on the device (e.g., ntfy, Gotify UP).
2. Configure the distributor to point to your notification server.
3. Build with `-p:BuildFlavor=fdroid`.
4. The app will register with the distributor on first launch.

## Troubleshooting

### `XA5300: Android SDK not found`

Set the `AndroidSdkDirectory` MSBuild property:
```powershell
dotnet build -p:AndroidSdkDirectory="<path-to-sdk>" src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj
```

### `JAVA_HOME is not set`

The Android workload bundles a JDK. If it's not detected:
```powershell
$env:JAVA_HOME = "$env:LOCALAPPDATA\Microsoft\Android\jdk\microsoft_dist_openjdk_17"
```

### Hot Reload Not Working

Ensure the `MauiVersion` in the project matches the installed workload version. Run `dotnet workload update` to align versions.

### Build Fails on Linux

Linux requires additional system packages:
```bash
# Ubuntu/Debian
sudo apt install -y libx11-dev libxrandr-dev

# Fedora
sudo dnf install -y libX11-devel libXrandr-devel
```
