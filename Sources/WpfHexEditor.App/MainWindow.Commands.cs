//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : MainWindow.Commands.cs
// Description  : Registers ALL built-in IDE commands into the central CommandRegistry
//                and wires _keyBindingService at startup.
// Architecture : Partial class of MainWindow. Called once from constructor/Loaded.
//                Each command wraps a RoutedCommand or Click handler via RelayCommand
//                so it can be executed without a specific CommandTarget.
//////////////////////////////////////////////

using System.Windows.Input;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // Fields set once at startup, consumed by CommandPalette and KeyboardShortcutsPage.
    internal CommandRegistry    _commandRegistry    = new();
    internal KeyBindingService  _keyBindingService  = null!;   // set in InitCommands()

    /// <summary>
    /// Initialises the command pipeline: creates the KeyBindingService and
    /// registers all built-in IDE commands. Called from the constructor.
    /// </summary>
    private void InitCommands()
    {
        _keyBindingService = new KeyBindingService(_commandRegistry);

        // Persistence is wired here (App layer owns both Commands + Options).
        // Overrides are loaded from settings in OnLoaded (after AppSettingsService.Load()).
        _keyBindingService.OverridesChanged += () =>
        {
            var s = WpfHexEditor.Core.Options.AppSettingsService.Instance;
            s.Current.KeyBindingOverrides =
                new Dictionary<string, string>(
                    _keyBindingService.GetOverrides(),
                    StringComparer.OrdinalIgnoreCase);
            s.Save();
        };

        RegisterBuiltInCommands();
    }

    /// <summary>
    /// Called from OnLoaded after AppSettingsService.Instance.Load() — populates
    /// the in-memory overrides from the persisted settings file.
    /// </summary>
    internal void LoadKeyBindingOverrides()
    {
        _keyBindingService.LoadOverrides(
            WpfHexEditor.Core.Options.AppSettingsService.Instance.Current.KeyBindingOverrides);
    }

    // -----------------------------------------------------------------------
    // Registration helpers
    // -----------------------------------------------------------------------

    private void RegisterBuiltInCommands()
    {
        // ── File ────────────────────────────────────────────────────────────
        Reg(CommandIds.File.NewFile,       "New File",               "File",    "Ctrl+N",         "\uE8A5",
            () => OnNewFile(this, null!));
        Reg(CommandIds.File.NewSolution,   "New Solution",           "File",    "Ctrl+Shift+N",   "\uE8A5",
            () => OnNewSolution(this, null!));
        Reg(CommandIds.File.NewProject,    "New Project",            "File",    null,             "\uE8A5",
            () => OnNewProject(this, null!));
        Reg(CommandIds.File.Open,          "Open File…",             "File",    "Ctrl+O",         "\uE8B5",
            () => OnOpenFile(this, null!));
        Reg(CommandIds.File.OpenSolution,  "Open Solution/Project…", "File",    "Ctrl+Shift+O",   "\uE8B5",
            () => OnOpenSolutionOrProject(this, null!));
        Reg(CommandIds.File.OpenFolder,   "Open Folder…",           "File",    "Ctrl+Shift+K",   "\uED41",
            () => OnOpenFolder(this, null!));
        Reg(CommandIds.File.Close,         "Close Tab",              "File",    "Ctrl+W",         "\uE711",
            () => OnCloseActiveDocument(this, null!));
        Reg(CommandIds.File.CloseAll,      "Close All Documents",    "File",    null,             null,
            () => OnCloseAllDocuments(this, null!));
        Reg(CommandIds.File.CloseSolution, "Close Solution",         "File",    null,             null,
            () => OnCloseSolution(this, null!));
        Reg(CommandIds.File.Save,          "Save",                   "File",    "Ctrl+S",         "\uE74E",
            () => OnSave(this, null!));
        Reg(CommandIds.File.SaveAll,       "Save All",               "File",    "Ctrl+Shift+S",   "\uE74E",
            () => OnSaveAll(this, null!));
        Reg(CommandIds.File.WriteToDisk,   "Write to Disk",          "File",    "Ctrl+Shift+W",   "\uE74E",
            () => WriteToDiskCommand.Execute(null, this));
        Reg(CommandIds.File.Exit,          "Exit",                   "File",    "Alt+F4",         "\uE7E8",
            () => OnExit(this, null!));
        Reg(CommandIds.File.QuickOpen,     "Quick File Open",        "File",    "Ctrl+P",         "\uE721",
            () => OnQuickOpen());
        Reg(CommandIds.File.ConvertToSlnx, "Convert Solution to .slnx", "File", null,             "\uE8AB",
            () => _ = OnConvertSolutionFormatAsync(toSlnx: true));
        Reg(CommandIds.File.ConvertToSln,  "Convert Solution to .sln",  "File", null,             "\uE8AB",
            () => _ = OnConvertSolutionFormatAsync(toSlnx: false));

        // ── Workspace ────────────────────────────────────────────────────────
        Reg(CommandIds.Workspace.New,    "New Workspace…",         "Workspace", null,            "\uE8A5",
            () => _ = OnNewWorkspaceAsync());
        Reg(CommandIds.Workspace.Open,   "Open Workspace…",        "Workspace", null,            "\uE8B5",
            () => _ = OnOpenWorkspaceAsync());
        Reg(CommandIds.Workspace.Save,   "Save Workspace",         "Workspace", null,            "\uE74E",
            () => _ = OnSaveWorkspaceAsync());
        Reg(CommandIds.Workspace.SaveAs, "Save Workspace As…",     "Workspace", null,            "\uE74E",
            () => _ = OnSaveWorkspaceAsAsync());
        Reg(CommandIds.Workspace.Close,  "Close Workspace",        "Workspace", null,            "\uE711",
            () => _ = OnCloseWorkspaceAsync());

        // ── Edit ────────────────────────────────────────────────────────────
        Reg(CommandIds.Edit.Undo,          "Undo",                   "Edit",    "Ctrl+Z",         "\uE7A7",
            () => ActiveDocumentEditor?.UndoCommand?.Execute(null));
        Reg(CommandIds.Edit.Redo,          "Redo",                   "Edit",    "Ctrl+Y",         "\uE7A6",
            () => ActiveDocumentEditor?.RedoCommand?.Execute(null));
        Reg(CommandIds.Edit.Cut,           "Cut",                    "Edit",    "Ctrl+X",         "\uE8C6",
            () => ActiveDocumentEditor?.CutCommand?.Execute(null));
        Reg(CommandIds.Edit.Copy,          "Copy",                   "Edit",    "Ctrl+C",         "\uE8C8",
            () => ActiveDocumentEditor?.CopyCommand?.Execute(null));
        Reg(CommandIds.Edit.Paste,         "Paste",                  "Edit",    "Ctrl+V",         "\uE77F",
            () => ActiveDocumentEditor?.PasteCommand?.Execute(null));
        Reg(CommandIds.Edit.Delete,        "Delete",                 "Edit",    "Del",            "\uE74D",
            () => ActiveDocumentEditor?.DeleteCommand?.Execute(null));
        Reg(CommandIds.Edit.SelectAll,     "Select All",             "Edit",    "Ctrl+A",         null,
            () => ActiveDocumentEditor?.SelectAllCommand?.Execute(null));
        Reg(CommandIds.Edit.Find,          "Find / Quick Search",    "Edit",    "Ctrl+F",         "\uE721",
            () => System.Windows.Input.ApplicationCommands.Find.Execute(null, this));
        Reg(CommandIds.Edit.AdvancedSearch,"Advanced Search",        "Edit",    "Ctrl+Shift+F",   "\uE721",
            () => AdvancedSearchCommand.Execute(null, this));
        Reg(CommandIds.Edit.FindNext,      "Find Next",              "Edit",    "F3",             null,
            () => FindNextCommand.Execute(null, this));
        Reg(CommandIds.Edit.FindPrevious,  "Find Previous",          "Edit",    "Shift+F3",       null,
            () => FindPreviousCommand.Execute(null, this));
        Reg(CommandIds.Edit.GoToOffset,    "Go to Offset…",          "Edit",    "Ctrl+G",         "\uE8AD",
            () => GoToOffsetCommand.Execute(null, this));

        // ── View ────────────────────────────────────────────────────────────
        Reg(CommandIds.View.CommandPalette,  "Command Palette",      "View",    "Ctrl+Shift+P",   "\uE721",
            () => ShowCommandPaletteCommand.Execute(null, this));
        Reg(CommandIds.View.WorkspaceSymbols,"Go to Symbol…",        "View",    "Ctrl+T",         "\uE8EF",
            () => ShowWorkspaceSymbolsPopup());
        Reg(CommandIds.View.SolutionExplorer,"Solution Explorer",    "View",    "Ctrl+Alt+L",     "\uE8B7",
            () => OnShowSolutionExplorer(this, null!));
        Reg(CommandIds.View.Properties,    "Properties",             "View",    "F4",             "\uE713",
            () => OnShowProperties(this, null!));
        Reg(CommandIds.View.Output,        "Output",                 "View",    null,             "\uE756",
            () => OnShowOutput(this, null!));
        Reg(CommandIds.View.ErrorList,     "Error List",             "View",    null,             "\uE783",
            () => OnShowErrorPanel(this, null!));
        Reg("View.Bookmarks",             "Bookmarks",              "View",    "Ctrl+F2",        "\uE8A4",
            () => OnShowBookmarks(this, null!));
        Reg(CommandIds.View.MarkdownOutline,"Markdown Outline",      "View",    null,             null,
            () => OnShowMarkdownOutline(this, null!));
        RegP(CommandIds.View.CompareFiles, "Compare Files…", "View", "Ctrl+Alt+D", null,
            param =>
            {
                if (param is string[] paths && paths.Length == 2)
                    _ = _compareFileLaunchService?.LaunchAsync(paths[0], paths[1]);
                else if (param is string left)
                    _ = _compareFileLaunchService?.LaunchAsync(left, null);
                else
                    _ = (_compareFileLaunchService?.LaunchAsync() ?? Task.CompletedTask);
            });
        RegP(CommandIds.View.CompareWithActiveEditor, "Compare File with Active Editor", "View / Compare",
            null, null,
            param =>
            {
                if (param is string leftPath)
                {
                    // From Solution Explorer: left=clicked file, right=active document
                    var rightPath = _documentManager.ActiveDocument?.FilePath;
                    _ = _compareFileLaunchService?.LaunchAsync(leftPath, rightPath);
                }
                else
                {
                    // From Command Palette: left=active document, picker for right
                    var path = _documentManager.ActiveDocument?.FilePath;
                    if (!string.IsNullOrEmpty(path))
                        _ = _compareFileLaunchService?.LaunchWithLeftAsync(path);
                }
            });
        Reg(CommandIds.View.CompareWithClipboard, "Compare Active File with Clipboard", "View / Compare",
            null, null,
            () => LaunchCompareWithClipboard());
        Reg(CommandIds.View.CompareWithHead, "Compare with HEAD (Git)", "View / Compare",
            null, null,
            () => _ = LaunchCompareWithHeadAsync());
        Reg(CommandIds.View.CompareReopenLast, "Reopen Last Comparison", "View / Compare",
            null, null,
            () => _ = (_compareFileLaunchService?.ReopenLastAsync() ?? Task.CompletedTask));
        Reg(CommandIds.View.EntropyAnalysis,"Entropy Analysis…",     "View",    null,             null,
            () => OnEntropyAnalysis(this, null!));
        Reg(CommandIds.View.Terminal,      "Terminal",               "View",    null,             "\uE756",
            () => OnOpenTerminal(this, null!));
        Reg(CommandIds.View.Options,       "Options…",               "View",    null,             "\uE713",
            () => OnSettings(this, null!));

        // ── View Menu Organization ────────────────────────────────────────
        Reg(CommandIds.View.ViewMenuModeFlat,       "View Menu: Flat Mode",       "View / Organize", null, "\uE700",
            () => _viewMenuOrganizer?.SetMode(WpfHexEditor.Core.Options.ViewMenuOrganizationMode.Flat));
        Reg(CommandIds.View.ViewMenuModeCategorized,"View Menu: Categorized Mode","View / Organize", null, "\uE700",
            () => _viewMenuOrganizer?.SetMode(WpfHexEditor.Core.Options.ViewMenuOrganizationMode.Categorized));
        Reg(CommandIds.View.ViewMenuModeByDockSide, "View Menu: By Dock Side",    "View / Organize", null, "\uE700",
            () => _viewMenuOrganizer?.SetMode(WpfHexEditor.Core.Options.ViewMenuOrganizationMode.ByDockSide));
        RegP(CommandIds.View.PinViewItem,   "Pin Panel to View Menu",   "View / Organize", null, "\uE718",
            p => _viewMenuOrganizer?.PinItem(p?.ToString() ?? string.Empty));
        RegP(CommandIds.View.UnpinViewItem, "Unpin Panel from View Menu","View / Organize", null, "\uE77A",
            p => _viewMenuOrganizer?.UnpinItem(p?.ToString() ?? string.Empty));

        // ── Project ─────────────────────────────────────────────────────────
        Reg(CommandIds.Project.AddNewItem,    "Add New Item…",       "Project", "Ctrl+Shift+A",   "\uE710",
            () => OnProjectAddNewItem(this, null!));
        Reg(CommandIds.Project.AddExistingItem,"Add Existing Item…", "Project", "Shift+Alt+A",    "\uE710",
            () => OnProjectAddExistingItem(this, null!));
        Reg(CommandIds.Project.AddNewProject, "Add New Project…",    "Project", null,             "\uE8A5",
            () => OnNewProject(this, null!));
        Reg(CommandIds.Project.Properties,    "Project Properties",  "Project", null,             "\uE713",
            () => OnProjectProperties(this, null!));

        // ── Build ────────────────────────────────────────────────────────────
        Reg(CommandIds.Build.BuildSolution,  "Build Solution",       "Build",   "Ctrl+Shift+B",   "\uE768",
            () => OnBuildSolution(this, null!));
        Reg(CommandIds.Build.BuildProject,   "Build Project",        "Build",   "F7",             "\uE768",
            () => OnBuildProject(this, null!));
        Reg(CommandIds.Build.RebuildSolution,"Rebuild Solution",     "Build",   null,             null,
            () => OnRebuildSolution(this, null!));
        Reg(CommandIds.Build.RebuildProject, "Rebuild Project",      "Build",   null,             null,
            () => OnRebuildProject(this, null!));
        Reg(CommandIds.Build.CleanSolution,  "Clean Solution",       "Build",   null,             null,
            () => OnCleanSolution(this, null!));
        Reg(CommandIds.Build.CleanProject,   "Clean Project",        "Build",   null,             null,
            () => OnCleanProject(this, null!));
        Reg(CommandIds.Build.Cancel,         "Cancel Build",         "Build",   null,             null,
            () => OnCancelBuild(this, null!));
        Reg(CommandIds.Build.ConfigManager,  "Configuration Manager…","Build",  null,             null,
            () => OnOpenConfigManager(this, null!));

        // ── Layout ───────────────────────────────────────────────────────────
        Reg(CommandIds.Layout.Save,       "Save Layout",            "Layout",   null,             null,
            () => OnSaveLayout(this, null!));
        Reg(CommandIds.Layout.Load,       "Load Layout",            "Layout",   null,             null,
            () => OnLoadLayout(this, null!));
        Reg(CommandIds.Layout.Reset,      "Reset Layout",           "Layout",   null,             null,
            () => OnResetLayout(this, null!));
        Reg(CommandIds.Layout.ToggleLock, "Toggle Locked Layout",   "Layout",   null,             null,
            () => OnToggleLock(this, null!));
        Reg(CommandIds.Layout.CustomizeLayout, "Customize Layout…",    "Layout",   "Ctrl+Shift+L",   "\uE713",
            () => OnCustomizeLayout());
        Reg(CommandIds.Layout.FullScreen,      "Full Screen",          "Layout",   "Alt+Enter",      "\uE740",
            () => OnToggleFullScreen());
        Reg(CommandIds.Layout.ZenMode,         "Zen Mode",             "Layout",   null,             "\uE78B",
            () => OnToggleZenMode());
        Reg(CommandIds.Layout.FocusedMode,     "Focused Mode",         "Layout",   null,             "\uE71D",
            () => OnToggleFocusedMode());
        Reg(CommandIds.Layout.PresentationMode,"Presentation Mode",    "Layout",   null,             "\uE8A3",
            () => OnTogglePresentationMode());
        Reg(CommandIds.Layout.ToggleMenuBar,   "Toggle Menu Bar",      "Layout",   null,             null,
            () => OnToggleMenuBar());
        Reg(CommandIds.Layout.ToggleToolbar,   "Toggle Toolbar",       "Layout",   null,             null,
            () => OnToggleToolbar());
        Reg(CommandIds.Layout.ToggleStatusBar, "Toggle Status Bar",    "Layout",   null,             null,
            () => OnToggleStatusBar());

        // ── Plugins ──────────────────────────────────────────────────────────
        Reg(CommandIds.Plugins.OpenManager,   "Plugin Manager",     "Plugins",  null,             "\uE74C",
            () => OpenPluginManagerCommand.Execute(null, this));
        Reg(CommandIds.Plugins.OpenMonitor,   "Plugin Monitor",     "Plugins",  null,             "\uE8EF",
            () => OnOpenPluginMonitor(this, null!));
        Reg(CommandIds.Plugins.OpenMarketplace,"Plugin Marketplace","Plugins",  null,             "\uE7BF",
            () => OnOpenMarketplace(this, null!));
        Reg(CommandIds.Plugins.InstallFromFile,"Install from File…","Plugins",  null,             "\uE8B5",
            () => OnInstallPluginFromMenu(this, null!));
        Reg(CommandIds.Plugins.RefreshAll,    "Refresh All Plugins","Plugins",  null,             "\uE72C",
            () => OnRefreshAllPlugins(this, null!));
        Reg(CommandIds.Plugins.DevWatch,      "Plugin Dev Watch…",  "Plugins",  null,             "\uE8F4",
            () => OnOpenPluginDevWatch(this, null!));
        Reg(CommandIds.Plugins.NewPluginWizard, "New Plugin Project…","Plugins", "Ctrl+Alt+N",     "\uE8A5",
            () => OnNewPluginWizard(this, null!));
        Reg(CommandIds.Plugins.PluginHotReload, "Hot-Reload Plugin",  "Plugins", "Ctrl+Shift+R",   "\uE72C",
            () => OnPluginHotReload(this, null!));

        // ── Editor ───────────────────────────────────────────────────────────
        Reg(CommandIds.Editor.FindAllReferences,  "Find All References",   "Editor", "Shift+F12",  "\uE721",
            () => ExecuteFindAllReferencesOnActiveEditor());
        Reg(CommandIds.Editor.GoToDefinition,     "Go to Definition",      "Editor", "F12",        "\uE8AD",
            () => ExecuteGoToDefinitionOnActiveEditor());
        Reg(CommandIds.Editor.GoToImplementation, "Go to Implementation",  "Editor", "Ctrl+F12",   "\uE8AD",
            () => ExecuteGoToImplementationOnActiveEditor());
        Reg(CommandIds.Editor.ShowCallHierarchy,  "Show Call Hierarchy",   "Editor", "Shift+Alt+H",  "\uE81E",
            () => ExecuteCallHierarchyOnActiveEditor());
        Reg(CommandIds.Editor.ShowTypeHierarchy,  "Show Type Hierarchy",   "Editor", "Ctrl+Alt+F12", "\uE81E",
            () => ExecuteTypeHierarchyOnActiveEditor());
        Reg(CommandIds.Editor.FormatDocument,     "Format Document",       "Editor", "Ctrl+K, Ctrl+D", "\uE70F",
            () => ExecuteFormatDocumentOnActiveEditor());
        Reg(CommandIds.Editor.FormatSelection,    "Format Selection",      "Editor", "Ctrl+K, Ctrl+F", "\uE70F",
            () => ExecuteFormatSelectionOnActiveEditor());

        // ── Debug ────────────────────────────────────────────────────────────
        Reg(CommandIds.Debug.StartDebugging,           "Start Debugging",            "Debug", "F5",            "\uE768",
            () => OnDebugStartOrContinue());
        Reg(CommandIds.Debug.StartWithoutDebugging,    "Start Without Debugging",    "Debug", "Ctrl+F5",       "\uEDB5",
            () => _ = RunStartupProjectAsync());
        Reg(CommandIds.Debug.StopDebugging,            "Stop Debugging",             "Debug", "Shift+F5",      "\uE71A",
            () => _ = _debuggerService?.StopSessionAsync());
        Reg(CommandIds.Debug.RestartDebugging,         "Restart Debugging",          "Debug", "Ctrl+Shift+F5", "\uE72C",
            () => OnDebugRestart());
        Reg(CommandIds.Debug.Continue,                 "Continue",                   "Debug", null,            "\uE768",
            () => _ = _debuggerService?.ContinueAsync());
        Reg(CommandIds.Debug.Pause,                    "Pause",                      "Debug", null,            "\uE769",
            () => _ = _debuggerService?.PauseAsync());
        Reg(CommandIds.Debug.StepOver,                 "Step Over",                  "Debug", "F10",           "\uE7EE",
            () => _ = _debuggerService?.StepOverAsync());
        Reg(CommandIds.Debug.StepInto,                 "Step Into",                  "Debug", "F11",           "\uE70D",
            () => _ = _debuggerService?.StepIntoAsync());
        Reg(CommandIds.Debug.StepOut,                  "Step Out",                   "Debug", "Shift+F11",     "\uE70E",
            () => _ = _debuggerService?.StepOutAsync());
        Reg(CommandIds.Debug.ToggleBreakpoint,         "Toggle Breakpoint",          "Debug", "F9",            "\uE7C1",
            () => OnToggleBreakpoint());
        Reg(CommandIds.Debug.DeleteAllBreakpoints,     "Delete All Breakpoints",     "Debug", "Ctrl+Shift+F9", "\uE74D",
            () => _ = _debuggerService?.ClearAllBreakpointsAsync());
        Reg(CommandIds.Debug.AttachToProcess,          "Attach to Process\u2026",    "Debug", "Ctrl+Alt+P",    "\uE71B",
            () => OnAttachToProcess());
        Reg(CommandIds.Debug.ShowBreakpoints,          "Show Breakpoints",           "Debug", null,            "\uEBE8",
            () => ShowOrCreatePanel("Breakpoints", "panel-dbg-breakpoints", DockDirection.Bottom));
        Reg(CommandIds.Debug.ShowCallStack,            "Show Call Stack",            "Debug", null,            "\uE81E",
            () => ShowOrCreatePanel("Call Stack",  "panel-dbg-callstack",   DockDirection.Bottom));
        Reg(CommandIds.Debug.ShowLocals,               "Show Locals",                "Debug", null,            "\uE943",
            () => ShowOrCreatePanel("Locals",      "panel-dbg-locals",      DockDirection.Bottom));
        Reg(CommandIds.Debug.ShowWatch,                "Show Watch",                 "Debug", null,            "\uE7B3",
            () => ShowOrCreatePanel("Watch",       "panel-dbg-watch",       DockDirection.Bottom));
    }

    // -----------------------------------------------------------------------
    // Editor command handlers (forward to the active CodeEditor)
    // -----------------------------------------------------------------------

    private void ExecuteFindAllReferencesOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.FindAllReferencesAsync();
    }

    private void ExecuteGoToDefinitionOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.GoToDefinitionAsync();
    }

    private void ExecuteGoToImplementationOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.GoToImplementationAsync();
    }

    private void ExecuteCallHierarchyOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.PrepareCallHierarchyAtCaretAsync();
    }

    private void ExecuteTypeHierarchyOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.PrepareTypeHierarchyAtCaretAsync();
    }

    private void ExecuteFormatDocumentOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.FormatDocumentAsync();
    }

    private void ExecuteFormatSelectionOnActiveEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            _ = host.PrimaryEditor.FormatSelectionAsync();
    }

    private void OnFormatDocument(object sender, System.Windows.RoutedEventArgs e)
        => ExecuteFormatDocumentOnActiveEditor();

    private void OnFormatSelection(object sender, System.Windows.RoutedEventArgs e)
        => ExecuteFormatSelectionOnActiveEditor();

    // -----------------------------------------------------------------------
    // Helper — registers a command with a plain Action delegate
    // -----------------------------------------------------------------------

    private void Reg(string id, string name, string category,
                     string? defaultGesture, string? icon, Action execute)
    {
        _commandRegistry.Register(new CommandDefinition(
            id, name, category, defaultGesture, icon,
            new RelayCommand(_ =>
            {
                _ideEventBus?.Publish(new CommandInvokedEvent { CommandId = id });
                execute();
            })));
    }

    /// <summary>Same as Reg but the execute action receives the command parameter.</summary>
    private void RegP(string id, string name, string category,
                      string? defaultGesture, string? icon, Action<object?> execute)
    {
        _commandRegistry.Register(new CommandDefinition(
            id, name, category, defaultGesture, icon,
            new RelayCommand(param =>
            {
                _ideEventBus?.Publish(new CommandInvokedEvent { CommandId = id });
                execute(param);
            })));
    }
}
