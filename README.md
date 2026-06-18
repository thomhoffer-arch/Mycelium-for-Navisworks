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

## Build — in one go (Windows)

Prerequisites: Navisworks Manage/Simulate installed locally (provides the API
DLLs — these are machine-local and can't be vendored), plus MSBuild
(Visual Studio or Build Tools) and .NET Framework 4.8 dev pack.

```powershell
# default path is Navisworks Manage 2024; override if yours differs
./build.ps1 -NavisworksPath "C:\Program Files\Autodesk\Navisworks Manage 2024"
```

or directly:

```powershell
msbuild mycelium-for-navisworks.csproj /p:Configuration=Release /p:NavisworksPath="<install dir>"
```

Output: `bin\Release\net48\MyceliumNavisworks.dll`.

## Install the plugin

Copy the DLL into a folder **named after the plugin** under the Navisworks
`Plugins` directory:

```
<Navisworks install>\Plugins\MyceliumNavisworks\MyceliumNavisworks.dll
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

```
mycelium-for-navisworks.csproj   net48, references local Navisworks DLLs
src/MyceliumExportPlugin.cs      the add-in: clashes → spine records → JSONL
src/IfcGuid.cs                   Revit UniqueId → IFC GlobalId
src/SpineRecord.cs               spine record + zero-dep JSON writer
build.ps1                        msbuild wrapper
```

License: Apache-2.0.
