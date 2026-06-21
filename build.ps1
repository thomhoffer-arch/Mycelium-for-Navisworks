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

$outDir = "src\Mycelium.Navisworks.Plugin\bin\$Configuration\net48"
Write-Host "`nBuilt $outDir\MyceliumNavisworks.dll" -ForegroundColor Green
Write-Host "Deploy BOTH DLLs (the plugin won't load without Core) to a folder named"
Write-Host "'MyceliumNavisworks' under the Navisworks 'Plugins' directory:"
Write-Host "  - $outDir\MyceliumNavisworks.dll"
Write-Host "  - $outDir\Mycelium.Navisworks.Core.dll"
Write-Host "`nOr just run install.cmd (one-click: detects Navisworks, builds, deploys both)." -ForegroundColor Cyan
