// ==========================================================
// Project: WpfHexEditor.Plugins.PatternAnalysis
// File: PatternAnalysisPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the Pattern Analysis panel.
//     Subscribes to FileOpened on IHexEditorService and feeds
//     file bytes to PatternAnalysisPanel for entropy / pattern detection.
//     Only runs analysis when the panel is visible; cancels in-flight work on new events.
//
// Architecture Notes:
//     Pattern: Observer — host event drives panel refresh.
//     Reads up to 1 MB for analysis to remain responsive on large files.
//     Fixes #167 — removed redundant ActiveEditorChanged subscription (double-fire),
//     added IsPanelVisible guard and CancellationToken to avoid wasted work.
// ==========================================================

using System.Threading.Tasks;
using System.Windows.Threading;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.PatternAnalysis.Commands;
using WpfHexEditor.Plugins.PatternAnalysis.Properties;
using WpfHexEditor.Plugins.PatternAnalysis.Views;

namespace WpfHexEditor.Plugins.PatternAnalysis;

/// <summary>
/// Official plugin wrapping the Pattern Analysis panel.
/// Triggers byte-pattern and entropy analysis whenever a new file is opened.
/// </summary>
public sealed class PatternAnalysisPlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?      _context;
    private PatternAnalysisPanel? _panel;
    private CancellationTokenSource? _cts;

    private const string PanelUiId = "WpfHexEditor.Plugins.PatternAnalysis.Panel.PatternAnalysisPanel";

    public string  Id      => "WpfHexEditor.Plugins.PatternAnalysis";
    public string  Name    => PatternAnalysisResources.PatternAnalysis_PluginName;
    public Version Version => new(0, 4, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor          = true,
        AccessFileSystem         = false,
        RegisterMenus            = true,
        WriteOutput              = true,
        RegisterTerminalCommands = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new PatternAnalysisPanel();

        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = PatternAnalysisResources.PatternAnalysis_PanelTitle,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = PatternAnalysisResources.PatternAnalysis_MenuItem,
                ParentPath = "View",
                Group      = "Statistics",
                IconGlyph  = "\uE773",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
            });

        // FileOpened fires on both new file opens AND tab switches to already-loaded files
        // (HexEditorServiceImpl.SetActiveEditor re-fires FileOpened when IsFileLoaded).
        // Do NOT subscribe to ActiveEditorChanged — that causes a second redundant analysis pass.
        context.HexEditor.FileOpened += OnFileOpened;

        context.Terminal.RegisterCommand(new PatternShowCommand());

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
        {
            _context.HexEditor.FileOpened -= OnFileOpened;
            _context.Terminal.UnregisterCommand("pattern-show");
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private async void OnFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null || !_context.HexEditor.IsActive) return;

        // Skip expensive I/O when the panel is not visible (closed or auto-hidden).
        if (!_context.UIRegistry.IsPanelVisible(PanelUiId)) return;

        // Cancel any in-flight analysis and start a fresh run.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // Capture locals — _panel / _context may change on the next event.
        var hexEditor = _context.HexEditor;
        var panel     = _panel;
        var readLen   = (int)Math.Min(hexEditor.FileSize, 1_048_576);

        // Move to the thread pool so the UI thread stays responsive.
        // ReadBytes must be retrieved on the UI thread (DependencyObject affinity),
        // so we dispatch back only for that call, then hand off to AnalyzeAsync
        // which does its heavy computation on a background thread internally.
        await Task.Run(async () =>
        {
            if (ct.IsCancellationRequested || !hexEditor.IsActive) return;
            var data = readLen > 0
                ? await panel.Dispatcher.InvokeAsync(() => hexEditor.ReadBytes(0, readLen), DispatcherPriority.Background, ct)
                : [];
            if (ct.IsCancellationRequested) return;
            await panel.Dispatcher.InvokeAsync(() => panel.AnalyzeAsync(data), DispatcherPriority.Background, ct);
        }, ct);
    }
}
