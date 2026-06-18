# Build the Navisworks add-in. Override the Navisworks install path if needed.
param(
    [string]$NavisworksPath = "C:\Program Files\Autodesk\Navisworks Manage 2024",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path "$NavisworksPath\Autodesk.Navisworks.Api.dll")) {
    Write-Error "Navisworks API not found at '$NavisworksPath'. Pass -NavisworksPath '<install dir>'."
}

# Requires MSBuild (Visual Studio or Build Tools) on PATH.
msbuild mycelium-for-navisworks.csproj `
    /p:Configuration=$Configuration `
    /p:NavisworksPath="$NavisworksPath"

Write-Host "`nBuilt bin\$Configuration\net48\MyceliumNavisworks.dll" -ForegroundColor Green
Write-Host "Deploy: copy it to a folder named 'MyceliumNavisworks' under the Navisworks 'Plugins' directory (see README)."
