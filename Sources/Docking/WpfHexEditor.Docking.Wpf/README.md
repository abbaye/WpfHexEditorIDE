# WpfDocking

A lightweight WPF docking framework inspired by Visual Studio and VS Code.  
Drop it into any WPF window — no IDE, no plugin host, zero external dependencies.

```
dotnet add package WpfDocking
```

---

## Quick Start

### 1 — Add the namespace

```xml
<Window
    xmlns:dock="clr-namespace:WpfHexEditor.Shell;assembly=WpfHexEditor.Docking.Wpf">
```

### 2 — Place the dock host

```xml
<dock:DockControl x:Name="DockHost" />
```

### 3 — Register a content factory and load a layout

```csharp
using WpfHexEditor.Shell;

DockWorkspace.ContentFactory = new MyContentFactory();
await DockWorkspace.LoadLayoutAsync("layout.json");
```

### 4 — Add panels and documents programmatically

```csharp
// Add a tool panel (left side)
DockHost.AddPanel(new MyToolPanel(), DockSide.Left);

// Add a document tab
DockHost.AddDocument(new MyDocument());

// Save layout
await DockWorkspace.SaveLayoutAsync("layout.json");
```

---

## Features

### Layout
- Panel docking: Left / Right / Top / Bottom / Center (tabbed)
- Document host with tab groups and split view
- Floating windows — undock any panel to a standalone window
- Auto-hide panels — collapse to edge bar, expand on hover
- Rounded corners with 3-mode dropdown (Sharp / Soft / Round) and live refresh
- JSON layout persistence (`DockLayoutSerializer`)

### Drag & Drop
- Drag-and-drop with VS-style overlay drop targets
- VS-like overlay gap and placement-aware tab styles
- `StaysOpen=true` on hover preview popup — Win32 mouse-capture no longer suppresses WPF `MouseLeave`

### Theming
- Runtime theme switching (Dark / Light via `DynamicResource`)
- Light and Dark theme `ContextMenu` — drop shadow, MDL2 icons, accent band
- ScrollBar theming consistent across all panels
- `ClipToBounds` fix for docking panes inside custom layouts

### Controls
- `DockGroupBadge` — numeric badge overlay on panel tab headers
- `DockControl` — main container
- `DockWorkspace` — layout/session manager

### Accessibility
- Full UI Automation / MSAA support on all docking elements

---

## Standalone Setup

No additional resource dictionary is required. The docking framework is self-contained.

For custom VS Code-style chrome (borderless window):

```xml
<Window WindowStyle="None">
    <WindowChrome.WindowChrome>
        <WindowChrome ResizeBorderThickness="4" CaptionHeight="32" />
    </WindowChrome.WindowChrome>
    <dock:DockControl x:Name="DockHost" />
</Window>
```

---

## Included Assemblies

All bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Docking.Wpf | WPF chrome, panels, documents, tab groups, drag-drop |
| WpfHexEditor.Docking.Core | Platform-agnostic layout engine (no WPF dependency) |
| WpfHexEditor.Core.Localization | 17-language satellite assemblies (ar-SA, de-DE, es, fr, it, ja, ko, nl, pl, pt-BR, pt-PT, ru, sv, tr, zh-CN, hi-IN, es-419) |

**Localizations**: ar-SA, de-DE, es-419, es-ES, fr-CA, fr-FR, hi-IN, it-IT, ja-JP, ko-KR, nl-NL, pl-PL, pt-BR, pt-PT, ru-RU, sv-SE, tr-TR, zh-CN

---

## What's New in 0.9.7.0

- **New**: Horizontal reorder for docked tool-panel tabs — drag tabs left/right within the same edge to reorder panels without floating.
- **Perf**: Tab-switch triple-fire eliminated — redundant layout passes on tab activation reduced from 3 to 1; measurable improvement on complex layouts with many panels.
- **Fix**: Toolbar `StaticResource` labels — all Docking toolbar buttons now correctly resolve localized labels via `StaticResource`; pre-register output queue prevents race on first display.
- **Feat**: Full Phase 5+6 localization — all Docking UI strings (panel headers, context menus, options pages) translated into 17 languages and wired to the language selector.

## What's New in 0.9.6.0

- **New**: Tab groups — split the document area horizontally or vertically (`Ctrl+Alt+→` / `Ctrl+Alt+↓`); move tabs between groups; close groups. Full `ITabGroupService` SDK contract.
- **New**: 16 `TG_*` theme tokens (`TG_ActiveTabBrush`, `TG_InactiveTabBrush`, `TG_SplitterBrush`, `TG_BadgeBrush`…) across all built-in themes.
- **New**: Tab group badges — document count badge on group headers.
- **New**: Drag visual — `IsDocumentDrag` flag enables distinct drag-between-groups visual feedback.
- **New**: `TabGroupsOptionsPage` — tab group behavior settings.
- **Fix**: Tab drag semantics — document tabs and docked tabs share the same mouse-move logic (X=reorder, Y>40=float); no discriminator per tab type.
- **Fix**: Docking overlay — VS-like border with active-tab gap; placement-aware tab styles for document host match Visual Studio drop-target feedback.
- **Fix**: Satellite assemblies now correctly bundled — `WpfHexEditor.Core.Localization` marked `PrivateAssets=all`; all 17 language `.resources.dll` files are included in the NuGet package.

## What's New in 0.9.5.2

- **New**: `DockGroupBadge` control — numeric badge overlay on panel tab headers.
- **New**: Rounded corners — 3-mode dropdown (Sharp / Soft / Round) with live refresh.
- **Fix**: `StaysOpen=true` on hover preview popup — Win32 mouse-capture no longer suppresses WPF `MouseLeave` events, fixing auto-hide panel flicker.
- **Fix**: `ClipToBounds` fix for docking panes inside custom layouts.
- **Fix**: ScrollBar theming consistent across all docked panels.
- **New**: Light theme `ContextMenu` — drop shadow, accent band, MDL2 icons.
- **New**: Empty editor tab placeholders — panels can be opened before content is loaded.

## What's New in 0.9.5.1

- VS-like overlay gap and placement-aware tab styles for document host.
- Hover preview popup stability improvements.
- Initial NuGet release.

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- **Full documentation**: [WpfDocking-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Docking/WpfHexEditor.Docking.Wpf/WpfDocking-guide.md) — Architecture, API reference, integration guides (Level 1–4), layout persistence, and settings reference.
- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
