// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Official plugin for the unified Data Inspector panel.
//     Registers one bottom-docked panel combining ByteChart (left) and
//     byte interpretations (right) as specified in PluginsSupport.md §13.
//     Implements IPluginWithOptions for IDE settings integration.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IHexEditorService.SelectionChanged
//     and FileOpened to drive panel updates automatically.
//     ByteChartPanel is no longer registered as a standalone panel;
//     the chart is embedded inside DataInspectorPanel.
// ==========================================================

using System.Windows;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.DataInspector.Options;
using WpfHexEditor.Plugins.DataInspector.Views;

namespace WpfHexEditor.Plugins.DataInspector;

/// <summary>
/// Plugin registering the unified Data Inspector panel (Bottom dock).
/// Implements <see cref="IPluginWithOptions"/> to expose its settings in the
/// IDE Options panel and in the Plugin Manager "Settings" tab.
/// </summary>
public sealed class DataInspectorPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string  Id      => "WpfHexEditor.Plugins.DataInspector";
    public string  Name    => "Data Inspector";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    private DataInspectorPanel? _panel;
    private IIDEHostContext?    _context;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new DataInspectorPanel();

        // Pass context so the panel can perform scope-aware reads (WholeFile mode).
        _panel.SetContext(context);

        // Register ONE unified panel docked on the Right (alongside ParsedFields).
        // ByteChart and byte interpretations are both inside this panel.
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.DataInspector.Panel.DataInspectorPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Data Inspector",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 280
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Data Inspector",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE9E6",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.DataInspector.Panel.DataInspectorPanel"))
            });

        // Subscribe to HexEditor events to drive panel updates automatically.
        context.HexEditor.SelectionChanged += OnSelectionChanged;
        context.HexEditor.FileOpened       += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context != null)
        {
            _context.HexEditor.SelectionChanged -= OnSelectionChanged;
            _context.HexEditor.FileOpened       -= OnFileOpened;
        }

        _panel   = null;
        _context = null;

        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }

    // ── HexEditor event handlers ──────────────────────────────────────────────

    /// <summary>
    /// Triggered on every selection/cursor change in the active HexEditor.
    /// Delegates scope resolution to the panel.
    /// </summary>
    private void OnSelectionChanged(object? sender, EventArgs e)
        => _panel?.OnHexEditorSelectionChanged();

    /// <summary>Clears the panel when a new file is opened (no active selection yet).</summary>
    private void OnFileOpened(object? sender, EventArgs e)
        => _panel?.Clear();

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    private DataInspectorOptionsPage? _optionsPage;

    /// <summary>Creates (or returns a new instance of) the options UI for this plugin.</summary>
    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new DataInspectorOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    /// <summary>Persists the current state of the options page and re-applies them to the live panel.</summary>
    public void SaveOptions()
    {
        _optionsPage?.Save();
        _panel?.ApplyOptions();
    }

    /// <summary>Reloads options from disk into the cached options page (if alive).</summary>
    public void LoadOptions() => _optionsPage?.Load();
}
