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
$project = "src\Mycelium.Navisworks.Plugin\Mycelium.Navisworks.Plugin.csproj"
msbuild $project `
    /p:Configuration=$Configuration `
    /p:NavisworksPath="$NavisworksPath"

$dll = "src\Mycelium.Navisworks.Plugin\bin\$Configuration\net48\MyceliumNavisworks.dll"
Write-Host "`nBuilt $dll" -ForegroundColor Green
Write-Host "Deploy: copy it to a folder named 'MyceliumNavisworks' under the Navisworks 'Plugins' directory (see README)."
