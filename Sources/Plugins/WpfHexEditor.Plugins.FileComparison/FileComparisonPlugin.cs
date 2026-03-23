// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: FileComparisonPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Plugin entry point for the File Comparison panel.
//     Auto-suggests the currently open hex file as "File 1" when a file is opened,
//     so the user can immediately pick "File 2" to start a structural diff.
//
// Architecture Notes:
//     Pattern: Observer — HexEditor.FileOpened drives SuggestFile1 (pre-fill only).
//     SuggestFile1 is a no-op if File 1 is already loaded, preventing overwrite.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.FileComparison.Views;

namespace WpfHexEditor.Plugins.FileComparison;

/// <summary>
/// Official plugin wrapping the File Comparison panel.
/// Pre-fills File 1 with the active hex file on open for a one-click diff workflow.
/// </summary>
public sealed class FileComparisonPlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.FileComparison.Panel.FileComparisonPanel";

    private IIDEHostContext?      _context;
    private FileComparisonPanel?  _panel;

    public string  Id      => "WpfHexEditor.Plugins.FileComparison";
    public string  Name    => "File Comparison";
    public Version Version => new(0, 2, 1);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new FileComparisonPanel();

        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "File Comparison",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "File _Comparison",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE93D",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
            });

        // Pre-fill File 1 when a hex file is opened — user just needs to pick File 2.
        context.HexEditor.FileOpened += OnHexFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnHexFileOpened;

        _panel   = null;
        _context = null;
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnHexFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null) return;
        var path = _context.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path)) return;

        _panel.Dispatcher.BeginInvoke(
            () => _panel.SuggestFile1(path),
            DispatcherPriority.Background);
    }
}
