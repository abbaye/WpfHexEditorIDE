# Plugin Monitoring Panel

The Plugin Monitoring Panel provides real-time CPU and memory charts for every loaded plugin, helping you diagnose performance issues and resource leaks without any external tooling.

---

## 📋 Table of Contents

- [Opening the Panel](#opening-the-panel)
- [What You See](#what-you-see)
- [Metrics Explained](#metrics-explained)
- [Chart Behavior](#chart-behavior)
- [Integration with Plugin Manager](#integration-with-plugin-manager)
- [Architecture](#architecture)
- [Known Limitations](#known-limitations)
- [See Also](#see-also)

---

## Opening the Panel

| Method | Action |
|--------|--------|
| **Tools → Plugin Monitoring** | Opens / focuses the panel |
| **Tools → Plugin Manager → (select plugin)** | Jumps to chart for selected plugin |

ContentId: `panel-plugin-monitoring`

The panel docks by default with the other diagnostic panels. It can be floated, auto-hidden, or tabbed with Plugin Manager.

---

## What You See

Each loaded plugin gets its own row containing:

```
┌──────────────────────────────────────────────────────────┐
│  [•] com.example.myplugin  v1.0.0          Loaded        │
│                                                           │
│  CPU %   ████▂▄▆▄▂▁▁▁▂▄▄▂▁▁          2.3 %              │
│  Mem MB  ▁▁▁▁▂▂▂▂▂▂▂▂▂▂▂▂▂▂         12.4 MB             │
└──────────────────────────────────────────────────────────┘
```

- **Status dot**: Green = Loaded, Yellow = Deactivated, Red = Faulted
- **CPU chart**: Rolling Polyline of CPU% over last N samples
- **Memory chart**: Rolling Polyline of managed memory (MB) over last N samples
- **Current value**: Numeric display next to each chart

---

## Metrics Explained

### CPU%

Measured via `PerformanceCounter("Process", "% Processor Time")` for the current IDE process, **attributed** to the plugin thread by `PluginWatchdog.WrapAsync()` timing.

> Note: CPU% is attributed per-plugin using elapsed time from `WrapAsync()` (returns `Task<TimeSpan>`). It is an approximation — it measures how long plugin code ran relative to the polling interval, not actual thread CPU time.

### Memory (MB)

Measured via `GC.GetTotalMemory(forceFullCollection: false)` snapshot. This is total managed heap, not per-plugin. Per-plugin delta is estimated by comparing snapshots before and after plugin activation.

> For true per-plugin memory isolation, the out-of-process `PluginSandbox` provides OS-level process memory counters.

### Polling Interval

Default: **1 second** (1000 ms). Configurable in **Options → Plugins → Monitoring interval (ms)**.

Maximum history points: configurable via `AppSettings.PluginSystemSettings.MaxHistoryPoints` (default: 120 = 2 minutes of history at 1 s).

---

## Chart Behavior

- Charts are pure WPF **Canvas + Polyline** — no third-party charting library.
- Points are added on each poll tick; oldest points are removed when the buffer exceeds `MaxHistoryPoints`.
- Chart redraws on `CollectionChanged` from the rolling history collection.
- CPU spikes above a threshold are highlighted with a different stroke color (configurable via theme brushes `PM_ChartCpuHighBrush`).
- Charts auto-scale Y-axis to the max value in the current window.

### Theme Brush Keys

| Key | Description |
|-----|-------------|
| `PM_BackgroundBrush` | Panel background |
| `PM_RowBorderBrush` | Row separator |
| `PM_ChartCpuBrush` | CPU chart line color |
| `PM_ChartCpuHighBrush` | CPU spike color |
| `PM_ChartMemBrush` | Memory chart line color |
| `PM_StatusLoadedBrush` | Status dot — Loaded |
| `PM_StatusFaultedBrush` | Status dot — Faulted |
| `PM_StatusDisabledBrush` | Status dot — Disabled |
| `PM_LabelForegroundBrush` | Plugin name label |
| `PM_ValueForegroundBrush` | Numeric value text |

All keys are defined in all 8 built-in themes (Dark, Light, VS2022Dark, DarkGlass, Minimal, Office, Cyberpunk, VisualStudio).

---

## Integration with Plugin Manager

`PluginManagerViewModel.SelectionChanged` is wired to the monitoring panel — selecting a plugin in the **Plugin Manager** scrolls the monitoring panel to that plugin's row and briefly highlights it.

Conversely, clicking a row in the monitoring panel is **not** wired to select the plugin in Plugin Manager (one-way link). This may be added in a future version.

---

## Architecture

```
WpfHexEditor.PluginHost/
  PluginMonitoringViewModel.cs   — observes PluginEntry collection; polls at 1 s interval;
                                   rolling chart history per plugin
  PluginMonitoringPanel.xaml     — VS-style dockable panel
  PluginMonitoringPanel.xaml.cs  — code-behind (DataContext wired via deferred Dispatcher.InvokeAsync)

  PluginManagerViewModel.cs      — Enable/Disable/Uninstall/Reload commands; SelectionChanged
                                   wired to monitoring panel
  PluginManagerControl.xaml      — DataGrid + toolbar + details pane
  PluginListItemViewModel.cs     — LiveCpuPercent, LiveMemoryMb, DiagnosticsSummary properties
```

### DataContext Wiring

Because `PluginMonitoringPanel` may be restored from layout before the plugin system initializes, DataContext is wired via a deferred dispatch in `MainWindow.PluginSystem.cs`:

```csharp
// After InitializePluginSystemAsync completes:
await Dispatcher.InvokeAsync(() =>
{
    if (_pendingPluginMonitorPanel is not null)
        _pendingPluginMonitorPanel.DataContext = _pluginMonitoringViewModel;
}, DispatcherPriority.Background);
```

This prevents the panel from showing a blank placeholder on layout restore.

---

## Enabling/Disabling Monitoring

Monitoring can be toggled globally in **Options → Plugins → Monitoring enabled**.

When disabled:
- `PluginMonitoringViewModel` stops the polling timer
- Charts freeze at last known values
- CPU/memory polling overhead drops to zero

Settings (`AppSettings.PluginSystemSettings`):

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MonitoringEnabled` | bool | true | Enable/disable all polling |
| `MonitoringIntervalMs` | int | 1000 | Poll interval in milliseconds |
| `MaxHistoryPoints` | int | 120 | Rolling chart buffer size |

---

## Known Limitations

- **CPU attribution is approximate** — measured via elapsed-time wrapping, not true per-thread CPU time.
- **Memory is total managed heap** — not isolated per-plugin unless using `PluginSandbox`.
- **No alert thresholds** — the panel is display-only; no automatic actions are triggered when limits are exceeded (planned for a future version).
- **Chart rendering is synchronous on UI thread** — for a very large number of plugins (> 50), consider increasing the poll interval to reduce UI load.

---

## See Also

- **[Plugin System](Plugin-System)** — plugin lifecycle and SDK
- **[Plugin Manager](Plugin-System#plugin-manager)** — enable/disable/reload
- **[Architecture Overview](Architecture-Overview)** — ContentId routing
- **[FAQ](FAQ#how-do-i-monitor-plugin-performance)** — quick FAQ answer
