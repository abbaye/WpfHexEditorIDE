// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: SynalysisGrammarPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Plugin entry point for Grammar Explorer (UFWB / Synalysis grammar support).
//     Registers the dockable panel, menus, embedded grammars, and wires the
//     full IDE integration lifecycle. Resolves GitHub issue #177.
//
// Architecture Notes:
//     Pattern: Facade + Mediator
//     - Populates SynalysisGrammarRepository with embedded resources from
//       WpfHexEditor.Definitions on startup.
//     - Discovers plugin-contributed grammars via IGrammarProvider implementations.
//     - Publishes GrammarAppliedEvent via IPluginEventBus → consumed by
//       ParsedFieldsPlugin to populate the Parsed Fields panel.
//     - SynalysisGrammarService handles async execution off the UI thread.
//     - Implements IPluginWithOptions: exposes GrammarExplorerOptionsPage in
//       IDE Options > Plugins > Grammar Explorer.
//
// Theme: WPF global theme applied in GrammarSelectorPanel.xaml via DynamicResource.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Windows;
using WpfHexEditor.Core.SynalysisGrammar;
using WpfHexEditor.Plugins.SynalysisGrammar.Options;
using WpfHexEditor.Plugins.SynalysisGrammar.Services;
using WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;
using WpfHexEditor.Plugins.SynalysisGrammar.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.SynalysisGrammar;

/// <summary>
/// Grammar Explorer plugin — adds UFWB grammar support to the IDE.
/// </summary>
public sealed class SynalysisGrammarPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string  Id      => "WpfHexEditor.Plugins.SynalysisGrammar";
    public string  Name    => "Grammar Explorer";
    public Version Version => new(0, 2, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
        AccessSettings   = true,
    };

    // -- Private state -----------------------------------------------------

    private IIDEHostContext?          _context;
    private SynalysisGrammarRepository? _repository;
    private SynalysisGrammarService?    _service;
    private GrammarSelectorPanel?       _panel;
    private GrammarSelectorViewModel?   _viewModel;
    private GrammarExplorerOptionsPage? _optionsPage;
    private CancellationTokenSource     _cts = new();

    private const string PanelId = "WpfHexEditor.Plugins.SynalysisGrammar.Panel.GrammarSelector";

    // -- Lifecycle ---------------------------------------------------------

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context    = context;
        _repository = new SynalysisGrammarRepository();
        _service    = new SynalysisGrammarService(_repository, context);

        // 1. Load embedded grammars from WpfHexEditor.Definitions.
        RegisterEmbeddedGrammars();

        // 2. Discover plugin-contributed grammars via IGrammarProvider.
        DiscoverPluginGrammars();

        // 3. Build UI (must be on UI thread — InitializeAsync is called on UI thread).
        _viewModel = new GrammarSelectorViewModel(
            _repository,
            applyCallback:        OnApplyRequested,
            loadFromDiskCallback: () => _panel?.ShowOpenDialog(),
            clearOverlayCallback: OnClearOverlayRequested);

        _panel = new GrammarSelectorPanel { ViewModel = _viewModel };
        _panel.DroppedGrammarFiles += OnGrammarFileDropped;

        _viewModel.Reload();

        // 4. Register dockable panel (Right side, tab group with ParsedFields).
        context.UIRegistry.RegisterPanel(
            PanelId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Grammar Explorer",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 340,
            });

        // 5. Register View menu entry.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Grammar Explorer",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE8A5",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelId)),
            });

        // 6. Register Analyze → Apply Grammar… menu entry.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Apply",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Apply Grammar…",
                ParentPath = "Analyze",
                Group      = "Grammar",
                IconGlyph  = "\uE768",
                Command    = new RelayCommand(_ => _panel?.ShowOpenDialog()),
            });

        // 7. Subscribe to HexEditor events.
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        context.HexEditor.FileOpened          += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _cts.Cancel();

        if (_context is not null)
        {
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context.HexEditor.FileOpened          -= OnFileOpened;
        }

        if (_panel is not null)
            _panel.DroppedGrammarFiles -= OnGrammarFileDropped;

        _panel       = null;
        _viewModel   = null;
        _optionsPage = null;
        _service     = null;
        _context     = null;
        return Task.CompletedTask;
    }

    // -- IPluginWithOptions ------------------------------------------------

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage ??= new GrammarExplorerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
    }

    public void LoadOptions()
    {
        GrammarExplorerOptions.Invalidate();
        _optionsPage?.Load();
    }

    public string GetOptionsCategory()     => "Plugins";
    public string GetOptionsCategoryIcon() => "\uE8A5";

    // -- Grammar registration ----------------------------------------------

    private void RegisterEmbeddedGrammars()
    {
        // Use a direct type reference to force-load the assembly rather than relying on
        // AppDomain.CurrentDomain.GetAssemblies(), which only returns already-loaded assemblies.
        // WpfHexEditor.Definitions embeds all .grammar files under FormatDefinitions/Synalysis/.
        var definitionsAssembly = typeof(WpfHexEditor.Definitions.EmbeddedFormatCatalog).Assembly;

        foreach (var key in definitionsAssembly.GetManifestResourceNames())
        {
            if (!key.Contains("FormatDefinitions") || !key.EndsWith(".grammar"))
                continue;

            _repository!.RegisterEmbedded(definitionsAssembly, key);
        }
    }

    private void DiscoverPluginGrammars()
    {
        // Ask the plugin system for all registered IGrammarProvider implementations.
        // If the extension point is not available we skip silently.
        try
        {
            var providers = _context?.ExtensionRegistry
                ?.GetExtensions<IGrammarProvider>() ?? [];

            foreach (var provider in providers)
            {
                foreach (var descriptor in provider.GetGrammars())
                {
                    using var stream = provider.OpenGrammar(descriptor.GrammarId);
                    if (stream is null) continue;

                    var parser  = new SynalysisGrammarParser();
                    var grammar = parser.ParseFromStream(stream);
                    var key     = "plugin:" + descriptor.GrammarId;
                    _repository!.RegisterGrammar(key, grammar);
                }
            }
        }
        catch (Exception ex)
        {
            _context?.Output?.Info($"[GrammarExplorer] Plugin grammar discovery failed: {ex.Message}");
        }
    }

    // -- Event handlers ----------------------------------------------------

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        if (_viewModel is null || _context is null) return;

        if (_viewModel.IsAutoApply && _context.HexEditor.IsActive)
            _ = AutoApplyAsync();
    }

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (_viewModel?.IsAutoApply == true)
            _ = AutoApplyAsync();
    }

    private void OnApplyRequested(GrammarEntryViewModel entry)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _ = _service?.ApplyByKeyAsync(entry.Key, _cts.Token);

        _viewModel!.StatusText = $"Applying: {entry.Name}…";
    }

    private void OnClearOverlayRequested()
    {
        _context?.HexEditor.ClearCustomBackgroundBlockByTag("synalysis:");
        if (_viewModel is not null)
            _viewModel.StatusText = "Overlay cleared.";
    }

    private void OnGrammarFileDropped(object? sender, string path)
    {
        if (_repository is null || _viewModel is null) return;

        try
        {
            _repository.RegisterFile(path);
            _viewModel.Reload();
            _context?.Output?.Info($"[GrammarExplorer] Loaded grammar: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _context?.Output?.Info($"[GrammarExplorer] Failed to load grammar '{path}': {ex.Message}");
        }
    }

    private async Task AutoApplyAsync()
    {
        if (_context?.HexEditor.CurrentFilePath is not { } path) return;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        await (_service?.AutoApplyAsync(path, _cts.Token) ?? Task.CompletedTask);
    }
}
