// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: DiagnosticToolsPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Plugin entry point for the Diagnostic Tools panel.
//
//     Flow:
//       1. InitializeAsync — registers panel + menu items, subscribes to
//          ProcessLaunchedEvent / ProcessExitedEvent on IIDEEventBus.
//       2. ProcessLaunchedEvent → opens panel, creates DiagnosticsSession,
//          starts CPU/memory monitoring + EventPipe counters.
//       3. ProcessExitedEvent  → stops session, logs exit code.
//       4. ShutdownAsync       → disposes session + subscriptions.
//
// Architecture Notes:
//     Pattern: Observer + Facade.
//     DiagnosticsSession owns all attachment logic (ProcessMonitor +
//     EventCounterReader). The plugin only orchestrates lifecycle.
//     ADR-DT-01.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Plugins.DiagnosticTools.Models;
using WpfHexEditor.Plugins.DiagnosticTools.Options;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;
using WpfHexEditor.Plugins.DiagnosticTools.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.DiagnosticTools;

/// <summary>
/// Official Diagnostic Tools plugin.
/// Attaches to the process launched by "Start Without Debugging" (Ctrl+F5)
/// and displays real-time CPU, memory, GC, and exception data.
/// </summary>
public sealed class DiagnosticToolsPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    private const string PanelUiId = "WpfHexEditor.Plugins.DiagnosticTools.Panel";

    private IIDEHostContext?            _context;
    private DiagnosticToolsPanelViewModel? _vm;
    private DiagnosticToolsPanel?       _panel;
    private DiagnosticsSession?         _session;
    private volatile int                _attachedPid;

    private IDisposable? _subLaunched;
    private IDisposable? _subExited;

    // -----------------------------------------------------------------------
    // IWpfHexEditorPlugin
    // -----------------------------------------------------------------------

    public string  Id      => "WpfHexEditor.Plugins.DiagnosticTools";
    public string  Name    => "Diagnostic Tools";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
    };

    // -----------------------------------------------------------------------

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _vm      = new DiagnosticToolsPanelViewModel();
        _panel   = new DiagnosticToolsPanel(_vm);

        _panel.SnapshotRequested    += OnSnapshotRequested;
        _panel.PauseResumeRequested += OnPauseResumeRequested;
        _panel.ExportRequested      += OnExportRequested;

        // Register the dockable panel.
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Diagnostic Tools",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredHeight = 280,
            });

        // View menu item to show/hide the panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Diagnostic Tools",
                ParentPath = "View",
                Group      = "Diagnostics",
                IconGlyph  = "\uE9D9",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PanelUiId)),
            });

        // Subscribe to process lifecycle events from the build system.
        _subLaunched = context.IDEEvents.Subscribe<ProcessLaunchedEvent>(OnProcessLaunched);
        _subExited   = context.IDEEvents.Subscribe<ProcessExitedEvent>(OnProcessExited);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subLaunched?.Dispose();
        _subExited?.Dispose();

        StopCurrentSession(exitCode: null);

        if (_panel is not null)
        {
            _panel.SnapshotRequested    -= OnSnapshotRequested;
            _panel.PauseResumeRequested -= OnPauseResumeRequested;
            _panel.ExportRequested      -= OnExportRequested;
        }

        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void OnProcessLaunched(ProcessLaunchedEvent evt)
    {
        // Stop any previous session (should not happen, but be defensive).
        StopCurrentSession(exitCode: null);

        _attachedPid = evt.ProcessId;
        _vm!.Reset();
        _vm.SessionStatus = $"Attaching to '{evt.ProcessName}' (PID {evt.ProcessId})…";

        // Auto-show the panel when a process starts.
        _context?.UIRegistry.ShowPanel(PanelUiId);

        var session = new DiagnosticsSession(evt.ProcessId, _vm);
        _session = session;
        session.Start();
    }

    private void OnProcessExited(ProcessExitedEvent evt)
    {
        if (evt.ProcessId != _attachedPid) return;
        StopCurrentSession(evt.ExitCode);
    }

    private async void OnSnapshotRequested(object? sender, EventArgs e)
    {
        if (_vm is null || _attachedPid == 0) return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await HeapSnapshotService.CaptureAsync(
            _attachedPid,
            status => _vm.SessionStatus = status,
            cts.Token).ConfigureAwait(false);
    }

    private void OnPauseResumeRequested(object? sender, EventArgs e)
    {
        if (_vm is null || _session is null) return;

        if (_vm.IsPaused)
        {
            _session.Resume();
            _vm.IsPaused = false;
            _vm.SessionStatus = $"Resumed — PID {_attachedPid}";
        }
        else
        {
            _session.Pause();
            _vm.IsPaused = true;
            _vm.SessionStatus = "Paused";
        }
    }

    private async void OnExportRequested(object? sender, EventArgs e)
    {
        if (_vm is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Export Diagnostic Metrics",
            Filter     = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName   = $"DiagMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv",
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            int rows = await _vm.ExportCsvAsync(dlg.FileName).ConfigureAwait(false);
            _vm.SessionStatus = $"Exported {rows} rows → {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            _vm.SessionStatus = $"Export failed: {ex.Message}";
        }
    }

    // -----------------------------------------------------------------------

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    private DiagnosticToolsOptionsPage? _optionsPage;

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new DiagnosticToolsOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions() => _optionsPage?.Save();

    public void LoadOptions() => _optionsPage?.Load();

    // ── Private helpers ───────────────────────────────────────────────────────

    private void StopCurrentSession(int? exitCode)
    {
        var sess = Interlocked.Exchange(ref _session, null);
        if (sess is null) return;

        sess.Stop(exitCode);
        sess.Dispose();
        _attachedPid = 0;
    }
}
