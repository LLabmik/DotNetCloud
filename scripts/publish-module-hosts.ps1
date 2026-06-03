#!/usr/bin/env pwsh
<#
.SYNOPSIS
Publishes the Contacts and Calendar module host processes to the modules directory
for process-isolated deployment with gRPC communication.
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "modules",
    [string]$SolutionRoot = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modules = @(
    @{ Id = "dotnetcloud.contacts"; Project = "src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj" },
    @{ Id = "dotnetcloud.calendar"; Project = "src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj" },
    @{ Id = "dotnetcloud.chat";     Project = "src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj" },
    @{ Id = "dotnetcloud.files";    Project = "src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj" },
    @{ Id = "dotnetcloud.notes";    Project = "src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj" },
    @{ Id = "dotnetcloud.tracks";   Project = "src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/DotNetCloud.Modules.Tracks.Host.csproj" },
    @{ Id = "dotnetcloud.music";    Project = "src/Modules/Music/DotNetCloud.Modules.Music.Host/DotNetCloud.Modules.Music.Host.csproj" },
    @{ Id = "dotnetcloud.photos";   Project = "src/Modules/Photos/DotNetCloud.Modules.Photos.Host/DotNetCloud.Modules.Photos.Host.csproj" },
    @{ Id = "dotnetcloud.video";    Project = "src/Modules/Video/DotNetCloud.Modules.Video.Host/DotNetCloud.Modules.Video.Host.csproj" },
    @{ Id = "dotnetcloud.search";   Project = "src/Modules/Search/DotNetCloud.Modules.Search.Host/DotNetCloud.Modules.Search.Host.csproj" },
    @{ Id = "dotnetcloud.bookmarks";Project = "src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj" },
    @{ Id = "dotnetcloud.email";    Project = "src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj" },
    @{ Id = "dotnetcloud.about";    Project = "src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj" },
    @{ Id = "dotnetcloud.ai";       Project = "src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj" }
)

Push-Location $SolutionRoot

try {
    foreach ($module in $modules) {
        $moduleOutput = Join-Path $OutputDir $module.Id
        Write-Host "Publishing $($module.Id) to $moduleOutput ..."

        dotnet publish $module.Project `
            -c $Configuration `
            -o $moduleOutput `
            --no-self-contained `
            /p:DebugType=None `
            /p:DebugSymbols=false

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish $($module.Id)"
            exit $LASTEXITCODE
        }

        Write-Host "  -> Published successfully"
    }

    Write-Host ""
    Write-Host "All module hosts published to $OutputDir/"
    Write-Host "Modules ready for process-isolated deployment."
}
finally {
    Pop-Location
}
