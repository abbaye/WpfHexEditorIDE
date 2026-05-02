///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Options
// File        : OptionsPageStrings.cs
// Description : Public façade that exposes OptionsResources category and page
//               name strings to external assemblies (WpfHexEditor.App).
//               OptionsResources.Designer.cs is internal by design (auto-generated);
//               this thin wrapper avoids making the designer class public.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// Public read-only accessors for Options page registry category and page name strings.
/// Values are resolved from <see cref="OptionsResources"/> (localized at runtime).
/// </summary>
public static class OptionsPageStrings
{
    // ── Category names ────────────────────────────────────────────────────────

    public static string CategoryEnvironment    => OptionsResources.Opt_Category_Environment;
    public static string CategoryHexEditor      => OptionsResources.Opt_Category_HexEditor;
    public static string CategorySolutionExplorer => OptionsResources.Opt_Category_SolutionExplorer;
    public static string CategoryCodeEditor     => OptionsResources.Opt_Category_CodeEditor;
    public static string CategoryTextEditor     => OptionsResources.Opt_Category_TextEditor;
    public static string CategoryPluginSystem   => OptionsResources.Opt_Category_PluginSystem;
    public static string CategoryBuildRun       => OptionsResources.Opt_Category_BuildRun;
    public static string CategoryDebugger       => OptionsResources.Opt_Category_Debugger;
    public static string CategoryExtensions     => OptionsResources.Opt_Category_Extensions;
    public static string CategoryFormatEditor   => OptionsResources.Opt_Category_FormatEditor;
    public static string CategoryTools          => OptionsResources.Opt_Category_Tools;

    // ── Page names ────────────────────────────────────────────────────────────

    public static string PageGeneral            => OptionsResources.Opt_Page_General;
    public static string PageSave               => OptionsResources.Opt_Page_Save;
    public static string PageOutput             => OptionsResources.Opt_Page_Output;
    public static string PageTabGroups          => OptionsResources.Opt_Page_TabGroups;
    public static string PageKeyboardShortcuts  => OptionsResources.Opt_Page_KeyboardShortcuts;
    public static string PageDocuments          => OptionsResources.Opt_Page_Documents;
    public static string PageWorkspace          => OptionsResources.Opt_Page_Workspace;
    public static string PageTabs               => OptionsResources.Opt_Page_Tabs;
    public static string PageLayout             => OptionsResources.Opt_Page_Layout;
    public static string PageDocking            => OptionsResources.Opt_Page_Docking;
    public static string PageViewMenu           => OptionsResources.Opt_Page_ViewMenu;
    public static string PageDisplay            => OptionsResources.Opt_Page_Display;
    public static string PageEditing            => OptionsResources.Opt_Page_Editing;
    public static string PageStatusBar          => OptionsResources.Opt_Page_StatusBar;
    public static string PageBehavior           => OptionsResources.Opt_Page_Behavior;
    public static string PageAppearanceColors   => OptionsResources.Opt_Page_AppearanceColors;
    public static string PageInlineHints        => OptionsResources.Opt_Page_InlineHints;
    public static string PageNavigation         => OptionsResources.Opt_Page_Navigation;
    public static string PageFormatting         => OptionsResources.Opt_Page_Formatting;
    public static string PageFeatures           => OptionsResources.Opt_Page_Features;
    public static string PageLanguageServers    => OptionsResources.Opt_Page_LanguageServers;
    public static string PageMarkdown           => OptionsResources.Opt_Page_Markdown;
    public static string PageDevelopment        => OptionsResources.Opt_Page_Development;
    public static string PageMigration          => OptionsResources.Opt_Page_Migration;
    public static string PageEventBus           => OptionsResources.Opt_Page_EventBus;
    public static string PageCompiler           => OptionsResources.Opt_Page_Compiler;
    public static string PageBreakpoints        => OptionsResources.Opt_Page_Breakpoints;
    public static string PageMarketplace        => OptionsResources.Opt_Page_Marketplace;
    public static string PageFormatExplorer     => OptionsResources.Opt_Page_FormatExplorer;
    public static string PageCompareFiles       => OptionsResources.Opt_Page_CompareFiles;
    public static string PageCommandPalette     => OptionsResources.Opt_Page_CommandPalette;
}
