# WpfHexEditor.Editor.XamlDesigner

**Type:** Class Library (`net8.0-windows`)
**Role:** Full split-pane visual XAML designer editor — live WPF canvas with bidirectional code↔canvas sync, adorner-based interaction, overkill undo/redo, and 9 VS-Like dockable panels.

---

## Responsibility

`WpfHexEditor.Editor.XamlDesigner` provides:

- Live XAML rendering on a design canvas (via `XamlReader.Parse`)
- Bidirectional selection sync between the code editor and the canvas (~95% fidelity)
- Move/resize/rotate handles via an adorner system
- Snap-to-grid and element edge snapping with visual guide lines
- Multi-select and 12 alignment/distribution operations
- A full undo/redo system with single ops, batch grouping, and full XAML snapshots
- 9 dockable VS-Like panels (Toolbox, Properties, Outline, History, LiveTree, Resources, DesignData, Bindings, Animation)
- 4 split layout modes (`Ctrl+Shift+L` to cycle)
- `#region` colorization, error card overlay, design-time data support

---

## Architecture

### Entry Point: `XamlDesignerFactory`

Implements `IEditorFactory`; registered with `EditorRegistry` at IDE startup. Creates `XamlDesignerSplitHost` instances for `.xaml` files.

### Split-Pane Composite: `XamlDesignerSplitHost`

The top-level `IDocumentEditor`. Hosts:
- **Left/code pane:** `CodeEditorSplitHost` (from `WpfHexEditor.Editor.CodeEditor`)
- **Right/design pane:** `ZoomPanCanvas` wrapping `DesignCanvas`
- **Layout modes:** `HorizontalDesignRight` (default) · `HorizontalDesignLeft` · `VerticalDesignBottom` · `VerticalDesignTop`
- Single source-of-truth layout controller: `UpdateGridLayout()` rebuilds column/row defs + grid splitter on each mode change

### Design Canvas: `DesignCanvas`

- Renders XAML via `XamlReader.Parse` into a `ContentControl`
- Before render: `DesignToXamlSyncService.InjectUids()` tags each element with `Tag="xd_N"` (pre-order index)
- After render: `XamlElementMapper.Build()` reads Tags → bidirectional UID↔UIElement↔XElement maps
- On parse error: displays inline error card instead of blank surface

### Undo/Redo System (`DesignUndoManager`)

Three entry types sharing `IDesignUndoEntry`:

| Type | Use case |
|------|---------|
| `SingleDesignUndoEntry` | One attribute diff (Move / Resize / Rotate / PropertyChange) |
| `BatchDesignUndoEntry` | Multiple ops grouped atomically (Alignment operations) |
| `SnapshotDesignUndoEntry` | Full XAML before/after (structural: Insert / Delete) |

- Max depth: 200 entries; oldest auto-trimmed
- Jump-to-state: computes undo/redo count to reach any history entry
- `DesignHistoryPanel` mirrors the stack for VS-like history navigation

### Bidirectional Sync (`XamlSourceLocationService`)

- **Code → Canvas:** caret line → find UID at element → `SelectElement(uid)` on canvas
- **Canvas → Code:** element click → find UID in mapper → locate XElement line → set code caret
- Debounced 150ms; guarded by `_isSyncingSelection` flag to prevent feedback loops

### Adorner System

| Adorner | Purpose |
|---------|---------|
| `SelectionAdorner` | Non-interactive dashed border (Phase 1 selection) |
| `ResizeAdorner` | 8 Thumb handles + rotation arc; delegates to `DesignInteractionService` |
| `HoverAdorner` | Hover highlight feedback |
| `MultiSelectionAdorner` | Visual for multi-element selection |
| `RubberBandAdorner` | Drag-to-select rubber band |

### Snap System

- `SnapEngineService` calculates snapped positions (grid + sibling element edges) within a configurable threshold
- Returns `IReadOnlyList<SnapGuide>` for visual feedback
- `SnapGuideOverlay` renders guide lines in real-time during drag

---

## File Structure

