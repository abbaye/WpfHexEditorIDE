// Project      : WpfHexEditorControl
// File         : Options/WorkspaceOptionsPage.cs
// Description  : Options page for the Workspace System feature.
//                Controls restore behaviour (solution, open files, theme) and save prompts.
// Architecture : Code-behind-only UserControl implementing IOptionsPage.

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// Options page for "Workspace" settings.
/// Registered at startup via <c>OptionsPageRegistry.RegisterDynamic</c>.
/// </summary>
public sealed class WorkspaceOptionsPage : UserControl, IOptionsPage
{
    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly CheckBox _promptSaveCheck;
    private readonly CheckBox _restoreSolutionCheck;
    private readonly CheckBox _restoreFilesCheck;
    private readonly CheckBox _restoreThemeCheck;

    // ── IOptionsPage ──────────────────────────────────────────────────────────
    public string PageTitle    => "Workspace";
    public string CategoryName => "Environment";

    public event EventHandler? Changed;

    public void Load(AppSettings settings)
    {
        var s = settings.Workspace;
        _promptSaveCheck.IsChecked       = s.PromptSaveOnClose;
        _restoreSolutionCheck.IsChecked  = s.RestoreSolutionOnOpen;
        _restoreFilesCheck.IsChecked     = s.RestoreOpenFilesOnOpen;
        _restoreThemeCheck.IsChecked     = s.RestoreThemeOnOpen;
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.Workspace;
        s.PromptSaveOnClose      = _promptSaveCheck.IsChecked == true;
        s.RestoreSolutionOnOpen  = _restoreSolutionCheck.IsChecked == true;
        s.RestoreOpenFilesOnOpen = _restoreFilesCheck.IsChecked == true;
        s.RestoreThemeOnOpen     = _restoreThemeCheck.IsChecked == true;
    }

    public WorkspaceOptionsPage()
    {
        var root = new StackPanel { Margin = new Thickness(12) };

        // ── Section: Open Workspace ───────────────────────────────────────────
        root.Children.Add(MakeSectionHeader("When Opening a Workspace"));

        _restoreSolutionCheck = new CheckBox
        {
            Content = AppResources.App_Options_RestoreSolution,
            Margin  = new Thickness(0, 4, 0, 4)
        };
        _restoreSolutionCheck.Checked   += OnChanged;
        _restoreSolutionCheck.Unchecked += OnChanged;
        root.Children.Add(_restoreSolutionCheck);

        _restoreFilesCheck = new CheckBox
        {
            Content = AppResources.App_Options_RestoreTabs,
            Margin  = new Thickness(0, 0, 0, 4)
        };
        _restoreFilesCheck.Checked   += OnChanged;
        _restoreFilesCheck.Unchecked += OnChanged;
        root.Children.Add(_restoreFilesCheck);

        _restoreThemeCheck = new CheckBox
        {
            Content = AppResources.App_Options_RestoreTheme,
            Margin  = new Thickness(0, 0, 0, 8)
        };
        _restoreThemeCheck.Checked   += OnChanged;
        _restoreThemeCheck.Unchecked += OnChanged;
        root.Children.Add(_restoreThemeCheck);

        // ── Section: Close / Exit ─────────────────────────────────────────────
        root.Children.Add(MakeSectionHeader("Close / Exit"));

        _promptSaveCheck = new CheckBox
        {
            Content = AppResources.App_Options_PromptSaveWorkspace,
            Margin  = new Thickness(0, 4, 0, 4)
        };
        _promptSaveCheck.Checked   += OnChanged;
        _promptSaveCheck.Unchecked += OnChanged;
        root.Children.Add(_promptSaveCheck);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        // Load current values immediately so the page shows correct state when opened.
        Load(AppSettingsService.Instance.Current);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private static TextBlock MakeSectionHeader(string title) => OptionsPageHelper.SectionHeader(title);
}
