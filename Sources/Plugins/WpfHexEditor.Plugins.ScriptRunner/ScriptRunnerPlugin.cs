// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: ScriptRunnerPlugin.cs
// Description:
//     Plugin entry point for the Script Runner panel (ADR-SCRIPT-01 Phase C).
//
//     Flow:
//       1. InitializeAsync — creates VM from context.Scripting, registers dockable
//          panel and View menu item.
//       2. ShutdownAsync   — clears references.
//
// Architecture Notes:
//     If context.Scripting is null (engine not available), the panel is still
//     registered but the Run button is disabled with a status message.
// ==========================================================

using System.Windows;
using WpfHexEditor.Plugins.ScriptRunner.Options;
using WpfHexEditor.Plugins.ScriptRunner.Panels;
using WpfHexEditor.Plugins.ScriptRunner.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ScriptRunner;

/// <summary>
/// Script Runner plugin — provides a C# scripting panel backed by Roslyn.
/// </summary>
public sealed class ScriptRunnerPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    private const string PanelUiId = "WpfHexEditor.Plugins.ScriptRunner.Panel";

    private IIDEHostContext?      _context;
    private ScriptRunnerViewModel? _vm;
    private ScriptRunnerPanel?     _panel;

    // ── IWpfHexEditorPlugin ───────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.ScriptRunner";
    public string  Name    => "Script Runner";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // VM — null scripting service disables Run button gracefully.
        _vm = new ScriptRunnerViewModel(context.Scripting);

        if (context.Scripting is null)
            _vm.StatusText = "Scripting engine not available.";

        // Panel must be created on the UI thread (InitializeAsync is called there).
        _panel = new ScriptRunnerPanel(_vm);

        // Register dockable panel.
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Script Runner",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 320,
            });

        // View menu item.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Script Runner",
                ParentPath = "View",
                Group      = "Tools",
                IconGlyph  = "\uE943",   // Segoe MDL2: Code
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PanelUiId)),
            });

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _panel   = null;
        _vm      = null;
        _context = null;
        return Task.CompletedTask;
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    private ScriptRunnerOptionsPage? _optionsPage;

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new ScriptRunnerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions() => _optionsPage?.Save();

    public void LoadOptions() => _optionsPage?.Load();
}
