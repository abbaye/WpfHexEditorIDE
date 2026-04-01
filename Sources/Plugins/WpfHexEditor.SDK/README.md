# WpfHexEditor.SDK

Public plugin API for the WpfHexEditor IDE — all contracts, models, and infrastructure types that a plugin author needs, with no other project reference required.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · WPF · **Version 2.0.0** (SemVer frozen)

### Versioning

This SDK follows [Semantic Versioning](https://semver.org/). All interfaces listed as
**Stable** in [`CHANGELOG.md`](CHANGELOG.md) are frozen within the 2.x line — no breaking
changes until SDK 3.0. Interfaces marked `[Obsolete]` are preview stubs.

See [`SDK_MIGRATION.md`](SDK_MIGRATION.md) for upgrade guides between major versions.

---

## Architecture / Modules

- **`Contracts/IWpfHexEditorPlugin`** — primary plugin entry-point interface (`InitializeAsync` / `ShutdownAsync`, `Id`, `Name`, `Version`, `Capabilities`).
- **`Contracts/IWpfHexEditorPluginV2`** — optional hot-reload extension (`SupportsHotReload`, `ReloadAsync`).
- **`Contracts/IIDEHostContext`** — complete IDE service locator injected into every plugin at initialization; exposes 11 service contracts listed below.
- **`Contracts/Services/`** — individual service contracts:
  - `IHexEditorService` — binary document access, selection, bytes
  - `ICodeEditorService` — text document access, cursor
  - `ISolutionExplorerService` — workspace navigation
  - `IOutputService` — IDE Output panel
  - `IParsedFieldService` — structured binary field data
  - `IErrorPanelService` — IDE Error panel
  - `ITerminalService` — terminal output, session management
  - `IAssemblyAnalysisEngine` — PE analysis (consumed by AssemblyExplorer plugin)
  - `IFocusContextService` — active document / panel tracking
  - `IPluginEventBus` — typed publish/subscribe for cross-plugin communication
  - `IThemeService` — current theme resources and change notifications
  - `IUIRegistry` — panel, menu, toolbar, and status-bar contribution (auto-removed on unload)
  - `IPermissionService` — runtime permission checks and revocation
- **`Models/`** — `PluginManifest`, `PluginCapabilities`, `PluginPermission`, `PluginIsolationMode`, `PluginState`, `MarketplaceListing`
- **`Events/`** — `OpenAssemblyInExplorerEvent`, `TemplateApplyRequestedEvent`
- **`UI/ToolbarOverflowManager`** — shared overflow logic for dockable panel toolbars
- **`Commands/RelayCommand`** — standard `ICommand` helper
- **`Build/`** — MSBuild targets distributed with the SDK

The SDK re-exports `WpfHexEditor.Core`, `WpfHexEditor.HexEditor`, and `WpfHexEditor.Panels.FileOps` as transitive references (`PrivateAssets=none`), so plugins that reference only `WpfHexEditor.SDK` receive all required types at compile time and runtime.

---

## Usage

```csharp
// plugin entry point — implement IWpfHexEditorPlugin
public sealed class MyPlugin : IWpfHexEditorPlugin
{
    public string  Id           => "MyCompany.MyPlugin";
    public string  Name         => "My Plugin";
    public Version Version      => new(1, 0, 0);
    public PluginCapabilities Capabilities => PluginCapabilities.None;

    public async Task InitializeAsync(IIDEHostContext context, CancellationToken ct)
    {
        context.UIRegistry.AddStatusBarItem(new StatusBarItem { Text = "Ready" });
        context.EventBus.Subscribe<OpenAssemblyInExplorerEvent>(OnOpen);
    }

    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Place a `manifest.json` beside the plugin DLL (fields: `id`, `version`, `entryPoint`, `assembly`, `loadPriority`, `dependencies`, `isolationMode`). The Plugin Host discovers and validates it automatically.
