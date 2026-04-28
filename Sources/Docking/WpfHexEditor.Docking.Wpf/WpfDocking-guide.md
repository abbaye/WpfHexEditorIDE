# WpfDocking — Documentation

## Table of Contents

1. [Architecture](#architecture)
2. [API Reference](#api-reference)
3. [Integration Guide — Level 1: Basic Setup](#level-1-basic-setup)
4. [Integration Guide — Level 2: Panels & Documents](#level-2-panels--documents)
5. [Integration Guide — Level 3: Layout Persistence](#level-3-layout-persistence)
6. [Integration Guide — Level 4: Advanced Integration](#level-4-advanced-integration)
7. [Settings Reference](#settings-reference)

---

## Architecture

### Assembly structure

```
WpfDocking.nupkg
└── lib/net8.0-windows/
    ├── WpfHexEditor.Docking.Wpf.dll   — DockControl, DockWorkspace, all WPF chrome
    └── WpfHexEditor.Docking.Core.dll  — DockEngine, DockLayoutRoot, DockItem, serialization
```

Zero external NuGet dependencies. Both assemblies are bundled inside the package.

### Type ownership

| Type | Assembly | Purpose |
|---|---|---|
| `DockControl` | Docking.Wpf | Main WPF container — renders the full dock layout |
| `DockWorkspace` | Docking.Wpf | High-level facade — layout, profiles, undo/redo |
| `DockEngine` | Docking.Core | Tree manipulation — dock, float, auto-hide, close |
| `DockLayoutRoot` | Docking.Core | Root layout tree — documents, panels, floating, hidden |
| `DockItem` | Docking.Core | Individual panel or document — state, metadata, binding |
| `DockGroupNode` | Docking.Core | Tabbed group of `DockItem`s |
| `DockSplitNode` | Docking.Core | Horizontal or vertical split between child nodes |
| `DocumentHostNode` | Docking.Core | Central document area — always present, cannot be removed |
| `DockLayoutSerializer` | Docking.Core | JSON serialization for layout persistence |
| `DockCommandStack` | Docking.Core | Undo/redo stack for layout operations |

### Layout tree model

```
DockLayoutRoot
├── RootNode  (DockSplitNode or DockGroupNode)
│   ├── DockSplitNode (Horizontal)
│   │   ├── DockGroupNode  ← tool panel (Left)
│   │   ├── DocumentHostNode  ← main document area (Center)
│   │   └── DockGroupNode  ← tool panel (Right)
│   └── DockGroupNode  ← tool panel (Bottom)
├── FloatingItems  [ DockItem, ... ]
├── AutoHideItems  [ DockItem, ... ]
└── HiddenItems    [ DockItem, ... ]
```

Every layout operation (dock, float, split, close) mutates this tree via `DockEngine`. The WPF visual tree is rebuilt from the model by `DockControl.RebuildVisualTree()`.

### Content factory model

`DockItem` is a pure data model — it holds `ContentId`, title, state, and metadata, but not the WPF content itself. Content is created on demand via a factory delegate:

```
DockItem.ContentId
       │
       ▼
ContentFactory(DockItem) → UIElement (your panel/editor)
       │
       ▼
DockControl renders it inside a DockTabControl or DocumentTabHost
```

This separation allows layouts to be saved and restored without keeping content alive.

### Thread safety

- All layout tree operations and WPF rendering run on the UI thread.
- `DockEngine` is not thread-safe — call only from the UI thread.
- `DockLayoutSerializer` is stateless and thread-safe for read operations.

---

## API Reference

### DockControl

The main WPF container. Place it as the root of your `Window`.

```csharp
// Content factory — called when a DockItem needs to be rendered
DockHost.ContentFactory = item => item.ContentId switch
{
    "Explorer"  => new ExplorerPanel(),
    "Output"    => new OutputPanel(),
    _           => new PlaceholderPanel(item.ContentId)
};

// Async variant (for lazy-loaded content)
DockHost.AsyncContentFactory = async item =>
{
    await Task.Delay(1); // yield to UI
    return new HeavyPanel();
};

// Active panel highlight style
DockHost.PanelHighlightMode = ActivePanelHighlightMode.TopBar;

// Panel corner radius (0 = sharp, 4 = soft, 8 = round)
DockHost.PanelCornerRadius = 4.0;

// Document tab group badges
DockHost.ShowGroupNumberBadge = true;

// Rebuild visual tree after programmatic layout change
DockHost.RebuildVisualTree();

// Animation settings
DockHost.ApplyAnimationSettings(enabled: true, fadeInMs: 120, fadeOutMs: 80, floatingFadeInMs: 150);

// Events
DockHost.TabCloseRequested += item => CloseDocument(item);
```

### DockWorkspace

High-level facade over `DockControl` — the recommended entry point for most scenarios.

```csharp
// Wire up
DockWorkspace.Host = DockHost;

// Layout persistence
string json = DockWorkspace.SaveLayout();
File.WriteAllText("layout.json", json);
DockWorkspace.LoadLayout(File.ReadAllText("layout.json"));

// Named profiles
DockWorkspace.SaveProfile("Debug");
DockWorkspace.LoadProfile("Debug");

// Quick slots (1–4)
DockWorkspace.SaveQuickProfile(1);
DockWorkspace.LoadQuickProfile(1);

// Undo / redo layout operations
DockWorkspace.Undo();
DockWorkspace.Redo();
bool canUndo = DockWorkspace.CanUndo;

// Undoable layout operation
DockWorkspace.ExecuteUndoable("Move Output Panel", () =>
    DockWorkspace.Dock(outputItem, bottomGroup, DockDirection.Center));
```

### DockEngine

Low-level layout tree API. Use `DockWorkspace` for most operations; use `DockEngine` directly when you need transactions or incremental updates.

```csharp
var engine = DockHost.Engine;

// Dock relative to a group
engine.Dock(myItem, targetGroup, DockDirection.Bottom);

// Float
engine.Float(myItem);

// Auto-hide to an edge bar
engine.AutoHide(myItem);

// Hide (keep in memory, remove from view)
engine.Hide(myItem);

// Show a hidden item
engine.Show(myItem, targetGroup, DockDirection.Center);

// Add to main document host
engine.DockAsDocument(myItem);

// Close (remove from layout entirely)
engine.Close(myItem);

// Batch operations — defers NormalizeTree until commit
engine.BeginTransaction();
engine.Hide(panel1);
engine.Hide(panel2);
engine.CommitTransaction();

// Events
engine.LayoutChanged       += () => SaveLayoutDebounced();
engine.ItemDocked          += item => { };
engine.ItemFloated         += item => { };
engine.ItemClosed          += item => { };
engine.ItemAddedToGroup    += (item, group) => { };
engine.ItemRemovedFromGroup += (item, group) => { };
```

### DockItem

The data model for a panel or document.

```csharp
var item = new DockItem
{
    ContentId  = "MyPanel",          // used by ContentFactory + serializer
    Title      = "My Panel",
    CanClose   = true,
    CanFloat   = true,
    IsDocument = false,              // false = tool panel, true = document tab
};

// Runtime state
item.IsDirty  = true;               // shows • in tab title
item.IsPinned = true;               // pin tab (protected from Close All)

// Custom metadata — persisted in JSON layout
item.Metadata["FilePath"] = @"C:\project\main.cs";

// State query
DockItemState state = item.State;   // Docked | Float | AutoHide | Hidden

// Change notification
item.PropertyChanged += (s, e) => { };
```

### DockGroupNode & DockSplitNode

```csharp
// Inspect a group
DockGroupNode group = layout.MainDocumentHost;
DockItem active = group.ActiveItem;
foreach (var item in group.Items) { }

// Inspect the tree
DockSplitNode split = (DockSplitNode)layout.RootNode;
Console.WriteLine(split.Orientation);   // Horizontal or Vertical
foreach (var child in split.Children) { }

// Find items anywhere in the tree
DockItem? found = layout.FindItemByContentId("Explorer");
```

### DockLayoutSerializer

```csharp
// Serialize
string json = DockLayoutSerializer.Serialize(layout);

// Deserialize
DockLayoutRoot restored = DockLayoutSerializer.Deserialize(json);
DockHost.Engine.Layout = restored;
DockHost.RebuildVisualTree();
```

### DockGroupBadge

Numeric badge overlay for tool panel tab headers.

```xml
<!-- In your panel's tab header DataTemplate -->
<dock:DockGroupBadge Value="{Binding UnreadCount}"
                     IsVisible="{Binding HasUnread}" />
```

```csharp
// Or in code, via DockControl
DockHost.ShowGroupNumberBadge = true;
```

---

## Level 1: Basic Setup

Minimum working integration — docking shell in a WPF window.

### 1 — Install

```
dotnet add package WpfDocking
```

### 2 — Add namespace and control

```xml
<Window
    xmlns:dock="clr-namespace:WpfHexEditor.Shell;assembly=WpfHexEditor.Docking.Wpf">

    <dock:DockControl x:Name="DockHost" />
```

### 3 — Register content factory

```csharp
DockHost.ContentFactory = item => item.ContentId switch
{
    "Explorer" => new ExplorerPanel(),
    "Output"   => new OutputPanel(),
    _          => new TextBlock { Text = item.ContentId }
};
```

### 4 — Add initial panels

```csharp
var explorer = new DockItem { ContentId = "Explorer", Title = "Explorer", IsDocument = false };
var output   = new DockItem { ContentId = "Output",   Title = "Output",   IsDocument = false };
var editor   = new DockItem { ContentId = "Editor",   Title = "Editor",   IsDocument = true  };

DockHost.Engine.DockAsDocument(editor);
DockHost.Engine.Dock(explorer, DockHost.Engine.Layout.MainDocumentHost, DockDirection.Left);
DockHost.Engine.Dock(output,   DockHost.Engine.Layout.MainDocumentHost, DockDirection.Bottom);
DockHost.RebuildVisualTree();
```

### 5 — Borderless window (VS Code style)

```xml
<Window WindowStyle="None">
    <WindowChrome.WindowChrome>
        <WindowChrome ResizeBorderThickness="4" CaptionHeight="32" />
    </WindowChrome.WindowChrome>
    <dock:DockControl x:Name="DockHost" />
</Window>
```

---

## Level 2: Panels & Documents

### Tool panels vs documents

```csharp
// Tool panel — docks to an edge, can auto-hide or float
var panel = new DockItem
{
    ContentId  = "Properties",
    Title      = "Properties",
    IsDocument = false,
    CanClose   = false,   // always present
    CanFloat   = true,
};

// Document — lives in the central document host, has a tab
var doc = new DockItem
{
    ContentId  = "File_main.cs",
    Title      = "main.cs",
    IsDocument = true,
    IsDirty    = false,
};
```

### Float, auto-hide, hide

```csharp
var engine = DockHost.Engine;

// Float a panel to a standalone window
engine.Float(panel);

// Collapse to the edge auto-hide bar
engine.AutoHide(panel);

// Hide completely (retains state, no UI)
engine.Hide(panel);

// Restore
engine.Show(panel, null, DockDirection.Left);
```

### Pin and protect documents

```csharp
doc.IsPinned = true;   // tab stays on the left, excluded from "Close All"
doc.IsSticky = true;   // tab never moved to the overflow dropdown
```

### Dirty indicator

```csharp
doc.IsDirty = true;    // shows • before title in tab
// Clear on save
doc.IsDirty = false;
```

### Panel locking

```csharp
panel.Owner!.LockMode = DockLockMode.PreventClosing | DockLockMode.PreventUndocking;
```

### Active panel highlight

```csharp
DockHost.PanelHighlightMode = ActivePanelHighlightMode.TopBar;    // accent bar at top
DockHost.PanelHighlightMode = ActivePanelHighlightMode.FullBorder; // border around panel
DockHost.PanelHighlightMode = ActivePanelHighlightMode.Glow;       // glow effect
DockHost.PanelHighlightMode = ActivePanelHighlightMode.None;       // no highlight
```

### Corner radius

```csharp
DockHost.PanelCornerRadius = 0.0;   // Sharp
DockHost.PanelCornerRadius = 4.0;   // Soft (default)
DockHost.PanelCornerRadius = 8.0;   // Round
```

### Tab close requests

```csharp
DockHost.TabCloseRequested += item =>
{
    if (item.IsDirty)
    {
        var result = MessageBox.Show("Save before closing?", item.Title, MessageBoxButton.YesNoCancel);
        if (result == MessageBoxResult.Cancel) return;
        if (result == MessageBoxResult.Yes) SaveDocument(item);
    }
    DockHost.Engine.Close(item);
};
```

---

## Level 3: Layout Persistence

### Save and restore layout

```csharp
// On window closing
string json = DockLayoutSerializer.Serialize(DockHost.Engine.Layout);
File.WriteAllText("layout.json", json);

// On startup — restore before showing window
if (File.Exists("layout.json"))
{
    var layout = DockLayoutSerializer.Deserialize(File.ReadAllText("layout.json"));
    DockHost.Engine.Layout = layout;
    DockHost.RebuildVisualTree();
}
```

### Named profiles

```csharp
var store = new DockLayoutProfileStore();

// Save current layout as "Coding" profile
store.SaveProfile("Coding", DockHost.Engine.Layout);

// Load and apply
var layout = store.LoadProfile("Coding");
DockHost.Engine.Layout = layout;
DockHost.RebuildVisualTree();
```

### Via DockWorkspace (recommended)

```csharp
DockWorkspace.Host = DockHost;

// Save
await File.WriteAllTextAsync("layout.json", DockWorkspace.SaveLayout());

// Restore
DockWorkspace.LoadLayout(await File.ReadAllTextAsync("layout.json"));

// Quick slots
DockWorkspace.SaveQuickProfile(1);   // Ctrl+Shift+1
DockWorkspace.LoadQuickProfile(1);   // Ctrl+1
```

### Window bounds persistence

```csharp
// The layout serializer persists window state automatically:
// DockLayoutRoot.WindowLeft, WindowTop, WindowWidth, WindowHeight, WindowState

// On restore:
if (layout.WindowLeft.HasValue)
{
    Left   = layout.WindowLeft.Value;
    Top    = layout.WindowTop!.Value;
    Width  = layout.WindowWidth!.Value;
    Height = layout.WindowHeight!.Value;
}
```

### ContentId contract

`ContentId` is the key that links a serialized `DockItem` to your content factory. It must be stable across sessions:

```csharp
// Good — stable identifier
new DockItem { ContentId = "Explorer" }
new DockItem { ContentId = $"File:{filePath}" }

// Bad — changes every session
new DockItem { ContentId = Guid.NewGuid().ToString() }
```

---

## Level 4: Advanced Integration

### Undo / redo for layout operations

```csharp
DockWorkspace.Host = DockHost;

// All operations via ExecuteUndoable are reversible
DockWorkspace.ExecuteUndoable("Split Output", () =>
    DockHost.Engine.Dock(outputItem, targetGroup, DockDirection.Bottom));

// Wire toolbar buttons
undoButton.IsEnabled = DockWorkspace.CanUndo;
DockWorkspace.CommandStack.StackChanged += () =>
{
    undoButton.IsEnabled = DockWorkspace.CanUndo;
    redoButton.IsEnabled = DockWorkspace.CanRedo;
};

DockWorkspace.Undo();
DockWorkspace.Redo();
```

### React to panel activation (multi-panel sync)

```csharp
// TrackActivation fires when the active tab changes in any group,
// including when focus moves between tab groups.
DockHost.Engine.ItemAddedToGroup += (item, group) =>
{
    if (item == group.ActiveItem)
        OnDocumentActivated(item);
};

// Or listen to your content panels directly
// (the content factory gives you the reference)
```

### Batch layout operations

```csharp
// Defers visual tree rebuild until commit — avoids intermediate states
DockHost.Engine.BeginTransaction();
DockHost.Engine.Hide(debugPanel);
DockHost.Engine.Hide(watchPanel);
DockHost.Engine.DockAsDocument(releaseDoc);
DockHost.Engine.CommitTransaction();
DockHost.RebuildVisualTree();
```

### Custom metadata on DockItem

```csharp
// Metadata is persisted in the JSON layout
item.Metadata["FilePath"]     = @"C:\project\main.cs";
item.Metadata["Language"]     = "csharp";
item.Metadata["CursorOffset"] = "1024";

// On restore — your ContentFactory reads it back
DockHost.ContentFactory = item =>
{
    if (item.Metadata.TryGetValue("FilePath", out var path))
        return new CodeEditor(path, cursorOffset: int.Parse(item.Metadata["CursorOffset"]));
    return new PlaceholderPanel();
};
```

### Document tab colorization

```csharp
var settings = DockHost.TabBarSettings;
settings.ColorMode = DocumentTabColorMode.FileExtension;
// Each file extension gets a deterministic accent color on its tab indicator

settings.ColorMode = DocumentTabColorMode.Regex;
settings.RegexRules.Add(new TabColorRule { Pattern = @"\.cs$",   Color = Colors.LightBlue });
settings.RegexRules.Add(new TabColorRule { Pattern = @"\.xaml$", Color = Colors.LightGreen });
```

### Auto-hide timing

```csharp
DockHost.AutoHideSettings.HoverOpenDelayMs  = 300;   // delay before expanding
DockHost.AutoHideSettings.HoverCloseDelayMs = 600;   // delay before collapsing
DockHost.AutoHideSettings.SlideAnimationMs  = 150;   // expand/collapse animation
```

### Theme switching

All docking chrome resolves through `DynamicResource` brushes — override them to apply a custom theme:

```xml
<Application.Resources>
    <SolidColorBrush x:Key="DockBackgroundBrush"    Color="#1E1E1E" />
    <SolidColorBrush x:Key="DockBorderBrush"        Color="#3F3F46" />
    <SolidColorBrush x:Key="DockTabActiveBrush"     Color="#007ACC" />
    <SolidColorBrush x:Key="DockTabInactiveBrush"   Color="#2D2D30" />
    <SolidColorBrush x:Key="DockTabForeground"      Color="#F1F1F1" />
</Application.Resources>
```

Switch at runtime:

```csharp
Application.Current.Resources["DockBackgroundBrush"] = new SolidColorBrush(Colors.White);
```

---

## Settings Reference

### DockControl properties

| Property | Type | Default | Description |
|---|---|---|---|
| `PanelHighlightMode` | `ActivePanelHighlightMode` | `TopBar` | Active panel border style |
| `PanelCornerRadius` | `double` | `4.0` | Panel overlay corner radius (px) |
| `ShowGroupNumberBadge` | `bool` | `false` | Badge with group index on document tab bars |
| `TabBarSettings` | `DocumentTabBarSettings` | — | Document tab appearance and color rules |
| `TabPreviewSettings` | `TabPreviewSettings` | — | Hover-preview popup timing |
| `AutoHideSettings` | `AutoHideSettings` | — | Auto-hide expand/collapse delays |
| `ContentFactory` | `Func<DockItem, object>` | — | Synchronous content factory |
| `AsyncContentFactory` | `Func<DockItem, Task<object>>` | — | Async content factory |

### DocumentTabBarSettings

| Property | Type | Default | Description |
|---|---|---|---|
| `TabPlacement` | `DocumentTabPlacement` | `Top` | Tab bar position: Top / Left / Right / Bottom |
| `ColorMode` | `DocumentTabColorMode` | `None` | Tab accent color: None / FileExtension / Regex |
| `MultiRowTabs` | `bool` | `false` | Wrap tabs to multiple rows when overflow |
| `RegexRules` | `List<TabColorRule>` | `[]` | Color rules for `Regex` color mode |

### AutoHideSettings

| Property | Type | Default | Description |
|---|---|---|---|
| `HoverOpenDelayMs` | `int` | `300` | Delay before auto-hide panel expands |
| `HoverCloseDelayMs` | `int` | `600` | Delay before auto-hide panel collapses |
| `SlideAnimationMs` | `int` | `150` | Slide animation duration |

### ActivePanelHighlightMode enum

| Value | Description |
|---|---|
| `None` | No active panel indicator |
| `TopBar` | Thin accent bar along the top edge of the active panel |
| `FullBorder` | Colored border around the entire active panel |
| `Glow` | Drop shadow / glow effect on the active panel |

### DockLockMode flags

| Value | Description |
|---|---|
| `None` | No restrictions |
| `PreventSplitting` | Panel cannot be split by drag-drop |
| `PreventUndocking` | Panel cannot be floated or moved |
| `PreventClosing` | Close button hidden |
| `Full` | All of the above |
