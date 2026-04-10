# WpfDocking

A lightweight WPF docking framework inspired by Visual Studio and VS Code.

**License:** GNU AGPL v3.0 | **Target:** net8.0-windows | **Dependencies:** Zero

---

## Features

- VS Code-like chrome (WindowChrome + WindowStyle=None)
- Panel docking: Left / Right / Top / Bottom / Center (tabbed)
- Document host with split-view and tab groups
- Floating windows (undock any panel)
- Auto-hide panels (collapse to edge bar, expand on hover)
- Drag-and-drop with overlay drop targets
- Runtime theme switching (Dark / Light via DynamicResource)
- JSON layout persistence (DockLayoutSerializer)
- Full UI Automation / MSAA accessibility support

## Quick Start

```xml
<Window xmlns:dock="clr-namespace:WpfHexEditor.Shell;assembly=WpfHexEditor.Docking.Wpf">
    <dock:DockControl x:Name="DockHost" />
</Window>
```

```csharp
// Register panel factory and restore layout
DockWorkspace.ContentFactory = new MyContentFactory();
await DockWorkspace.LoadLayoutAsync("layout.json");
```

## Installation

```
dotnet add package WpfDocking
```

## Included Assemblies

| Assembly | Description |
|----------|------------|
| WpfHexEditor.Docking.Wpf | WPF docking chrome, panels, documents |
| WpfHexEditor.Docking.Core | Platform-agnostic layout engine |

## Repository

[GitHub - WpfHexEditorIDE](https://github.com/abbaye/WpfHexEditorIDE)
