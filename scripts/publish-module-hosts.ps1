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
    @{ Id = "dotnetcloud.chat"; Project = "src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj" },
    @{ Id = "dotnetcloud.files"; Project = "src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj" },
    @{ Id = "dotnetcloud.notes"; Project = "src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj" },
    @{ Id = "dotnetcloud.music"; Project = "src/Modules/Music/DotNetCloud.Modules.Music.Host/DotNetCloud.Modules.Music.Host.csproj" },
    @{ Id = "dotnetcloud.video"; Project = "src/Modules/Video/DotNetCloud.Modules.Video.Host/DotNetCloud.Modules.Video.Host.csproj" },
    @{ Id = "dotnetcloud.extraction"; Project = "src/Core/DotNetCloud.Core.Search.Extraction.Host/DotNetCloud.Core.Search.Extraction.Host.csproj" },
    @{ Id = "dotnetcloud.about"; Project = "src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj" }
)

# Store-hosted modules (dotnetcloud.tracks, .photos, .bookmarks, .email, .ai, .example)
# have moved to the DotNetCloud-ModuleStore repository. They must not be built from
# here — install/update them through the store instead:
#   dotnetcloud module install <id> --from-store

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
