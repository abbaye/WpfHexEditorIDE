// ==========================================================
// Project: WpfHexEditor.App
// File: Options/DocumentsOptionsPage.cs
// Description:
//     Options page for external file change detection and auto-reload.
//     Category: Environment > Documents
//     VS-style settings: detect changes, auto-reload, ignored directories.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Environment > Documents.
/// Configures external file change detection, auto-reload, and ignored directories.
/// </summary>
public sealed class DocumentsOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly CheckBox _detectExternalChanges;
    private readonly CheckBox _autoReload;
    private readonly TextBox  _ignoredDirs;

    private bool _loading;

    public DocumentsOptionsPage()
    {
        _detectExternalChanges = new CheckBox
        {
            Content = AppResources.App_Options_DetectExternalChanges,
            Margin  = new Thickness(0, 4, 0, 4),
        };
        _detectExternalChanges.Checked   += OnChanged;
        _detectExternalChanges.Unchecked += OnDetectToggled;

        _autoReload = new CheckBox
        {
            Content = AppResources.App_Options_AutoReload,
            Margin  = new Thickness(16, 4, 0, 4),
        };
        _autoReload.Checked   += OnChanged;
        _autoReload.Unchecked += OnChanged;

        _ignoredDirs = new TextBox
        {
            Margin = new Thickness(0, 4, 0, 4),
        };
        _ignoredDirs.TextChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        // External changes section
        root.Children.Add(MakeSectionHeader("External File Changes"));
        root.Children.Add(_detectExternalChanges);
        root.Children.Add(_autoReload);
        root.Children.Add(new TextBlock
        {
            Text      = "When enabled, files modified by other programs are silently reloaded.\nWhen disabled, a warning badge (⚠) appears on modified files in the Solution Explorer.",
            Margin    = new Thickness(16, 2, 0, 8),
            FontStyle = FontStyles.Italic,
            Opacity   = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });

        // Ignored directories section
        root.Children.Add(MakeSectionHeader("Ignored Directories"));
        root.Children.Add(MakeLabeledRow("Excluded from file watching:", _ignoredDirs));
        root.Children.Add(new TextBlock
        {
            Text      = "Semicolon-separated directory names (e.g. bin;obj;.vs;.git;node_modules).\nChanges inside these directories are never flagged as external modifications.",
            Margin    = new Thickness(0, 2, 0, 8),
            FontStyle = FontStyles.Italic,
            Opacity   = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };
    }

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            _detectExternalChanges.IsChecked = settings.Documents.DetectExternalFileChanges;
            _autoReload.IsChecked            = settings.Documents.AutoReloadExternalChanges;
            _autoReload.IsEnabled            = settings.Documents.DetectExternalFileChanges;
            _ignoredDirs.Text                = settings.Documents.IgnoredDirectories ?? string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Flush(AppSettings settings)
    {
        settings.Documents.DetectExternalFileChanges = _detectExternalChanges.IsChecked == true;
        settings.Documents.AutoReloadExternalChanges = _autoReload.IsChecked == true;
        settings.Documents.IgnoredDirectories        = _ignoredDirs.Text?.Trim() ?? string.Empty;
        DocumentsAppSettings.NotifyChanged();
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDetectToggled(object sender, RoutedEventArgs e)
    {
        _autoReload.IsEnabled = _detectExternalChanges.IsChecked == true;
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TextBlock MakeSectionHeader(string title) => OptionsPageHelper.SectionHeader(title);

    private static Grid MakeLabeledRow(string labelText, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text              = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(label);
        grid.Children.Add(control);
        return grid;
    }
}
