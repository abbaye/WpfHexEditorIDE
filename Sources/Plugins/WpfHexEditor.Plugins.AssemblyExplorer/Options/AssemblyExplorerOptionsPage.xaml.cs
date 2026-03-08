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
        // Guard: named fields may be null if InitializeComponent() failed to resolve
        // the BAML resource (e.g., custom AssemblyLoadContext in the plugin host).
        if (FontSizeSlider is null) return;

        var opts = AssemblyExplorerOptions.Instance;

        FontSizeSlider.Value = opts.DecompilerFontSize;
        FontSizeLabel.Text   = $"{opts.DecompilerFontSize}pt";

        SelectComboByTag(BackendCombo,  opts.DecompilerBackend);
        SelectComboByTag(LanguageCombo, opts.DecompileLanguage);

        ChkAutoAnalyze.IsChecked    = opts.AutoAnalyzeOnFileOpen;
        ChkAutoSync.IsChecked       = opts.AutoSyncWithHexEditor;
        ChkShowResources.IsChecked  = opts.ShowResources;
        ChkShowMetadata.IsChecked   = opts.ShowMetadataTables;
        ChkInheritTheme.IsChecked   = opts.InheritIDETheme;
        ChkShowNonPublic.IsChecked  = opts.ShowNonPublicMembers;
        ChkShowInherited.IsChecked  = opts.ShowInheritedMembers;
        ChkPinAssemblies.IsChecked  = opts.PinAssembliesAcrossFileChange;

        // Populate recent files list
        RecentFilesList.Items.Clear();
        foreach (var path in opts.RecentFiles)
            RecentFilesList.Items.Add(path);
    }

    /// <summary>Persists current control values to <see cref="AssemblyExplorerOptions.Instance"/>.</summary>
    public void Save()
    {
        var opts = AssemblyExplorerOptions.Instance;

        opts.DecompilerFontSize              = (int)FontSizeSlider.Value;
        opts.DecompilerBackend               = GetComboTag(BackendCombo)  ?? "None";
        opts.DecompileLanguage               = GetComboTag(LanguageCombo) ?? "CSharp";
        opts.AutoAnalyzeOnFileOpen           = ChkAutoAnalyze.IsChecked   == true;
        opts.AutoSyncWithHexEditor           = ChkAutoSync.IsChecked      == true;
        opts.ShowResources                   = ChkShowResources.IsChecked  == true;
        opts.ShowMetadataTables              = ChkShowMetadata.IsChecked   == true;
        opts.InheritIDETheme                 = ChkInheritTheme.IsChecked   == true;
        opts.ShowNonPublicMembers            = ChkShowNonPublic.IsChecked  == true;
        opts.ShowInheritedMembers            = ChkShowInherited.IsChecked  == true;
        opts.PinAssembliesAcrossFileChange   = ChkPinAssemblies.IsChecked  == true;
        // RecentFiles list is managed by AddRecentFile() — not saved from here.

        opts.Save();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // FontSizeLabel may not exist yet if the Slider fires ValueChanged during InitializeComponent.
        if (FontSizeLabel is null) return;
        FontSizeLabel.Text = $"{(int)e.NewValue}pt";
    }

    private void OnClearRecentClick(object sender, RoutedEventArgs e)
    {
        AssemblyExplorerOptions.Instance.RecentFiles.Clear();
        AssemblyExplorerOptions.Instance.Save();
        RecentFilesList.Items.Clear();
    }

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
