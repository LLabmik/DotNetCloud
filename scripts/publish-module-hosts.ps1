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
    @{
        Id = "dotnetcloud.contacts"
        Project = "src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj"
    },
    @{
        Id = "dotnetcloud.calendar"
        Project = "src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj"
    }
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
