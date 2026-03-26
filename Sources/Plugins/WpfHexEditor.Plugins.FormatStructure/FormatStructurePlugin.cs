// ==========================================================
// Project: WpfHexEditor.Plugins.FormatStructure
// File: FormatStructurePlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Plugin entry point for the Format Structure Tree panel (D1).
//     Shows the full hierarchical tree of all parsed blocks from the
//     active .whfmt format definition — like 010 Editor Template Results.
//
// Architecture Notes:
//     Listens to FormatDetected + ActiveEditorChanged to rebuild the tree.
//     Field selection in the tree → IHexEditorService.SetSelection + NavigateTo.
// ==========================================================

using WpfHexEditor.Core;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Plugins.FormatStructure.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.FormatStructure;

public sealed class FormatStructurePlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext? _context;
    private FormatStructurePanel? _panel;

    private const string PanelUiId = "WpfHexEditor.Plugins.FormatStructure.Panel";

    public string  Id      => "WpfHexEditor.Plugins.FormatStructure";
    public string  Name    => "Format Structure Tree";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = false
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new FormatStructurePanel();

        // Register dockable panel
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Format Structure",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 360
            });

        // View menu entry
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Format _Structure Tree",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE8FD",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
            });

        // Wire events
        context.HexEditor.FormatDetected      += OnFormatDetected;
        context.HexEditor.ActiveEditorChanged  += OnActiveEditorChanged;

        // Wire field click → hex editor navigation
        _panel.FieldNavigateRequested += OnFieldNavigateRequested;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context != null)
        {
            _context.HexEditor.FormatDetected      -= OnFormatDetected;
            _context.HexEditor.ActiveEditorChanged  -= OnActiveEditorChanged;
        }

        if (_panel != null)
            _panel.FieldNavigateRequested -= OnFieldNavigateRequested;

        _panel   = null;
        _context = null;
        return Task.CompletedTask;
    }

    private void OnFormatDetected(object? sender, FormatDetectedArgs e)
    {
        if (_panel == null || !e.Success) return;

        // Cast the raw definition to get blocks
        if (e.RawFormatDefinition is FormatDefinition fmt)
        {
            _panel.LoadFormat(fmt.FormatName ?? "Unknown", fmt.Blocks);
        }
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        // Clear panel when switching editors — FormatDetected will repopulate
        _panel?.Clear();
    }

    private void OnFieldNavigateRequested(object? sender, StructureFieldNode node)
    {
        if (_context == null || node.Offset < 0) return;

        _context.HexEditor.NavigateTo(node.Offset);
        if (node.Length > 0)
            _context.HexEditor.SetSelection(node.Offset, node.Offset + node.Length - 1);
    }
}