```
WpfHexEditor.Editor.XamlDesigner/
├── XamlDesignerFactory.cs                      — IEditorFactory for .xaml files
│
├── Controls/
│   ├── DesignCanvas.cs                         — Live WPF rendering surface
│   ├── XamlDesignerSplitHost.cs                — Main split-pane document editor
│   ├── ZoomPanCanvas.cs                        — Zoom (Ctrl+Wheel) + pan wrapper
│   ├── Adorners/
│   │   ├── SelectionAdorner.cs                 — Dashed border selection
│   │   ├── ResizeAdorner.cs                    — 8-handle + rotation thumb adorner
│   │   ├── HoverAdorner.cs                     — Mouse-over highlight
│   │   ├── MultiSelectionAdorner.cs            — Multi-element selection visual
│   │   └── RubberBandAdorner.cs                — Drag-to-select rubber band
│   ├── Guides/
│   │   └── SnapGuideOverlay.cs                 — Real-time snap guide lines
│   ├── Rulers/
│   │   ├── RulerControl.cs                     — Horizontal/vertical ruler
│   │   └── TimelineRuler.cs                    — Animation timeline ruler
│   └── Editors/                                — Custom property editors
│       ├── ColorPickerEditor.xaml/.cs          — Color DP editor
│       ├── EnumEditor.xaml/.cs                 — Enum DP combo editor
│       ├── FontPickerEditor.xaml/.cs           — Font picker
│       ├── NumericSliderEditor.xaml/.cs        — Numeric slider editor
│       └── ThicknessEditor.xaml/.cs            — Margin/Padding editor
│
├── Models/
│   ├── XamlDocument.cs                         — Pure domain: XAML text + parse state (no WPF)
│   ├── XamlElementMapper.cs                    — UID ↔ UIElement ↔ XElement bidirectional maps
│   ├── DesignOperation.cs                      — Immutable op record (before/after attr dicts)
│   ├── ToolboxItem.cs                          — Toolbox item metadata + default XAML snippet
│   ├── PropertyInspectorEntry.cs               — DependencyProperty descriptor
│   └── DesignDataSource.cs                     — Design-time data model
│
├── Services/
│   ├── DesignInteractionService.cs             — Drag-move/resize state machine
│   ├── DesignToXamlSyncService.cs              — UID injection + XAML attribute patching
│   ├── DesignUndoManager.cs                    — Overkill undo/redo (Single/Batch/Snapshot)
│   ├── XamlSourceLocationService.cs            — Bidirectional code↔canvas selection sync
│   ├── SnapEngineService.cs                    — Grid + element edge snap calculations
│   ├── AlignmentService.cs                     — 12 alignment/distribution operations
│   ├── PropertyInspectorService.cs             — DP reflection + categorization
│   ├── PropertyEditorRegistry.cs              — Custom property editor registry
│   ├── ToolboxDropService.cs                   — Toolbox DnD insertion handler
│   ├── ToolboxRegistry.cs                      — Catalog of available toolbox items
│   ├── DesignTimeXamlPreprocessor.cs           — Filters d:* attributes before preview
│   ├── DesignTimeDataService.cs                — d:DataContext binding support
│   ├── LiveVisualTreeService.cs                — Live canvas visual tree enumeration
│   ├── ResourceScannerService.cs               — Scans XAML for resource definitions
│   ├── ResourceReferenceService.cs             — StaticResource/DynamicResource lookup
│   ├── XamlDesignPropertyProvider.cs           — SDK IPropertyProvider implementation
│   ├── BindingInspectorService.cs              — Binding expression analysis
│   ├── StoryboardSyncService.cs                — Animation Storyboard synchronization
│   └── AnimationPreviewService.cs              — Real-time animation playback
│
├── ViewModels/
│   ├── DesignCanvasViewModel.cs                — Grid/snap/ruler settings (INPC)
│   ├── DesignHistoryPanelViewModel.cs          — History panel state + jump-to-state command
│   ├── DesignHistoryEntryViewModel.cs          — Single history row
│   ├── XamlOutlinePanelViewModel.cs            — Outline tree root + selection
│   ├── XamlOutlineNode.cs                      — Recursive outline tree node
│   ├── XamlToolboxPanelViewModel.cs            — Searchable toolbox items grid
│   ├── PropertyInspectorPanelViewModel.cs      — Reflected DP table
│   ├── ResourceBrowserPanelViewModel.cs        — Resource grid
│   ├── ResourceEntryViewModel.cs               — Single resource row
│   ├── LiveTreeNode.cs                         — Visual tree node
│   ├── AlignmentToolbarViewModel.cs            — Alignment button state
│   ├── ZoomPanViewModel.cs                     — Zoom level + pan offset
│   ├── AnimationTimelinePanelViewModel.cs      — Timeline tracks + keyframes
│   ├── AnimationTrackViewModel.cs              — Single animation track
│   ├── KeyframeViewModel.cs                    — Single keyframe
│   └── NullToVisibilityConverter.cs            — XAML converter utility
│
├── Panels/
│   ├── PropertyInspectorPanel.xaml/.cs         — F4 property grid (VS-Like)
│   ├── XamlToolboxPanel.xaml/.cs               — Drag-drop toolbox
│   ├── XamlOutlinePanel.xaml/.cs               — XAML element tree
│   ├── DesignHistoryPanel.xaml/.cs             — Undo history + jump-to-state
│   ├── LiveVisualTreePanel.xaml/.cs            — Live runtime visual tree
│   ├── ResourceBrowserPanel.xaml/.cs           — Static/dynamic resource browser
│   ├── DesignDataPanel.xaml/.cs                — Design-time data editor
│   ├── BindingInspectorPanel.xaml/.cs          — Binding diagnostics
│   └── AnimationTimelinePanel.xaml/.cs         — Keyframe animation editor
│
└── Themes/
    └── Generic.xaml                            — 30 XD_* brush tokens × 8 themes
```

