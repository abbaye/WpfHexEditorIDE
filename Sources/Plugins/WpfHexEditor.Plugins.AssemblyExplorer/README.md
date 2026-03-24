# WpfHexEditor.Plugins.AssemblyExplorer

**Project:** WpfHexEditor.Plugins.AssemblyExplorer
**Version:** 0.1.0
**License:** GNU Affero General Public License v3.0
**Platform:** net8.0-windows (WPF)
**Default dock:** Left
**Load priority:** 40

---

## Description

The Assembly Explorer plugin provides a Visual Studio-like tree panel for browsing .NET managed assemblies (PE/ECMA-335 files). It uses `System.Reflection.Metadata` and `System.Reflection.PortableExecutable` (both inbox in net8.0, no NuGet dependency) to parse the assembly's type system and expose namespaces, types, methods, fields, properties, events, and embedded resources in a collapsible tree. Selecting a node syncs the hex editor caret to the corresponding PE file offset.

---

## Features

- **.NET PE tree view** — namespaces, types (class/interface/struct/enum/delegate), methods, fields, properties, events, resources.
- **PE offset sync** — clicking any tree node navigates the hex editor to the node's raw PE offset.
- **Filter / search** — real-time filter box narrows the tree by name.
- **Visibility toggles** — show/hide resources and metadata tables independently.
- **Sort modes** — sort tree nodes by name or declaration order.
- **Expand / Collapse all** — single toolbar button pair.
- **Auto-sync with hex editor** — when enabled, opening a `.dll` or `.exe` in the editor automatically triggers assembly analysis.
- **Detail pane** (`AssemblyDetailPane`) — shows decompiled stub text for the selected member (decompiler backend is a stub placeholder for Phase 2 ILSpy integration).
- **Open Assembly dialog** — manual file picker for loading any PE file independently of the active editor tab.
- **Context menu** on each tree node:
  - Open in hex editor
  - Decompile (stub)
  - Copy name / full name / PE offset
- **IDE menu integration**:
  - `View > Assembly Explorer`
  - `Tools > Analyze Assembly` (Ctrl+Shift+A)
  - `Edit > Go to Metadata Token…` (stub)
- **Status bar** (right-aligned, order 20–21): `Assembly: {name} v{version}` | `{types} types | {methods} methods`.
- **6-group toolbar overflow** (`ToolbarOverflowManager`), collapse order: Decompile → Visibility → Sort → Sync → Expand/Collapse → Filter.
- **Options** persisted to `%AppData%\WpfHexaEditor\Plugins\AssemblyExplorer.json` (`AssemblyExplorerOptions`).
- **EventBus events** (plugin-private): `AssemblyLoadedEvent`, `AssemblyMemberSelectedEvent`.

---

## Panel

| Property | Value |
|---|---|
| Panel ID | `WpfHexEditor.Plugins.AssemblyExplorer.Panel.AssemblyExplorerPanel` |
| Default dock side | Left |
| Auto-hide | false |
| Preferred width | 300 |
| View menu entry | `View > Assembly Explorer` |

### Internal components

| Component | Responsibility |
|---|---|
| `AssemblyExplorerPanel.xaml(.cs)` | Main tree panel; 6-group toolbar overflow; context menu wiring |
| `AssemblyExplorerViewModel.cs` | Orchestrator; `LoadAssemblyAsync`, tree rebuild, filter, visibility toggle |
| `AssemblyDetailPane.xaml(.cs)` | Member detail / decompilation view |
| `AssemblyExplorerPlugin.cs` | Plugin entry point; registers panel + 3 menu items + 2 status bar items; wires `FileOpened`/`ActiveEditorChanged` |
| `AssemblyAnalysisService.cs` | PEReader pipeline; `CanAnalyze()` checks MZ signature; produces `AssemblyModel` |
| `PeOffsetResolver.cs` | **Stub** — all offsets return 0; Phase 2: full ECMA-335 §II.24 resolution |
| `DecompilerService.cs` | **Stub** — placeholder text; Phase 2: ILSpy/dnSpy backend |
| `AssemblyExplorerOptions.cs` | Persisted settings |

### Phase 2 TODO

- `PeOffsetResolver`: full ECMA-335 §II.24 metadata table offset resolution.
- `DecompilerService`: ILSpy decompiler backend via `WpfHexEditor.Core.Decompiler` (no `ProjectReference` yet).

### Architecture notes

- Pattern: **MVVM** — code-behind is thin; all tree state lives in `AssemblyExplorerViewModel`.
- BCL-only: `System.Reflection.Metadata` + `System.Reflection.PortableExecutable` are inbox in net8.0.
- `AssemblyLoaded` and `MemberSelected` ViewModel events are published to `IPluginEventBus` from the panel constructor, keeping the ViewModel free of SDK references.

### Theme compliance

All brushes use `DynamicResource` (`PFP_*` tokens). Panel registers itself with `IThemeService.RegisterThemeAwareControl` on `SetContext()`.
