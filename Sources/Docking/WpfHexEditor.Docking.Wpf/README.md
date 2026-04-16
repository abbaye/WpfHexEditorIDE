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

Both bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Docking.Wpf | WPF chrome, panels, documents, drag-drop |
| WpfHexEditor.Docking.Core | Platform-agnostic layout engine (no WPF dependency) |

---

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

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