---

## Dockable Panels

| Panel ID | Name | Default Side |
|----------|------|-------------|
| `xd-property-inspector` | Property Inspector (F4) | Right, 260px |
| `xd-toolbox` | XAML Toolbox | Left, auto-hide, 220px |
| `xd-outline` | XAML Outline | Left, auto-hide, 220px |
| `xd-history` | Design History | Right, auto-hide, 240px |
| `xd-live-tree` | Live Visual Tree | Left, auto-hide, 240px |
| `xd-resources` | Resource Browser | Right, auto-hide, 260px |
| `xd-design-data` | Design Data | Bottom, auto-hide, 180px |
| `xd-bindings` | Binding Inspector | Right, auto-hide, 280px |
| `xd-animation-timeline` | Animation Timeline | Bottom, auto-hide, 180px |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` | Code Only view |
| `Ctrl+2` | Split view (default) |
| `Ctrl+3` | Design Only view |
| `Ctrl+Shift+L` | Cycle split layout (4 modes) |
| `Ctrl++` / `Ctrl+-` | Zoom in / out |
| `Ctrl+0` | Reset zoom to 100% |
| `F7` | Toggle code/design focus |
| `Delete` | Delete selected element |
| `Escape` | Select parent element (hierarchical, VS-Like) |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo design operations |

---

## Interaction Cycle

```
User clicks element on canvas
  └─ Hit-test at mouse position (Alt+Click cycles depth)
  └─ SelectElement() → ResizeAdorner placed, code caret synced (150ms debounce)

User drags element
  └─ DesignInteractionService.OnMoveStart/Delta/Completed()
  └─ SnapEngineService.Snap() → snapped point + guide list
  └─ SnapGuideOverlay.Show(guides) — real-time feedback
  └─ On complete: DesignToXamlSyncService.PatchElement()
        └─ Update code editor XAML
        └─ Push SingleDesignUndoEntry

User edits XAML code
  └─ 300ms debounce
  └─ DesignTimeXamlPreprocessor (remove d:* attributes)
  └─ DesignCanvas.XamlSource = preprocessed
        └─ Re-render + re-map elements
        └─ Error card if parse fails
```

---

## Theme Tokens (30 XD_* brushes)

All tokens defined in `Themes/Generic.xaml`, applied via `SetResourceReference()` for live theme switching.

Key tokens: `XD_CanvasBackground`, `XD_SelectionBorderBrush`, `XD_ResizeHandleFillBrush`, `XD_ResizeHandleStrokeBrush`, `XD_SnapGuideBrush`, `XD_RulerBackground`, `XD_RulerTickBrush`, `XD_HistoryEntryBackground`, `XD_HistoryEntrySelectedBackground`, `XD_ErrorCardBackground`.

---

## Key Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.Editor.CodeEditor` | Code pane in split view |
| `WpfHexEditor.Editor.Core` | `IDocumentEditor`, `IEditorToolbarContributor`, `IStatusBarContributor` |
| `WpfHexEditor.SDK` | `IPropertyProvider`, SDK extension points |
| `WpfHexEditor.ColorPicker` | Color picker property editor |
| `WpfHexEditor.Definitions` | Shared constants and enums |

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Composite** | `XamlDesignerSplitHost` wraps code + design panes |
| **Memento** | `IDesignUndoEntry` hierarchy (undo/redo) |
| **Observer** | `SelectedElementChanged`, `OperationCommitted` events |
| **Command** | `AlignmentService` alignment operations |
| **Strategy** | `PropertyEditorRegistry` per-type editors |
| **Registry** | `ToolboxRegistry`, `PropertyEditorRegistry` |
| **Decorator** | `ZoomPanCanvas` wraps `DesignCanvas` |
