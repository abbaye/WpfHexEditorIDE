# WpfHexEditor.Decompiler.Core

`IDecompiler` contract and `DecompilerRegistry` — the extension point for plugging ILSpy, dnSpy, or any architecture-specific decompiler into the IDE.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows

---

## Architecture / Modules

- **`IDecompiler`** — interface that every decompiler implementation must satisfy:
  - `Architecture` — identifier string (e.g. `"x86"`, `"x86-64"`, `"ARM"`, `"WASM"`, `"IL"`).
  - `DisplayName` — human-readable name shown in the UI decompiler selector.
  - `CanDecompile(filePath)` — returns `true` when this decompiler can handle the given file (typically checks extension and/or magic bytes).
  - `DecompileAsync(filePath, ct)` — decompiles the file and returns the output text (assembly listing, pseudo-C, IL, etc.).
- **`DecompilerRegistry`** — static global registry (`List<IDecompiler>`).
  - `Register(IDecompiler)` — called at application startup for each decompiler implementation.
  - `All` — read-only list of all registered decompilers.

### Current state (stub)

No concrete `IDecompiler` implementation ships in this project. Integration with ILSpy or dnSpy is planned and will be delivered as a separate `WpfHexEditor.Decompiler.ILSpy` project that references this contract library. The `AssemblyExplorer` plugin uses `CSharpSkeletonEmitter` from `WpfHexEditor.Core.AssemblyAnalysis` as a fallback until a full decompiler backend is registered.

---

## Usage

```csharp
// Register at startup (e.g. in a future WpfHexEditor.Decompiler.ILSpy project)
DecompilerRegistry.Register(new ILSpyDecompiler());

// Consume in a panel or plugin
var decompiler = DecompilerRegistry.All
    .FirstOrDefault(d => d.CanDecompile(filePath));

if (decompiler is not null)
{
    string text = await decompiler.DecompileAsync(filePath, ct);
    // display in CodeEditor
}
```
