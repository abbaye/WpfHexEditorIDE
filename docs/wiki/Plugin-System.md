# Plugin System

The WpfHexEditor Plugin System lets third-party developers extend the IDE with new panels, commands, editor features, and services — packaged as `.whxplugin` files.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Projects](#projects)
- [Plugin Contract](#plugin-contract)
- [IIDEHostContext — Services](#iidehostcontext--services)
- [Manifest Format](#manifest-format)
- [Lifecycle](#lifecycle)
- [Packaging](#packaging)
- [First-Party Plugins](#first-party-plugins)
- [Plugin Manager](#plugin-manager)
- [Hot-Unload](#hot-unload)
- [Permissions](#permissions)
- [Options Integration](#options-integration)
- [See Also](#see-also)

---

## Overview

```
Sources/
  WpfHexEditor.SDK/           — Public contracts (IWpfHexEditorPluginV2, IIDEHostContext, …)
  WpfHexEditor.PluginHost/    — Runtime host (discovery, load, watchdog, PermissionService, …)
  WpfHexEditor.PluginSandbox/ — Out-of-process sandbox (named-pipe IPC)
  Sources/Plugins/            — 7 first-party plugin packages
  Sources/Tools/
    WpfHexEditor.PackagingTool/   — whxpack CLI
    WpfHexEditor.PluginInstaller/ — WPF installer dialog
```

Plugin discovery locations (evaluated in order):
1. `%AppData%\WpfHexEditor\Plugins\<PluginId>\`
2. `<IDE_EXE>\bin\Plugins\<PluginId>\`

---

## Projects

| Project | Role |
|---------|------|
| `WpfHexEditor.SDK` | Public plugin API — interfaces, contracts, event bus |
| `WpfHexEditor.PluginHost` | Runtime host — discovery, load, unload, watchdog, crash handler |
| `WpfHexEditor.PluginSandbox` | Isolated console host (named-pipe IPC stub) |
| `WpfHexEditor.PackagingTool` | `whxpack` CLI — creates `.whxplugin` packages |
| `WpfHexEditor.PluginInstaller` | WPF installer dialog — extract + register |

---

## Plugin Contract

Implement `IWpfHexEditorPluginV2` from `WpfHexEditor.SDK`:

```csharp
public interface IWpfHexEditorPluginV2
{
    string Id      { get; }   // reverse-domain: "com.example.myplugin"
    string Name    { get; }
    string Version { get; }
    string? Description { get; }

    void Init(IIDEHostContext context);
    void Activate();
    void Deactivate();
    void Dispose();
}
```

Optional interface — auto-registers an Options page under **Plugins**:
```csharp
public interface IPluginWithOptions
{
    FrameworkElement CreateOptionsPage();
    string OptionsPageTitle { get; }
}
```

### Minimal Example

```csharp
using WpfHexEditor.SDK;

public class MyPlugin : IWpfHexEditorPluginV2, IPluginWithOptions
{
    private IIDEHostContext? _context;

    public string Id          => "com.example.myplugin";
    public string Name        => "My Plugin";
    public string Version     => "1.0.0";
    public string? Description => "Does something awesome";
    public string OptionsPageTitle => "My Plugin";

    public void Init(IIDEHostContext context)
    {
        _context = context;

        // Register a VS-style dockable panel
        context.UIRegistry.RegisterPanel(
            contentId: "my-panel",
            title:     "My Panel",
            factory:   () => new MyPanelView());

        // Subscribe to events
        context.EventBus.Subscribe<FileOpenedEvent>(OnFileOpened);
    }

    public void Activate()   => _context?.UIRegistry.ShowDockablePanel("my-panel");
    public void Deactivate() => _context?.UIRegistry.HideDockablePanel("my-panel");
    public void Dispose()    { /* cleanup */ }

    public FrameworkElement CreateOptionsPage() => new MyOptionsPage();

    private void OnFileOpened(FileOpenedEvent e)
        => _context?.OutputService.WriteLine($"Opened: {e.FilePath}");
}
```

---

## IIDEHostContext — Services

`IIDEHostContext` is the gateway to all IDE services injected into your plugin at `Init()`:

| Property | Type | Description |
|----------|------|-------------|
| `HexEditorService` | `IHexEditorService` | Read bytes, search, go to offset, get selection |
| `CodeEditorService` | `ICodeEditorService` | Access active code editor content |
| `OutputService` | `IOutputService` | Write to the Output panel |
| `ErrorPanelService` | `IErrorPanelService` | Post diagnostics (Info/Warning/Error) |
| `SolutionExplorerService` | `ISolutionExplorerService` | Open files, query project tree |
| `ParsedFieldService` | `IParsedFieldService` | Read parsed fields from active HexEditor |
| `TerminalService` | `ITerminalService` | Execute commands, write output, read history |
| `UIRegistry` | `IUIRegistry` | Show/Hide/Toggle/Focus dockable panels |
| `EventBus` | `IPluginEventBus` | Subscribe/publish IDE-wide events |
| `FocusContextService` | `IFocusContextService` | Query active editor focus |
| `PermissionService` | `IPermissionService` | Check/request permissions |
| `ThemeService` | `IThemeService` | Query current theme, subscribe to changes |

### Terminal Service Example

```csharp
// Execute a terminal command programmatically
context.TerminalService.Execute("list-files --filter *.bin");

// Write tabular output
context.TerminalService.WriteTable(
    headers: new[] { "Offset", "Value", "Type" },
    rows:    parsedFields.Select(f => new[] { f.Offset.ToString("X"), f.Value, f.TypeName }));

// Export session
context.TerminalService.ExportSession(@"C:\Logs\session.txt");
```

---

## Manifest Format

Each plugin folder must contain a `manifest.json`:

```json
{
  "id":          "com.example.myplugin",
  "name":        "My Plugin",
  "version":     "1.0.0",
  "description": "Does something awesome",
  "author":      "Jane Dev",
  "entryPoint":  "MyPlugin.dll",
  "minHostVersion": "0.2.0",
  "capabilities": ["HexEditor", "Terminal"],
  "permissions":  ["ReadFile", "WriteOutput"]
}
```

When building from a `.csproj`, the PackagingTool auto-generates `manifest.json` from MSBuild properties:

```xml
<PropertyGroup>
  <PluginId>com.example.myplugin</PluginId>
  <PluginVersion>1.0.0</PluginVersion>
  <PluginEntryPoint>MyPlugin.dll</PluginEntryPoint>
  <PluginAuthor>Jane Dev</PluginAuthor>
</PropertyGroup>
```

---

## Lifecycle

```
Discover → Load → Init(context) → Activate
                                     ↓
                  [running — responds to IDE events]
                                     ↓
                  Deactivate → Dispose → Unload (ALC released)
```

- **Init**: called once after assembly is loaded — register panels, commands, subscriptions.
- **Activate**: called when plugin is enabled by user or on startup.
- **Deactivate**: called before disabling; hide panels, detach expensive handlers.
- **Dispose**: called before assembly unload; release all resources.
- **Unload**: the collectible `AssemblyLoadContext` is cleared — all plugin memory freed.

### Crash Handling

`PluginCrashHandler` catches unhandled exceptions from plugin code:
- Calls `HandleCrash(PluginEntry, Exception)` synchronously
- `HandleCrashAsync()` for background crash processing
- Plugin is marked `Faulted`; `PluginEntry.FaultException` stores the exception

---

## Packaging

```bash
# Build the plugin
dotnet build MyPlugin.csproj -c Release

# Package into .whxplugin (ZIP archive with manifest + DLL)
whxpack --input bin/Release/net8.0-windows --output MyPlugin.whxplugin

# Install (silent mode for CI)
WpfHexEditor.PluginInstaller.exe --file MyPlugin.whxplugin --silent
```

`ManifestFinalizer` computes SHA-256 of all assemblies and embeds them in the manifest for integrity verification.

---

## First-Party Plugins

| Plugin | Wraps | Options |
|--------|-------|---------|
| `WpfHexEditor.Plugins.DataInspector` | `DataInspectorPanel` | Display format, endianness, auto-refresh interval |
| `WpfHexEditor.Plugins.StructureOverlay` | `StructureOverlayPanel` | — |
| `WpfHexEditor.Plugins.FileStatistics` | `FileStatisticsPanel` | — |
| `WpfHexEditor.Plugins.PatternAnalysis` | `PatternAnalysisPanel` | — |
| `WpfHexEditor.Plugins.FileComparison` | `FileComparisonPanel` | — |
| `WpfHexEditor.Plugins.ArchiveStructure` | `ArchiveStructurePanel` | — |
| `WpfHexEditor.Plugins.CustomParserTemplate` | `CustomParserTemplatePanel` | — |

---

## Plugin Manager

Open via **Tools → Plugin Manager** or `Ctrl+Shift+P`.

| Feature | Description |
|---------|-------------|
| List | All discovered plugins with status (Loaded/Faulted/Disabled) |
| Enable/Disable | Toggle without IDE restart |
| Uninstall | Removes plugin folder; takes effect on next launch |
| Reload | Hot-unload + hot-load without restart |
| Diagnostics | View crash exception, init duration, memory usage |
| Filter/Search | Filter by name or status |

The `PluginManagerViewModel` wires `SelectionChanged` to the **Plugin Monitoring** panel — selecting a plugin in the manager jumps to its chart.

---

## Hot-Unload

Each plugin is loaded into a **collectible `AssemblyLoadContext`** (`PluginLoadContext`). When a plugin is disabled or reloaded:

1. `Deactivate()` + `Dispose()` called on the plugin instance
2. `PluginEntry.Unload()` clears the `PluginLoadContext` reference
3. GC collects the ALC and all plugin assemblies (no IDE restart needed)

> Note: Hot-unload UI exposure (button in Plugin Manager) is implemented. The collectible ALC infrastructure is fully functional.

---

## Permissions

Plugins declare required permissions in `manifest.json` (`"permissions"` array). At runtime `PermissionService` checks grants:

```csharp
if (!context.PermissionService.IsGranted(PluginId, PluginPermission.ReadFile))
{
    context.OutputService.WriteWarning("ReadFile permission denied.");
    return;
}
```

`PermissionChangedEventArgs` (object-initializer form):
```csharp
new PermissionChangedEventArgs
{
    PluginId   = "com.example.myplugin",
    Permission = PluginPermission.Terminal,
    IsGranted  = true
}
```

---

## Options Integration

If your plugin implements `IPluginWithOptions`, an Options page is automatically registered under **Plugins → Your Plugin Name** in the Options dialog.

```csharp
public FrameworkElement CreateOptionsPage()
{
    return new MyOptionsPage
    {
        DataContext = new MyOptionsViewModel(Settings.Instance)
    };
}
```

`PluginOptionsRegistry.RegisterDynamic(category, pageName, factory)` — called automatically on load.
`UnregisterDynamic(pageId)` — called automatically on disable/unload.

---

## See Also

- **[Terminal Panel](Terminal-Panel)** — `ITerminalService` plugin API
- **[Plugin Monitoring](Plugin-Monitoring)** — CPU/memory charts per plugin
- **[Architecture Overview](Architecture-Overview)** — plugin lifecycle sequence diagram
- **[FAQ](FAQ#ide-application)** — common plugin questions
