# WpfHexEditor Plugin Development Guide

Build plugins for the WpfHexEditor IDE using the public SDK.

## Quick Start

### 1. Create the project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\WpfHexEditor.SDK\WpfHexEditor.SDK.csproj" />
  </ItemGroup>
</Project>
```

### 2. Implement IWpfHexEditorPlugin

```csharp
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

public sealed class MyPlugin : IWpfHexEditorPlugin
{
    public string  Id           => "MyCompany.MyPlugin";
    public string  Name         => "My Plugin";
    public Version Version      => new(1, 0, 0);
    public PluginCapabilities Capabilities => PluginCapabilities.AccessHexEditor;

    private IIDEHostContext? _context;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct)
    {
        _context = context;
        // Register UI, subscribe to events, etc.
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        _context = null;
        return Task.CompletedTask;
    }
}
```

### 3. Create manifest.json

```json
{
  "id": "MyCompany.MyPlugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "entryPoint": "MyNamespace.MyPlugin",
  "assembly": { "file": "MyPlugin.dll" },
  "sdkVersion": "2.0.0",
  "minSDKVersion": "2.0.0",
  "isolationMode": "Auto",
  "loadPriority": 100
}
```

### 4. Deploy

Copy the DLL + manifest.json to the `Plugins/MyPlugin/` folder next to the IDE executable.

---

## Core Concepts

### IIDEHostContext

Every plugin receives an `IIDEHostContext` during initialization. This is the entry point
to all IDE services:

| Service | Property | Permission Required |
|---------|----------|-------------------|
| Hex editor state | `context.HexEditor` | `AccessHexEditor` |
| Code editor state | `context.CodeEditor` | `AccessCodeEditor` |
| Output panel | `context.Output` | `WriteOutput` |
| Error panel | `context.ErrorPanel` | `WriteErrorPanel` |
| Terminal panel | `context.Terminal` | `WriteTerminal` |
| Solution explorer | `context.SolutionExplorer` | None |
| UI registration | `context.UIRegistry` | None |
| Theme info | `context.Theme` | None |
| Event bus | `context.EventBus` | None |
| IDE events | `context.IDEEvents` | None |
| Focus tracking | `context.FocusContext` | None |
| Debugger | `context.Debugger` | None (nullable) |
| Diff service | `context.DiffService` | None (nullable) |

### Permissions (PluginCapabilities)

Declare required permissions in the plugin class and manifest:

```csharp
public PluginCapabilities Capabilities =>
    PluginCapabilities.AccessHexEditor |
    PluginCapabilities.WriteOutput;
```

The host grants permissions at load time. Sandboxed plugins that access ungated
services without permission will receive `UnauthorizedAccessException`.

---

## Registering UI Elements

### Panels

```csharp
var panel = new MyPanelControl();
context.UIRegistry.RegisterPanel(
    new PanelDescriptor
    {
        ContentId = "MyPlugin.Panel",
        Title     = "My Panel",
        DockSide  = "Bottom",
        PreferredHeight = 200,
    },
    panel);
```

### Menu Items

```csharp
context.UIRegistry.RegisterMenuItem(
    new MenuItemDescriptor
    {
        ContentId   = "MyPlugin.Menu.DoSomething",
        Header      = "Do Something",
        ParentPath  = "_View",
        Group       = "Analysis",
        GestureText = "Ctrl+Shift+D",
        Command     = new RelayCommand(() => DoSomething()),
    });
```

### Toolbar Items

```csharp
context.UIRegistry.RegisterToolbarItem(
    new ToolbarItemDescriptor
    {
        ContentId = "MyPlugin.Toolbar.Run",
        Header    = "Run Analysis",
        IconGlyph = "\u25B6",
        Command   = new RelayCommand(() => RunAnalysis()),
    });
```

### Status Bar Items

```csharp
context.UIRegistry.RegisterStatusBarItem(
    new StatusBarItemDescriptor
    {
        ContentId = "MyPlugin.Status",
        Priority  = 100,
    },
    new TextBlock { Text = "Ready" });
```

All registered UI elements are automatically removed when the plugin is unloaded.

---

## Event Handling

### HexEditor Events

```csharp
context.HexEditor.SelectionChanged += (s, e) =>
{
    var offset = context.HexEditor.CurrentOffset;
    var bytes  = context.HexEditor.GetSelectedBytes();
    // Process selection
};

context.HexEditor.FileOpened += (s, e) =>
{
    var path = context.HexEditor.CurrentFilePath;
    // Handle new file
};
```

### IDE Events (typed event bus)

```csharp
context.IDEEvents.Subscribe<FileOpenedEvent>(e =>
{
    var filePath = e.FilePath;
    // React to file open
});
```

### Plugin-to-Plugin Events

```csharp
// Subscribe
context.EventBus.Subscribe<MyCustomEvent>(e => HandleEvent(e));

// Publish
context.EventBus.Publish(new MyCustomEvent { Data = "hello" });
```

---

## Options Pages

Implement `IPluginWithOptions` to add a settings page:

```csharp
public sealed class MyPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string DisplayName => "My Plugin";
    public string Icon => "\u2699";
    public bool HasOptions => true;

    public Type? GetOptionsPageType() => typeof(MyOptionsPage);
    // ... rest of IWpfHexEditorPlugin
}
```

---

## Extension Points

Contribute custom capabilities via extension points:

```csharp
// Binary parser extension
public sealed class MyParser : IBinaryParserExtension
{
    public string Name => "My Binary Parser";
    public bool CanParse(string filePath) => true;
    public T? Parse<T>(byte[] data) where T : class => /* ... */;
}
```

Available extension points: `IBinaryParserExtension`, `IDecompilerExtension`,
`IFileAnalyzerExtension`, `IHexViewOverlayExtension`, `IQuickInfoProvider`.

---

## Hot Reload (v2)

Implement `IWpfHexEditorPluginV2` for in-place plugin updates:

```csharp
public sealed class MyPlugin : IWpfHexEditorPluginV2
{
    public bool SupportsHotReload => true;

    public async Task ReloadAsync(CancellationToken ct)
    {
        // Refresh cached data, re-read config, etc.
    }
}
```

---

## Isolation Modes

| Mode | Description |
|------|-------------|
| `Auto` | IDE decides (in-process for trusted, sandbox for unknown) |
| `InProcess` | Runs in the IDE process (full access, no isolation) |
| `Sandbox` | Runs in a separate process with IPC (restricted access) |

Set in `manifest.json`:
```json
"isolationMode": "Sandbox"
```

Sandboxed plugins communicate via IPC. UI elements are embedded via HWND reparenting.

---

## Best Practices

1. **Always unsubscribe** from events in `ShutdownAsync`
2. **Check `IsPanelVisible`** before expensive computations
3. **Use CancellationToken** for long-running operations
4. **Declare minimal permissions** — only request what you need
5. **Handle null services** — optional services (Debugger, DiffService, etc.) may be null
6. **Use DynamicResource** for theme colors, not hardcoded brushes
7. **Test with both Light and Dark themes**

---

## SDK Version Compatibility

See [CHANGELOG.md](CHANGELOG.md) for stable interfaces and [SDK_MIGRATION.md](SDK_MIGRATION.md)
for upgrade guides between major versions.

Current stable version: **SDK 2.0.0** (SemVer frozen for 2.x line).
