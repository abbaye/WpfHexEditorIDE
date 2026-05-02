//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : MainWindow.ViewMenu.cs
// Description  : Partial class that initialises the dynamic View menu system.
//                Registers built-in entries, wires the ViewMenuOrganizer to
//                the MenuAdapter's ViewItemsChanged event, and performs the
//                initial menu build.
// Architecture : Partial class of MainWindow (UI wiring layer).
//////////////////////////////////////////////

using System.Windows.Input;
using WpfHexEditor.App.Services.ViewMenu;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    private ViewMenuOrganizer? _viewMenuOrganizer;

    /// <summary>
    /// Creates the <see cref="ViewMenuOrganizer"/>, registers all built-in View entries,
    /// subscribes to <see cref="Services.MenuAdapter.ViewItemsChanged"/>, and performs
    /// the first menu build.
    /// <para>
    /// Must be called after <c>_menuAdapter</c> and <c>_dockingAdapter</c> are resolved
    /// from the service provider, and <strong>before</strong> plugins load (so that plugin
    /// contributions trigger <c>ViewItemsChanged → RebuildMenu</c>).
    /// </para>
    /// </summary>
    private void InitViewMenuOrganizer()
    {
        if (_menuAdapter is null) return;

        _viewMenuOrganizer = new ViewMenuOrganizer(
            ViewMenu,                              // x:Name="ViewMenu" in MainWindow.xaml
            _menuAdapter,
            AppSettingsService.Instance,
            uiId => _dockingAdapter?.GetPanelDockSide(uiId));

        RegisterBuiltInViewEntries();

        _menuAdapter.ViewItemsChanged += OnViewMenuItemsChanged;

        // Initial build (only built-in items; plugins will trigger rebuilds later).
        _viewMenuOrganizer.RebuildMenu();
    }

    /// <summary>
    /// Registers the hardcoded View menu items as <see cref="ViewMenuEntry"/> records.
    /// These correspond to the items formerly defined in MainWindow.xaml.
    /// </summary>
    private void RegisterBuiltInViewEntries()
    {
        if (_viewMenuOrganizer is null) return;

        // Command Palette — always first, outside any category
        _viewMenuOrganizer.RegisterCommandPaletteEntry(new ViewMenuEntry(
            Id:                CommandIds.View.CommandPalette,
            Header:            "_Command Palette",
            GestureText:       "Ctrl+Shift+P",
            IconGlyph:         "",
            Command:           ShowCommandPaletteCommand,
            CommandParameter:  null,
            Group:             null,
            Category:          null,
            DockSide:          null,
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_CommandPalette"));

        // Core IDE panels
        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.SolutionExplorer,
            Header:            "_Solution Explorer",
            GestureText:       "Ctrl+Alt+L",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowSolutionExplorer(this, null!)),
            CommandParameter:  null,
            Group:             "Core IDE",
            Category:          ViewMenuClassifier.CoreIDE,
            DockSide:          "Left",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_SolutionExplorer"));

        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.Properties,
            Header:            "_Properties",
            GestureText:       "F4",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowProperties(this, null!)),
            CommandParameter:  null,
            Group:             "Core IDE",
            Category:          ViewMenuClassifier.CoreIDE,
            DockSide:          "Right",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_Properties"));

        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.Output,
            Header:            "_Output",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowOutput(this, null!)),
            CommandParameter:  null,
            Group:             "Core IDE",
            Category:          ViewMenuClassifier.CoreIDE,
            DockSide:          "Bottom",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_Output"));

        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.ErrorList,
            Header:            "_Error List",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowErrorPanel(this, null!)),
            CommandParameter:  null,
            Group:             "Core IDE",
            Category:          ViewMenuClassifier.CoreIDE,
            DockSide:          "Bottom",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_ErrorList"));

        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.Terminal,
            Header:            "_Terminal",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnOpenTerminal(this, null!)),
            CommandParameter:  null,
            Group:             "Core IDE",
            Category:          ViewMenuClassifier.CoreIDE,
            DockSide:          "Bottom",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_Terminal"));

        // Editors & Code
        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.MarkdownOutline,
            Header:            "_Markdown Outline",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowMarkdownOutline(this, null!)),
            CommandParameter:  null,
            Group:             "Editors & Code",
            Category:          ViewMenuClassifier.EditorsCode,
            DockSide:          "Right",
            ToolTip:           "Show Markdown Outline panel (heading navigator)",
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_MarkdownOutline",
            ToolTipResourceKey: "APP_VM_TipMarkdown"));

        // Data & Files
        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.CompareFiles,
            Header:            "_Compare Files…",
            GestureText:       "Ctrl+Alt+D",
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnCompareFiles(this, null!)),
            CommandParameter:  null,
            Group:             "Data & Files",
            Category:          ViewMenuClassifier.DataFiles,
            DockSide:          "Center",
            ToolTip:           null,
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_CompareFiles"));

        // Format Editor (.whfmt)
        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                "View.FormatBrowser",
            Header:            "_Format Browser",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowFormatBrowser(this, null!)),
            CommandParameter:  null,
            Group:             "Format Editor",
            Category:          ViewMenuClassifier.EditorsCode,
            DockSide:          "Right",
            ToolTip:           "Browse and manage built-in and user .whfmt format definitions",
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_FormatBrowser",
            ToolTipResourceKey: "APP_VM_TipFormatBrowser"));

        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                "View.FormatCatalog",
            Header:            "_Format Catalog",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnShowFormatCatalog(this, null!)),
            CommandParameter:  null,
            Group:             "Format Editor",
            Category:          ViewMenuClassifier.EditorsCode,
            DockSide:          "Center",
            ToolTip:           "Open the Format Catalog document tab (all formats in a sortable grid)",
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_FormatCatalog",
            ToolTipResourceKey: "APP_VM_TipFormatCatalog"));

        // Analysis
        _viewMenuOrganizer.RegisterBuiltInEntry(new ViewMenuEntry(
            Id:                CommandIds.View.EntropyAnalysis,
            Header:            "_Entropy Analysis…",
            GestureText:       null,
            IconGlyph:         "",
            Command:           new RelayCommand(_ => OnEntropyAnalysis(this, null!)),
            CommandParameter:  null,
            Group:             "Analysis",
            Category:          ViewMenuClassifier.Analysis,
            DockSide:          null,
            ToolTip:           "Analyse entropy and byte distribution of the active file",
            IsBuiltIn:         true,
            HeaderResourceKey: "APP_VM_EntropyAnalysis",
            ToolTipResourceKey: "APP_VM_TipEntropy"));
    }

    /// <summary>Handler for <see cref="Services.MenuAdapter.ViewItemsChanged"/>.</summary>
    private void OnViewMenuItemsChanged()
        => Dispatcher.InvokeAsync(() => _viewMenuOrganizer?.RebuildMenu());
}
