# mycelium-for-navisworks

Navisworks add-in that reads **Clash Detective** results and writes Connective
Spine records (JSONL) for an orchestrator (Loam) to ingest — the seamless,
file-free alternative to exporting BCF/Excel by hand. Join keys: `ifcGuid`
(primary) + `zone` + optional `classification`.

> **Is this even needed?** If your team runs the BIMcollab BCF Manager, your
> Navisworks clashes already reach BIMcollab Cloud and are read live by
> `mycelium-for-bcf-api` — no Navisworks code required. Build this connector
> only when you have **no BIMcollab**, want the **richer clash fields** (status,
> distance, grid, element pairs) BCF drops, or want **write-back** into
> Navisworks. See the registry note.

## Why this is a separate repo

It's a **C# / .NET Framework / Windows** in-process add-in — a different stack
and release cadence from the Node connectors, which is exactly why the Mycelium
registry keeps each connector in its own repo.

## Install — one click (Windows)

> **Windows only.** Navisworks ships for Windows only — there is no macOS build
> of Navisworks for this add-in to plug into. On a Mac, run Navisworks (and this
> installer) inside a Windows VM.

### The installer (`MyceliumSetup.exe`)

Download **`MyceliumSetup.exe`** from the
[latest release](https://github.com/thomhoffer-arch/Mycelium-for-Navisworks/releases)
and double-click it. It detects every Navisworks Manage/Simulate install on the
machine, builds the add-in against each, and deploys it. Then restart
Navisworks → run **Tools ▸ Add-Ins ▸ Mycelium: Export clashes**. Uninstall it
from **Add/Remove Programs** like any app.

The `.exe` carries the source and builds on your machine at install time — an
in-process .NET add-in must compile against your local, proprietary Navisworks
API DLLs, which can't be vendored or prebuilt. (The installer itself is built in
CI; see `.github/workflows/installer.yml` and `installer/mycelium.iss`.)

### Or the script (no `.exe`)

If you have the repo checked out, **double-click `install.cmd`** — same thing.
It self-elevates (the Navisworks `Plugins` folder lives under Program Files),
detects every Navisworks install, builds the add-in against each, and deploys it.

### Prerequisites

Either path needs, on the target machine:

- Navisworks Manage/Simulate installed locally.
- A build tool: **Visual Studio Build Tools** with the *.NET desktop* workload
  (or full Visual Studio), which provides MSBuild and the .NET Framework 4.8
  targeting pack. The .NET SDK alone works as a fallback if the net48 targeting
  pack is present.

To target one specific install, or to remove the add-in via the script:

```powershell
.\install.ps1 -NavisworksPath "C:\Program Files\Autodesk\Navisworks Manage 2024"
.\install.cmd /uninstall      # or: .\install.ps1 -Uninstall
```

### Build only (no deploy)

```powershell
# default path is Navisworks Manage 2024; override if yours differs
./build.ps1 -NavisworksPath "C:\Program Files\Autodesk\Navisworks Manage 2024"
```

or directly:

```powershell
msbuild src\Mycelium.Navisworks.Plugin\Mycelium.Navisworks.Plugin.csproj /p:Configuration=Release /p:NavisworksPath="<install dir>"
```

Output: `src\Mycelium.Navisworks.Plugin\bin\Release\net48\`.

### Manual deploy

If you deploy by hand, copy **both** DLLs — the add-in won't load without its
Core dependency — into a folder **named after the plugin** under the Navisworks
`Plugins` directory:

```
<Navisworks install>\Plugins\MyceliumNavisworks\
    MyceliumNavisworks.dll
    Mycelium.Navisworks.Core.dll
```

Restart Navisworks → run **Tools ▸ Add-Ins ▸ Mycelium: Export clashes**. It
writes JSONL to `%MYCELIUM_OUT%` (default `%TEMP%\mycelium-navisworks.jsonl`).

Configure via environment variables:

| Var | Meaning | Default |
|---|---|---|
| `MYCELIUM_PROJECT_KEY` | shared spine `projectKey` | `horizons` |
| `MYCELIUM_OUT` | output JSONL path | `%TEMP%\mycelium-navisworks.jsonl` |

## Output shape

Each line is `{ "identity": {…}, "freshness": {…} }`, conformant with the
Connective Spine SDK's `checkConformance`. One record **per element** in a clash
(each element joins on its own `ifcGuid`); clash test, status, and distance ride
along in `identity.text` for downstream edge extraction.

## Version caveats (read if the build complains)

A few Clash API members differ across Navisworks versions and are wrapped in
`try/catch` in `src/MyceliumExportPlugin.cs`:

- `ClashResult.Item1` / `Item2` (the two clashing `ModelItem`s),
- `ClashResult.Status` / `Distance` / `GridLocation`.

If a member name differs in your SDK, adjust the corresponding `Safe…` helper —
the spine mapping and IFC-GUID logic stay the same. IFC GlobalId resolution
(`TryGetIfcGuid`) reads the element's property categories and falls back to
deriving from a Revit `UniqueId` (`src/IfcGuid.cs`).

## Layout

The connector is split so the host-bound shim is tiny and the mapping logic is
testable without a Navisworks install:

```
src/Mycelium.Navisworks.Core/    netstandard2.0, NO Navisworks dependency
  IfcGuid.cs                       Revit UniqueId → IFC GlobalId (+ inverse, for tests)
  SpineRecord.cs                   spine record + zero-dep JSON writer
src/Mycelium.Navisworks.Plugin/  net48 add-in, references local Navisworks DLLs
  MyceliumExportPlugin.cs          clashes → Core → JSONL  (output: MyceliumNavisworks.dll)
tests/Mycelium.Navisworks.Tests/ net8.0 xUnit, exercises Core cross-platform
build.ps1                        msbuild wrapper for the add-in
.github/workflows/ci.yml         CI: Core tests on Linux; best-effort add-in build on Windows
```

## Tests / CI

The `Core` library has no Navisworks reference, so it builds and is unit-tested
on any platform:

```bash
dotnet test tests/Mycelium.Navisworks.Tests/Mycelium.Navisworks.Tests.csproj
```

CI runs those tests on Linux (the real gate). It also has a Windows job that
compiles the add-in **only** when a runner exposes the Navisworks API DLLs (set
the repo variable `NAVISWORKS_PATH`, e.g. on a self-hosted runner with
Navisworks installed) — hosted runners can't, so that job otherwise prints a
notice and passes. The authoritative add-in build is still `build.ps1` on a
machine with Navisworks.

License: Apache-2.0.
