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
//
// Architecture Notes:
//     Pattern: Observer — host event drives panel refresh.
//     Reads up to 1 MB for analysis to remain responsive on large files.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.PatternAnalysis.Views;

namespace WpfHexEditor.Plugins.PatternAnalysis;

/// <summary>
/// Official plugin wrapping the Pattern Analysis panel.
/// Triggers byte-pattern and entropy analysis whenever a new file is opened.
/// </summary>
public sealed class PatternAnalysisPlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?     _context;
    private PatternAnalysisPanel? _panel;

    public string  Id      => "WpfHexEditor.Plugins.PatternAnalysis";
    public string  Name    => "Pattern Analysis";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new PatternAnalysisPanel();

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.PatternAnalysis.Panel.PatternAnalysisPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Pattern Analysis",
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
                Header     = "_Pattern Analysis",
                ParentPath = "View",
                Group      = "Statistics",
                IconGlyph  = "\uE773",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.PatternAnalysis.Panel.PatternAnalysisPanel"))
            });

        context.HexEditor.FileOpened += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnFileOpened;
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null || !_context.HexEditor.IsActive) return;

        var readLen = (int)Math.Min(_context.HexEditor.FileSize, 1_048_576);
        var data    = readLen > 0 ? _context.HexEditor.ReadBytes(0, readLen) : [];

        _panel.Dispatcher.BeginInvoke(() => _panel.Analyze(data));
    }
}
