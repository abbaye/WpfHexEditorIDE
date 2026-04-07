//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : CommandIds.cs
// Description  : Canonical string IDs for all built-in IDE commands.
//                Used as keys in CommandRegistry and KeyBindingOverrides.
// Architecture : Pure constants — no dependencies. Safe to reference from any layer.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Commands;

/// <summary>
/// Canonical identifiers for all built-in IDE commands.
/// Grouped by menu category to match the IDE menu structure.
/// </summary>
public static class CommandIds
{
    public static class File
    {
        public const string NewFile          = "File.NewFile";
        public const string NewSolution      = "File.NewSolution";
        public const string NewProject       = "File.NewProject";
        public const string Open             = "File.Open";
        public const string OpenSolution     = "File.OpenSolution";
        public const string OpenFolder       = "File.OpenFolder";
        public const string Close            = "File.Close";
        public const string CloseAll         = "File.CloseAll";
        public const string CloseSolution    = "File.CloseSolution";
        public const string Save             = "File.Save";
        public const string SaveAll          = "File.SaveAll";
        public const string WriteToDisk      = "File.WriteToDisk";
        public const string Exit             = "File.Exit";
        public const string QuickOpen        = "File.QuickOpen";
        public const string ConvertToSlnx    = "File.ConvertToSlnx";
        public const string ConvertToSln     = "File.ConvertToSln";
        public const string ConvertToWhsln   = "File.ConvertToWhsln";
    }

    public static class Edit
    {
        public const string Undo             = "Edit.Undo";
        public const string Redo             = "Edit.Redo";
        public const string Cut              = "Edit.Cut";
        public const string Copy             = "Edit.Copy";
        public const string Paste            = "Edit.Paste";
        public const string Delete           = "Edit.Delete";
        public const string SelectAll        = "Edit.SelectAll";
        public const string Find             = "Edit.Find";
        public const string AdvancedSearch   = "Edit.AdvancedSearch";
        public const string FindNext         = "Edit.FindNext";
        public const string FindPrevious     = "Edit.FindPrevious";
        public const string GoToOffset       = "Edit.GoToOffset";
    }

    public static class View
    {
        public const string CommandPalette    = "View.CommandPalette";
        public const string WorkspaceSymbols  = "View.WorkspaceSymbols";
        public const string SolutionExplorer  = "View.SolutionExplorer";
        public const string Properties        = "View.Properties";
        public const string Output            = "View.Output";
        public const string ErrorList         = "View.ErrorList";
        public const string MarkdownOutline   = "View.MarkdownOutline";
        public const string CompareFiles           = "View.CompareFiles";
        public const string CompareWithActiveEditor = "View.Compare.WithActiveEditor";
        public const string CompareWithClipboard    = "View.Compare.WithClipboard";
        public const string CompareWithHead         = "View.Compare.WithHead";
        public const string CompareReopenLast       = "View.Compare.ReopenLast";
        public const string EntropyAnalysis   = "View.EntropyAnalysis";
        public const string Terminal          = "View.Terminal";
        public const string Options           = "View.Options";

        // View Menu Organization
        public const string ViewMenuModeFlat        = "View.MenuMode.Flat";
        public const string ViewMenuModeCategorized = "View.MenuMode.Categorized";
        public const string ViewMenuModeByDockSide  = "View.MenuMode.ByDockSide";
        public const string PinViewItem             = "View.PinItem";
        public const string UnpinViewItem           = "View.UnpinItem";
    }

    public static class Project
    {
        public const string AddNewItem        = "Project.AddNewItem";
        public const string AddExistingItem   = "Project.AddExistingItem";
        public const string AddNewProject     = "Project.AddNewProject";
        public const string Properties        = "Project.Properties";
    }

    public static class Build
    {
        public const string BuildSolution     = "Build.BuildSolution";
        public const string BuildProject      = "Build.BuildProject";
        public const string RebuildSolution   = "Build.RebuildSolution";
        public const string RebuildProject    = "Build.RebuildProject";
        public const string CleanSolution     = "Build.CleanSolution";
        public const string CleanProject      = "Build.CleanProject";
        public const string Cancel            = "Build.Cancel";
        public const string ConfigManager     = "Build.ConfigManager";
    }

