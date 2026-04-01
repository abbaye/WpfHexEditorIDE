# WpfHexEditor.PluginHost

Runtime infrastructure that discovers, loads, monitors, and hot-reloads WpfHexEditor plugins inside the IDE process.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · WPF · Version 1.1.0

---

## Architecture / Modules

- **`WpfPluginHost`** — central orchestrator (`IAsyncDisposable`).
  - `DiscoverPluginsAsync` — scans `%AppData%\WpfHexEditor\Plugins\` plus optional extra directories; validates each `manifest.json` via `PluginManifestValidator`.
  - `LoadPluginAsync` — creates a collectible `AssemblyLoadContext`, resolves the entry point type, runs `InitializeAsync` on the STA Dispatcher (watchdog-bounded), initializes permissions, records CPU/memory diagnostics.
  - `TopologicalSort` — loads dependencies before dependents using `LoadPriority`.
  - `UnloadPluginAsync` — calls `ShutdownAsync`, removes all UI contributions via `UIRegistry`, releases the ALC.
  - `ReloadPluginAsync` — supports `IWpfHexEditorPluginV2.ReloadAsync` fast path or full unload + GC + reload.
  - `InstallFromFileAsync` — extracts a `.whxplugin` ZIP, validates manifest, immediately loads.
  - Periodic `DispatcherTimer` (5 s) samples process-level CPU % and managed heap into each plugin's diagnostics ring buffer.
- **`PluginWatchdog`** — enforces `InitTimeout` / `ShutdownTimeout`; raises `PluginNonResponsive` on breach.
- **`PluginCrashHandler`** — transitions plugin to `Faulted` state and raises `PluginCrashed`.
- **`PluginLoadContext`** — collectible `AssemblyLoadContext` with `AssemblyDependencyResolver` for per-plugin dependency isolation.
- **`PluginScopedContext`** — per-plugin `IIDEHostContext` wrapper that substitutes `IHexEditorService` with `TimedHexEditorService` to produce per-plugin execution metrics.
- **`TimedHexEditorService`** — decorator that records every service call duration into the plugin's diagnostics ring buffer.
- **`PluginEventBus`** — thread-safe typed publish/subscribe event bus shared across all plugins.
- **`PermissionService`** — per-plugin runtime permission map; supports runtime revocation.
- **`PluginManifestValidator`** — validates SDK/manifest version compatibility and required fields.
- **`PluginStateSerializer`** — persists and restores `PluginState` across sessions.
- **`SandboxPluginProxy`** — stub proxy for out-of-process sandbox mode (Phase 5, not yet active).
- **`Monitoring/`** — `SlowPluginDetector`, diagnostics ring buffer, `PluginDiagnosticsCollector`.
- **`Services/`** — `UIRegistry` (panel/menu/toolbar/status-bar contribution tracking), `PluginScheduler`, `PluginOptionsRegistry`.
- **`Adapters/`** — IDE service adapters bridging app-layer implementations to SDK contracts.
- **`UI/`** — `PluginManagerControl` (WPF panel for plugin management), `IDEHostContext` implementation.

---

## Usage

```csharp
// Application startup
var host = new WpfPluginHost(ideHostContext, uiRegistry, permissionService,
                              Dispatcher.CurrentDispatcher, log, logError);
await host.LoadAllAsync();

// Hot-reload a specific plugin
await host.ReloadPluginAsync("MyCompany.MyPlugin");

// Install from package
await host.InstallFromFileAsync(@"C:\Downloads\myplugin.whxplugin");

// Dispose on exit
await host.DisposeAsync();
```
