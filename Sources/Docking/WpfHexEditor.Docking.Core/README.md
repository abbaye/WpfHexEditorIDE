# WpfHexEditor.Docking.Core

Abstract, platform-agnostic docking engine — layout model, state machine, and tree-manipulation logic with no WPF dependency.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows (no WPF; `InternalsVisibleTo` for `WpfHexEditor.Docking.Wpf` and tests)

---

## Architecture / Modules

### Layout Model (`Nodes/`)

- **`DockNode`** — abstract base for all nodes in the layout tree.
- **`DockSplitNode`** — horizontal or vertical split; holds ordered children with proportional size ratios.
- **`DockGroupNode`** — leaf group containing a tab-strip of `DockItem` instances; tracks `ActiveItem`.
- **`DocumentHostNode`** — specialized `DockGroupNode` for document tabs; the `IsMain` flag identifies the primary document area.
- **`DockLayoutRoot`** — root of the entire layout tree; exposes `RootNode`, `MainDocumentHost`, `FloatingItems`, `AutoHideItems`, `HiddenItems`.

### State and Enumerations

- **`DockItemState`** — `Docked`, `Float`, `AutoHide`, `Hidden`
- **`DockDirection`** — `Center`, `Left`, `Right`, `Top`, `Bottom`
- **`DockSide`** — `Left`, `Right`, `Top`, `Bottom`
- **`DockLockMode`** — flags: `PreventSplitting`, `PreventUndocking`, `PreventClosing`
- **`SplitOrientation`** — `Horizontal`, `Vertical`

### Engine

- **`DockEngine`** — all tree-mutation operations:
  - `Dock(item, target, direction)` — dock relative to a group (Center = tab into group; directional = wrap in new split at 25 %).
  - `DockAtRoot(item, direction)` — wrap the entire root node, producing full-width/height outer panels.
  - `DockAsDocument(item, target?)` — dock as a tabbed document in the main host.
  - `SplitDocumentHost(item, host, direction)` — create a second document host at 50/50.
  - `Undock`, `Float`, `Close`, `Hide`, `Show` — individual item state transitions.
  - `AutoHide` / `RestoreFromAutoHide` — infers correct `DockSide` from tree position.
  - `AutoHideAll` / `RestoreAllFromAutoHide` — batch operations wrapped in a transaction.
  - `FloatGroup` — float an entire tab group as a single floating window.
  - `MoveItem` — move between groups.
  - `NormalizeTree` — prunes empty groups (except `MainDocumentHost`), collapses single-child splits, re-normalizes ratios.
  - `BeginTransaction` / `CommitTransaction` — defers `NormalizeTree` and `LayoutChanged` for batched operations.
- **`ILayoutUpdateStrategy`** — extension point for custom normalization strategies.

### Serialization (`Serialization/`)

Layout persistence contracts and helpers for saving/restoring the dock tree to/from JSON or XML.

### Commands (`Commands/`)

Command abstractions for docking operations (used by the WPF drag-and-drop layer in `WpfHexEditor.Docking.Wpf`).

### Additional

- **`RegexColorRule`** — color rule model for terminal/editor syntax highlighting, shared at the core layer.
- **`DocumentTabBarSettings`** — tab bar appearance configuration.

---

## Design Notes

- No WPF types are referenced; the engine is fully unit-testable without a UI thread.
- `WpfHexEditor.Docking.Wpf` consumes this library and renders the tree using WPF `Grid`, `TabControl`, and floating `Window` elements.
- `DockEngine` emits events (`ItemDocked`, `ItemFloated`, `LayoutChanged`, etc.) that the WPF layer subscribes to in order to rebuild the visual tree.
