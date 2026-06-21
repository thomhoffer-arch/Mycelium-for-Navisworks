<#
.SYNOPSIS
    One-click installer for the Mycelium Navisworks add-in.

.DESCRIPTION
    Detects every Navisworks Manage/Simulate install on this machine, builds the
    add-in against each one (the plugin must compile against that version's local,
    proprietary API DLLs — it can't be vendored), and deploys it into the
    Navisworks Plugins directory so it appears under Tools > Add-Ins.

    The add-in is split into two assemblies and BOTH must be deployed:
      - MyceliumNavisworks.dll        (the host-bound Clash Detective shim)
      - Mycelium.Navisworks.Core.dll  (its SDK-independent mapping dependency)

    Normally you don't run this directly — double-click install.cmd, which
    elevates first (the Plugins folder is under Program Files).

.PARAMETER NavisworksPath
    Install a single Navisworks. If omitted, every detected install is targeted.

.PARAMETER Configuration
    MSBuild configuration. Default: Release.

.PARAMETER Uninstall
    Remove the deployed add-in from every detected (or specified) Navisworks.

.EXAMPLE
    .\install.ps1
.EXAMPLE
    .\install.ps1 -NavisworksPath "C:\Program Files\Autodesk\Navisworks Manage 2024"
.EXAMPLE
    .\install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$NavisworksPath,
    [string]$Configuration = "Release",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$PluginFolderName = "MyceliumNavisworks"          # Navisworks loads plugins from a folder named after them
$PluginDll        = "MyceliumNavisworks.dll"
$CoreDll          = "Mycelium.Navisworks.Core.dll"
$Root             = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project          = Join-Path $Root "src\Mycelium.Navisworks.Plugin\Mycelium.Navisworks.Plugin.csproj"

function Write-Step($msg)  { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "    $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "    $msg" -ForegroundColor Yellow }

# --- Discover Navisworks installs -------------------------------------------
# A directory is a Navisworks install iff it contains Autodesk.Navisworks.Api.dll.
function Find-NavisworksInstalls {
    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $pf) { continue }
        $autodesk = Join-Path $pf "Autodesk"
        if (Test-Path $autodesk) {
            Get-ChildItem $autodesk -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like "Navisworks *" } |
                ForEach-Object { $candidates.Add($_.FullName) }
        }
    }

    # Registry-recorded install locations (covers non-default drives/paths).
    $regRoots = @(
        "HKLM:\SOFTWARE\Autodesk\Navisworks Manage",
        "HKLM:\SOFTWARE\Autodesk\Navisworks Simulate",
        "HKLM:\SOFTWARE\WOW6432Node\Autodesk\Navisworks Manage",
        "HKLM:\SOFTWARE\WOW6432Node\Autodesk\Navisworks Simulate"
    )
    foreach ($rr in $regRoots) {
        if (-not (Test-Path $rr)) { continue }
        Get-ChildItem $rr -ErrorAction SilentlyContinue | ForEach-Object {
            $loc = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).InstallLocation
            if ($loc) { $candidates.Add($loc.TrimEnd('\')) }
        }
    }

    $candidates |
        Where-Object { $_ -and (Test-Path (Join-Path $_ "Autodesk.Navisworks.Api.dll")) } |
        Select-Object -Unique
}

# --- Locate a build tool that can target net48 ------------------------------
function Find-MSBuild {
    # 1. vswhere (ships with VS 2017+ and Build Tools) — most reliable for net48.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -requires Microsoft.Component.MSBuild `
                    -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($path -and (Test-Path $path)) { return @{ Exe = $path; Kind = "msbuild" } }
    }
    # 2. msbuild already on PATH.
    $onPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($onPath) { return @{ Exe = $onPath.Source; Kind = "msbuild" } }
    # 3. dotnet as a fallback (works if the net48 targeting pack is present).
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { return @{ Exe = $dotnet.Source; Kind = "dotnet" } }
    return $null
}

function Get-PluginDir($navPath) {
    Join-Path (Join-Path $navPath "Plugins") $PluginFolderName
}

# --- Uninstall path ---------------------------------------------------------
if ($Uninstall) {
    Write-Step "Uninstalling Mycelium add-in"
    $targets = if ($NavisworksPath) { @($NavisworksPath) } else { @(Find-NavisworksInstalls) }
    if (-not $targets) { Write-Warn2 "No Navisworks installs found."; return }

    $removed = 0
    foreach ($nav in $targets) {
        $dir = Get-PluginDir $nav
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force
            Write-Ok "Removed $dir"
            $removed++
        }
    }
    if ($removed -eq 0) { Write-Warn2 "Nothing to remove (add-in not installed)." }
    else { Write-Host "`nDone. Restart Navisworks to unload the add-in." -ForegroundColor Green }
    return
}

# --- Install path -----------------------------------------------------------
Write-Step "Detecting Navisworks installations"
$targets = if ($NavisworksPath) {
    if (-not (Test-Path (Join-Path $NavisworksPath "Autodesk.Navisworks.Api.dll"))) {
        throw "No Navisworks API found at '$NavisworksPath'. Check the path."
    }
    @($NavisworksPath)
} else {
    @(Find-NavisworksInstalls)
}

if (-not $targets) {
    throw "No Navisworks Manage/Simulate install found. Install Navisworks first, or pass -NavisworksPath '<install dir>'."
}
foreach ($t in $targets) { Write-Ok "Found: $t" }

Write-Step "Locating build tools"
$tool = Find-MSBuild
if (-not $tool) {
    throw "No MSBuild or dotnet SDK found. Install Visual Studio Build Tools (with the .NET desktop workload) or the .NET SDK, then re-run."
}
Write-Ok "Using $($tool.Kind): $($tool.Exe)"

$installed = 0
foreach ($nav in $targets) {
    Write-Step "Building for $(Split-Path $nav -Leaf)"
    $outDir = Join-Path $Root "src\Mycelium.Navisworks.Plugin\bin\$Configuration\net48"

    if ($tool.Kind -eq "msbuild") {
        & $tool.Exe $Project /t:Restore,Build /p:Configuration=$Configuration `
            /p:NavisworksPath="$nav" /v:minimal /nologo
    } else {
        & $tool.Exe build $Project -c $Configuration /p:NavisworksPath="$nav" --nologo
    }
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $nav (exit $LASTEXITCODE)." }

    $builtPlugin = Join-Path $outDir $PluginDll
    $builtCore   = Join-Path $outDir $CoreDll
    foreach ($f in @($builtPlugin, $builtCore)) {
        if (-not (Test-Path $f)) { throw "Expected build output missing: $f" }
    }

    $dest = Get-PluginDir $nav
    Write-Step "Deploying to $dest"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item $builtPlugin $dest -Force
    Copy-Item $builtCore   $dest -Force   # REQUIRED: the plugin won't load without Core
    Write-Ok "Deployed $PluginDll + $CoreDll"
    $installed++
}

Write-Host ""
Write-Host "Installed into $installed Navisworks install(s)." -ForegroundColor Green
Write-Host "Restart Navisworks, then run:  Tools > Add-Ins > 'Mycelium: Export clashes'" -ForegroundColor Green
Write-Host ""
Write-Host "Optional config (environment variables):" -ForegroundColor Gray
Write-Host "  MYCELIUM_PROJECT_KEY   shared spine projectKey   (default: horizons)" -ForegroundColor Gray
Write-Host "  MYCELIUM_OUT           output JSONL path         (default: %TEMP%\mycelium-navisworks.jsonl)" -ForegroundColor Gray
