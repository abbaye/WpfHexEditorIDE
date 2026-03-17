// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Options/GrammarExplorerOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Grammar Explorer options page.
//     Implements Load() and Save() called by the plugin entry point
//     via IPluginWithOptions.LoadOptions / SaveOptions.
//
// Architecture Notes:
//     Pack URI pre-check is required because the plugin runs in a custom
//     AssemblyLoadContext and Application.GetResourceStream() may return null
//     before the resource is available. Guard prevents a WPF NRE inside
//     InitializeComponent(). Same pattern as AssemblyExplorerOptionsPage.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Options;

/// <summary>
/// Options page UserControl for the Grammar Explorer plugin.
/// Loaded into IDE Options > Plugins > Grammar Explorer and
/// Plugin Manager "Settings" tab.
/// </summary>
public partial class GrammarExplorerOptionsPage : UserControl
{
    public GrammarExplorerOptionsPage()
    {
        // Pre-check: Application.GetResourceStream() returns null without throwing
        // when the BAML resource cannot be resolved from this AssemblyLoadContext.
        // This prevents the WPF NRE that fires inside InitializeComponent() before
        // any catch block can intercept it.
        var uri = new System.Uri(
            "/WpfHexEditor.Plugins.SynalysisGrammar;component/options/grammarexploreroptionspage.xaml",
            System.UriKind.Relative);

        if (Application.GetResourceStream(uri) is not null)
        {
            try { InitializeComponent(); }
            catch { /* Unexpected BAML failure — fields stay null; Load() guard handles it */ }
        }
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    /// <summary>Populates controls from <see cref="GrammarExplorerOptions.Instance"/>.</summary>
    public void Load()
    {
        // Guard: named fields may be null if InitializeComponent() failed.
        if (ChkAutoApply is null) return;

        var opts = GrammarExplorerOptions.Instance;

        ChkAutoApply.IsChecked           = opts.AutoApplyOnFileOpen;
        SampleSizeSlider.Value           = opts.MaxSampleSizeKb;
        SampleSizeLabel.Text             = $"{opts.MaxSampleSizeKb} KB";
        ChkShowEmbedded.IsChecked        = opts.ShowEmbeddedGrammars;
        ChkShowFile.IsChecked            = opts.ShowFileGrammars;
        ChkShowPlugin.IsChecked          = opts.ShowPluginGrammars;
        ChkClearOverlayOnRemoval.IsChecked = opts.ClearOverlayOnRemoval;
    }

    /// <summary>Persists current control values to <see cref="GrammarExplorerOptions.Instance"/>.</summary>
    public void Save()
    {
        if (ChkAutoApply is null) return;

        var opts = GrammarExplorerOptions.Instance;

        opts.AutoApplyOnFileOpen  = ChkAutoApply.IsChecked           == true;
        opts.MaxSampleSizeKb      = (int)SampleSizeSlider.Value;
        opts.ShowEmbeddedGrammars = ChkShowEmbedded.IsChecked        == true;
        opts.ShowFileGrammars     = ChkShowFile.IsChecked            == true;
        opts.ShowPluginGrammars   = ChkShowPlugin.IsChecked          == true;
        opts.ClearOverlayOnRemoval = ChkClearOverlayOnRemoval.IsChecked == true;

        opts.Save();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnSampleSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SampleSizeLabel is null) return;
        SampleSizeLabel.Text = $"{(int)e.NewValue} KB";
    }
}
