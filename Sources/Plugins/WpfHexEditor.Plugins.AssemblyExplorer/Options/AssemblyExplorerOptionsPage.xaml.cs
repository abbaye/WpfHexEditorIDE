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

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Options;

/// <summary>
/// Options page UserControl for the Assembly Explorer plugin.
/// Loaded into IDE Options > Plugins > Assembly Explorer and
/// Plugin Manager "Settings" tab.
/// </summary>
public partial class AssemblyExplorerOptionsPage : UserControl
{
    public AssemblyExplorerOptionsPage()
    {
        // Application.LoadComponent() (called by InitializeComponent()) throws NRE internally
        // when the plugin is loaded in a custom AssemblyLoadContext and the pack URI resource
        // can't be resolved. The throw happens inside WPF code BEFORE any catch block runs,
        // so VS "break when thrown" fires even though the exception would be caught upstream.
        //
        // Fix: pre-check via GetResourceStream(), which returns null without throwing when
        // the resource is unavailable. Skip InitializeComponent() entirely in that case;
        // Load()'s null guard (if FontSizeSlider is null) handles the empty-control case.
        var uri = new System.Uri(
            "/WpfHexEditor.Plugins.AssemblyExplorer;V1.0.0.0;component/options/assemblyexploreroptionspage.xaml",
            System.UriKind.Relative);

        if (Application.GetResourceStream(uri) is not null)
        {
            try { InitializeComponent(); }
            catch { /* Unexpected BAML failure — fields stay null; Load() guard handles it */ }
        }
    }

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

        SelectComboByTag(BackendCombo,      opts.DecompilerBackend);
        SelectComboByTag(QualityCombo,      opts.DecompilationQuality.ToString());

        // Populate language combo dynamically from the registry (Strategy pattern).
        // Falls back gracefully when the registry is empty (e.g., options page opened before plugin init).
        LanguageCombo.Items.Clear();
        var registeredLanguages = DecompilationLanguageRegistry.All;
        if (registeredLanguages.Count > 0)
        {
            foreach (var lang in registeredLanguages)
                LanguageCombo.Items.Add(new ComboBoxItem { Content = lang.DisplayName, Tag = lang.Id });
        }
        else
        {
            // Fallback: at minimum always show C# so the combo is never empty.
            LanguageCombo.Items.Add(new ComboBoxItem { Content = "C#", Tag = "CSharp" });
        }
        SelectComboByTag(LanguageCombo, opts.DecompileLanguage);
        if (LanguageCombo.SelectedItem is null && LanguageCombo.Items.Count > 0)
            LanguageCombo.SelectedIndex = 0;
        SelectComboByTag(CSharpVersionCombo, opts.CSharpLanguageVersion.ToString());

        ChkShowXmlDocs.IsChecked  = opts.ShowXmlDocs;
        ChkShowHidden.IsChecked   = opts.ShowHiddenMembers;
        ChkEnablePdb.IsChecked    = opts.EnablePdbIntegration;

        ChkAutoAnalyze.IsChecked    = opts.AutoAnalyzeOnFileOpen;
        ChkAutoSync.IsChecked       = opts.AutoSyncWithHexEditor;
        ChkShowResources.IsChecked  = opts.ShowResources;
        ChkShowMetadata.IsChecked   = opts.ShowMetadataTables;
        ChkInheritTheme.IsChecked   = opts.InheritIDETheme;
        ChkShowNonPublic.IsChecked  = opts.ShowNonPublicMembers;
        ChkShowInherited.IsChecked  = opts.ShowInheritedMembers;
        ChkPinAssemblies.IsChecked  = opts.PinAssembliesAcrossFileChange;
        TxtMaxAssemblies.Text       = opts.MaxLoadedAssemblies.ToString();

        // Populate recent files list
        RecentFilesList.Items.Clear();
        foreach (var path in opts.RecentFiles)
            RecentFilesList.Items.Add(path);
    }

    /// <summary>Persists current control values to <see cref="AssemblyExplorerOptions.Instance"/>.</summary>
    public void Save()
    {
        var opts = AssemblyExplorerOptions.Instance;

        opts.DecompilerFontSize    = (int)FontSizeSlider.Value;
        opts.DecompilerBackend     = GetComboTag(BackendCombo)      ?? "ILSpy";
        opts.DecompileLanguage     = GetComboTag(LanguageCombo)     ?? "CSharp";
        opts.ShowXmlDocs           = ChkShowXmlDocs.IsChecked == true;
        opts.ShowHiddenMembers     = ChkShowHidden.IsChecked   == true;
        opts.EnablePdbIntegration  = ChkEnablePdb.IsChecked    == true;

        if (Enum.TryParse<DecompilationQuality>(GetComboTag(QualityCombo), out var q))
            opts.DecompilationQuality = q;
        if (int.TryParse(GetComboTag(CSharpVersionCombo), out var v))
            opts.CSharpLanguageVersion = v;
        opts.AutoAnalyzeOnFileOpen           = ChkAutoAnalyze.IsChecked   == true;
        opts.AutoSyncWithHexEditor           = ChkAutoSync.IsChecked      == true;
        opts.ShowResources                   = ChkShowResources.IsChecked  == true;
        opts.ShowMetadataTables              = ChkShowMetadata.IsChecked   == true;
        opts.InheritIDETheme                 = ChkInheritTheme.IsChecked   == true;
        opts.ShowNonPublicMembers            = ChkShowNonPublic.IsChecked  == true;
        opts.ShowInheritedMembers            = ChkShowInherited.IsChecked  == true;
        opts.PinAssembliesAcrossFileChange   = ChkPinAssemblies.IsChecked  == true;
        if (int.TryParse(TxtMaxAssemblies.Text, out var maxVal))
            opts.MaxLoadedAssemblies = Math.Clamp(maxVal, 1, 500);
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
