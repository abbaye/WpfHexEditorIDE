# WpfHexEditor.Docking.Wpf

> 100% in-house VS-style docking engine — float, dock, auto-hide, colored tabs, 8 themes. Zero third-party dependency.

[![.NET](https://img.shields.io/badge/.NET-net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](../../LICENSE)

---

## Architecture

```mermaid
graph TB
    subgraph DOCK["WpfHexEditor.Docking.Wpf"]
        DH["DockHost (UserControl)\nEntrypoint — IDockHost"]
        DW["DockWorkspace\nCenter document area"]
        DC["DockControl\nVisual tree builder"]
        DTC["DockTabControl\nTabbed panel header + strip"]
        DTH["DocumentTabHost\nDocument area tabs"]
        DSP["DockSplitPanel\nGridSplitter-based layout"]

        FW["FloatingWindow\nFloat panels in OS window"]
        DDM["DockDragManager\nDrag-and-drop routing"]
        DOW["DockOverlayWindow\nDrop-target overlays (4+center)"]
        DEOW["DockEdgeOverlayWindow\nScreen-edge drop zones"]

        AHB["AutoHideBar\nSlide-out panel per side (L/R/T/B)"]
        NAV["NavigatorWindow\nCtrl+Tab document switcher"]
    end

    subgraph CORE["WpfHexEditor.Docking.Core (model)"]
        DI["DockItem\n(INotifyPropertyChanged)\nTitle, ContentId, State, IsPinned…"]
        DGN["DockGroupNode\nCollection of DockItems (panel tabs)"]
        DHN["DocumentHostNode\nDocument area group"]
        DSN["DockSplitNode\nSplit container"]
    end

    subgraph THEMES["Themes (8)"]
        T1["Dark"]
        T2["Light"]
        T3["VS2022Dark"]
        T4["DarkGlass"]
        T5["Minimal"]
        T6["Office"]
        T7["Cyberpunk"]
        T8["VisualStudio"]
    end

    DH --> DW --> DC
    DC --> DTC & DTH & DSP
    DDM --> FW
    DDM --> DOW & DEOW
    DC --> AHB
    DC --> NAV
    DTC --> DI
    DGN --> DI
    DHN --> DI
    DSN --> DGN & DHN
    DH --> THEMES

    style DH fill:#e3f2fd,stroke:#1976d2,stroke-width:3px
    style DI fill:#fff9c4,stroke:#f57c00,stroke-width:2px
    style THEMES fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
```

---

## Project Structure

```
WpfHexEditor.Docking.Wpf/
├── DockHost.cs / IDockHost.cs       ← Public API entrypoint
├── DockWorkspace.cs                 ← Document area container
├── DockControl.cs                   ← Visual tree builder (panels ↔ nodes)
├── DockTabControl.cs                ← Tab strip + colored tabs + pin/close
├── DocumentTabHost.cs               ← Document area tabs
├── DockSplitPanel.cs                ← GridSplitter-based split layout
│
├── FloatingWindow.cs                ← Float a panel in a separate OS window
├── DockDragManager.cs               ← Drag-and-drop state machine
├── DockOverlayWindow.cs             ← Drop-target overlay (5 compass zones)
├── DockEdgeOverlayWindow.cs         ← Screen-edge drop zones
│
├── AutoHideBar.cs                   ← Slide-out panels (L/R/T/B)
├── NavigatorWindow.cs               ← Ctrl+Tab switcher (VS-style)
│
├── Attached/                        ← Attached properties (DockProperties)
├── Automation/                      ← UI Automation support
├── Commands/                        ← Routed commands
├── Controls/                        ← Inner controls (tab headers, close btn)
├── Dialogs/                         ← Rename dialog, etc.
├── Helpers/
│   ├── DockTabEventWirer.cs         ← Wires tab events (close, float, etc.)
│   └── ...
├── Services/
│   ├── FloatingWindowManager.cs     ← Manages all floating windows lifecycle
│   ├── LayoutSerializer.cs          ← Save/restore layout (JSON)
│   └── DockLayoutService.cs
│
└── Themes/
    ├── Dark/                        ← Colors.xaml + Theme.xaml
    ├── Light/
    ├── VS2022Dark/
    ├── DarkGlass/
    ├── Minimal/
    ├── Office/
    ├── Cyberpunk/
    ├── VisualStudio/
    └── PanelCommon.xaml             ← Shared panel toolbar styles
```

---

## Key Concepts

### DockItem (model)

```csharp
var item = new DockItem
{
    Title     = "My Panel",
    ContentId = "panel-my",   // unique — used for layout restore
    CanClose  = true,
    CanFloat  = true,
    IsDocument = false,       // false = tool panel, true = document
    IsPinned  = false,        // pinned tabs moved left, protected from Close All
};
// Title implements INotifyPropertyChanged → tab header updates live
item.Title = "My Panel *";    // dirty flag shown immediately
```

### DockItem states

```mermaid
stateDiagram-v2
    [*] --> Docked : AddPanel()
    Docked --> Floating : FloatGroup() / drag out
    Docked --> AutoHide : ToggleAutoHide()
    Floating --> Docked : drop on overlay zone
    AutoHide --> Docked : ReDock()
    Docked --> Hidden : Close()
    Floating --> Hidden : Close()
    Hidden --> Docked : ShowOrCreatePanel()
```

---

## Usage

### Setup (XAML)

```xml
<Window xmlns:dock="clr-namespace:WpfHexEditor.Docking.Wpf;assembly=WpfHexEditor.Docking.Wpf">
    <dock:DockHost x:Name="DockHost"
                   Theme="VS2022Dark"
                   ActiveItemChanged="OnActiveItemChanged" />
</Window>
```

### Add a panel (code-behind)

```csharp
var item    = new DockItem { Title = "Output", ContentId = "panel-output" };
var content = new OutputPanel();

DockHost.AddPanel(content, item, DockSide.Bottom);
```

### Open a document tab

```csharp
var item    = new DockItem { Title = "file.bin", ContentId = "doc-file.bin",
                             IsDocument = true };
var editor  = new HexEditor { FileName = "file.bin" };

DockHost.OpenDocument(editor, item);
DockHost.ActivateItem(item);
```

### Float a panel

```csharp
DockHost.FloatGroup(item);          // float to last known position
// or drag the tab header out of the dock
```

### Auto-hide

```csharp
DockHost.ToggleAutoHide(item);      // slides panel to side bar
```

### Save / restore layout

```csharp
// Save
string json = DockHost.SaveLayout();
File.WriteAllText("layout.json", json);

// Restore
string json = File.ReadAllText("layout.json");
DockHost.RestoreLayout(json, contentFactory);
```

---

## Themes

Switch themes at runtime:

```csharp
DockHost.Theme = "DarkGlass";       // instant, no restart
```

| Theme | Description |
|-------|-------------|
| `Dark` | Dark neutral |
| `Light` | Light neutral |
| `VS2022Dark` | Visual Studio 2022 dark (default) |
| `DarkGlass` | Translucent dark with glass effect |
| `Minimal` | Flat minimal style |
| `Office` | Microsoft Office ribbon style |
| `Cyberpunk` | High-contrast neon purple |
| `VisualStudio` | VS 2019 light |

Each theme provides: `DockWindowBackgroundBrush`, `DockBorderBrush`, `DockMenuForegroundBrush`, `Panel_*` toolbar keys, `ERR_*` Error panel keys, `SE_*` Solution Explorer keys, `PP_*` Properties panel keys.

---

## Tab Features

| Feature | Description |
|---------|-------------|
| **Colored tabs** | Per-document tab color (set via `DockItem.TabColor`) |
| **Pin** | Pinned tabs move to the left end, protected from "Close All" |
| **Close** | × button on tab (respects `DockItem.CanClose`) |
| **Dirty indicator** | `DockItem.Title = "file *"` → tab updates live via INPC |
| **Context menu** | Float, Close, Close Others, Pin, Split |
| **Ctrl+Tab** | `NavigatorWindow` — VS-style document switcher |

---

## Dependencies

| Project | Why |
|---------|-----|
| `WpfHexEditor.Docking.Core` | Model — `DockItem`, `DockGroupNode`, `DocumentHostNode` |

---

## License

GNU Affero General Public License v3.0 — Copyright 2026 Derek Tremblay. See [LICENSE](../../LICENSE).
