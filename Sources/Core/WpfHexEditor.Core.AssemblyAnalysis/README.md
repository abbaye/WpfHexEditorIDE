# WpfHexEditor.Core.AssemblyAnalysis

BCL-only .NET PE analysis pipeline — parses managed assemblies using `System.Reflection.Metadata` and `System.Reflection.PortableExecutable` with no external NuGet dependencies.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows

---

## Architecture / Modules

### Service Layer (`Services/`)

- **`IAssemblyAnalysisEngine`** — contract: `CanAnalyze(filePath)` (MZ signature check), `AnalyzeAsync(filePath, ct)` → `AssemblyModel`.
- **`AssemblyAnalysisEngine`** — concrete implementation (Strategy pattern).
  - Validates MZ magic bytes (`0x4D 0x5A`) before opening the file.
  - Uses `PEReader` + `MetadataReader` pipeline; all heavy work is offloaded via `Task.Run`.
  - Enumerates type definitions: base type, interface list, custom attributes, `TargetFrameworkAttribute` detection.
  - Supports cancellation before each `foreach` body — safe on large assemblies (e.g. `System.Runtime` with ~40 K types).
- **`PeOffsetResolver`** — resolves metadata table row handles to physical file offsets using ECMA-335 §II.24 table layout. Enables the hex editor to navigate directly to a metadata token's raw bytes.
- **`SignatureDecoder`** — decodes `MethodSignature<TType>` blobs into human-readable strings for `MemberModel.Signature`.
- **`CSharpSkeletonEmitter`** — emits a minimal C# skeleton (type + member stubs) from an `AssemblyModel`; used as a fallback display when a full decompiler is unavailable.
- **`IlTextEmitter`** — emits a raw IL text listing from method bodies.

### Domain Model (`Models/`)

- **`AssemblyModel`** — top-level result: `Name`, `Version`, `TargetFramework`, `Sections` (PE sections), `Types`.
- **`TypeModel`** — type metadata: `Name`, `Namespace`, `Kind` (Class/Interface/Struct/Enum/Delegate), `BaseTypeName`, `InterfaceNames`, `CustomAttributes`, `Members`.
- **`MemberModel`** — method, property, field, or event: `Name`, `Kind`, `Signature`, `FileOffset`.
- **`PeOffsetMap`** — maps `EntityHandle` values to file byte offsets; produced by `PeOffsetResolver`.

---

## Design Notes

- All BCL inbox types (`System.Reflection.Metadata`, `System.Reflection.PortableExecutable`) are available in `net8.0` without NuGet packages.
- `PEReader` must remain open for the entire enumeration session; `AssemblyAnalysisEngine` manages its lifetime inside the `Task.Run` closure.
- The engine is shared between the `AssemblyExplorer` plugin and any future plugin that needs PE introspection; it is exposed to plugins through `IAssemblyAnalysisEngine` via `IIDEHostContext` (see `WpfHexEditor.SDK`).
- Native PE files (no managed metadata) are partially supported: sections are enumerated, but type/member data is absent.

---

## Usage

```csharp
IAssemblyAnalysisEngine engine = new AssemblyAnalysisEngine();

if (engine.CanAnalyze(filePath))
{
    AssemblyModel model = await engine.AnalyzeAsync(filePath, cancellationToken);
    // model.Types, model.Sections, model.TargetFramework ...
}
```
