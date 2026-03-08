// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Options/AssemblyExplorerOptionsPage.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the Assembly Explorer options page.
//     Implements Load() and Save() called by the plugin entry point
//     via IPluginWithOptions.LoadOptions / SaveOptions.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Options;

/// <summary>
/// Options page UserControl for the Assembly Explorer plugin.
/// Loaded into IDE Options > Plugins > Assembly Explorer and
/// Plugin Manager "Settings" tab.
/// </summary>
public partial class AssemblyExplorerOptionsPage : UserControl
{
    public AssemblyExplorerOptionsPage()
        => InitializeComponent();

    // ── Load / Save ───────────────────────────────────────────────────────────

    /// <summary>Populates controls from <see cref="AssemblyExplorerOptions.Instance"/>.</summary>
    public void Load()
    {
        var opts = AssemblyExplorerOptions.Instance;

        FontSizeSlider.Value = opts.DecompilerFontSize;
        FontSizeLabel.Text   = $"{opts.DecompilerFontSize}pt";

        SelectComboByTag(BackendCombo,  opts.DecompilerBackend);
        SelectComboByTag(LanguageCombo, opts.DecompileLanguage);

        ChkAutoAnalyze.IsChecked  = opts.AutoAnalyzeOnFileOpen;
        ChkAutoSync.IsChecked     = opts.AutoSyncWithHexEditor;
        ChkShowResources.IsChecked = opts.ShowResources;
        ChkShowMetadata.IsChecked  = opts.ShowMetadataTables;
        ChkInheritTheme.IsChecked  = opts.InheritIDETheme;
    }

    /// <summary>Persists current control values to <see cref="AssemblyExplorerOptions.Instance"/>.</summary>
    public void Save()
    {
        var opts = AssemblyExplorerOptions.Instance;

        opts.DecompilerFontSize     = (int)FontSizeSlider.Value;
        opts.DecompilerBackend      = GetComboTag(BackendCombo)  ?? "None";
        opts.DecompileLanguage      = GetComboTag(LanguageCombo) ?? "CSharp";
        opts.AutoAnalyzeOnFileOpen  = ChkAutoAnalyze.IsChecked  == true;
        opts.AutoSyncWithHexEditor  = ChkAutoSync.IsChecked     == true;
        opts.ShowResources          = ChkShowResources.IsChecked == true;
        opts.ShowMetadataTables     = ChkShowMetadata.IsChecked  == true;
        opts.InheritIDETheme        = ChkInheritTheme.IsChecked  == true;

        opts.Save();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => FontSizeLabel.Text = $"{(int)e.NewValue}pt";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag is string t && t == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static string? GetComboTag(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string;
}