    public static class Layout
    {
        public const string Save              = "Layout.Save";
        public const string Load              = "Layout.Load";
        public const string Reset             = "Layout.Reset";
        public const string ToggleLock        = "Layout.ToggleLock";
        public const string CustomizeLayout   = "Layout.CustomizeLayout";
        public const string FullScreen        = "Layout.FullScreen";
        public const string ZenMode           = "Layout.ZenMode";
        public const string FocusedMode       = "Layout.FocusedMode";
        public const string PresentationMode  = "Layout.PresentationMode";
        public const string ToggleMenuBar     = "Layout.ToggleMenuBar";
        public const string ToggleToolbar     = "Layout.ToggleToolbar";
        public const string ToggleStatusBar   = "Layout.ToggleStatusBar";
    }

    public static class Debug
    {
        public const string StartDebugging       = "Debug.StartDebugging";
        public const string StopDebugging        = "Debug.StopDebugging";
        public const string RestartDebugging     = "Debug.RestartDebugging";
        public const string Continue             = "Debug.Continue";
        public const string StepOver             = "Debug.StepOver";
        public const string StepInto             = "Debug.StepInto";
        public const string StepOut              = "Debug.StepOut";
        public const string ToggleBreakpoint     = "Debug.ToggleBreakpoint";
        public const string DeleteAllBreakpoints = "Debug.DeleteAllBreakpoints";
        public const string AttachToProcess      = "Debug.AttachToProcess";
        public const string ShowBreakpoints      = "Debug.ShowBreakpoints";
        public const string ShowCallStack        = "Debug.ShowCallStack";
        public const string ShowLocals           = "Debug.ShowLocals";
        public const string StartWithoutDebugging = "Debug.StartWithoutDebugging";
        public const string Pause                 = "Debug.Pause";
        public const string ShowWatch             = "Debug.ShowWatch";
    }

    public static class Workspace
    {
        public const string New     = "Workspace.New";
        public const string Open    = "Workspace.Open";
        public const string Save    = "Workspace.Save";
        public const string SaveAs  = "Workspace.SaveAs";
        public const string Close   = "Workspace.Close";
    }

    public static class Editor
    {
        public const string FindAllReferences  = "Editor.FindAllReferences";
        public const string GoToDefinition     = "Editor.GoToDefinition";
        public const string GoToImplementation = "Editor.GoToImplementation";
        public const string PeekDefinition     = "Editor.PeekDefinition";
        public const string FormatDocument     = "Editor.FormatDocument";
        public const string FormatSelection    = "Editor.FormatSelection";
        public const string ShowCallHierarchy  = "Editor.ShowCallHierarchy";
        public const string ShowTypeHierarchy  = "Editor.ShowTypeHierarchy";
    }

    public static class TabGroup
    {
        public const string NewVertical        = "TabGroup.NewVertical";
        public const string NewHorizontal      = "TabGroup.NewHorizontal";
        public const string MoveToNext         = "TabGroup.MoveToNext";
        public const string MoveToPrevious     = "TabGroup.MoveToPrevious";
        public const string CloseCurrentGroup  = "TabGroup.CloseCurrentGroup";
        public const string CloseAllGroups     = "TabGroup.CloseAllGroups";
        public const string FocusGroup1        = "TabGroup.FocusGroup1";
        public const string FocusGroup2        = "TabGroup.FocusGroup2";
        public const string FocusGroup3        = "TabGroup.FocusGroup3";
        public const string FocusGroup4        = "TabGroup.FocusGroup4";
    }

    public static class Plugins
    {
        public const string OpenManager      = "Plugins.OpenManager";
        public const string OpenMonitor      = "Plugins.OpenMonitor";
        public const string OpenMarketplace  = "Plugins.OpenMarketplace";
        public const string InstallFromFile  = "Plugins.InstallFromFile";
        public const string RefreshAll       = "Plugins.RefreshAll";
        public const string DevWatch         = "Plugins.DevWatch";
        public const string NewPluginWizard  = "Plugins.NewPluginWizard";
        public const string PluginHotReload  = "Plugins.PluginHotReload";
    }
}
