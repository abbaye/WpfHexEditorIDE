// Project      : WpfHexEditorControl
// File         : Options/ArchiveExplorerOptionsPage.xaml.cs
// Description  : Code-behind for the Archive Explorer options page.
//                Load() populates controls from ArchiveExplorerOptions.Instance.
//                Save() persists them back and calls opts.Save().
//
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.ArchiveExplorer.Properties;

namespace WpfHexEditor.Plugins.ArchiveExplorer.Options;

/// <summary>
/// Options UserControl for the Archive Explorer plugin.
/// Instantiated by <see cref="ArchiveExplorerPlugin.CreateOptionsPage"/>.
/// </summary>
public partial class ArchiveExplorerOptionsPage : UserControl
{
    public ArchiveExplorerOptionsPage()
    {
        var uri = new Uri(
            "/WpfHexEditor.Plugins.ArchiveExplorer;component/options/archiveexploreroptionspage.xaml",
            UriKind.Relative);

        if (Application.GetResourceStream(uri) is not null)
        {
            try { InitializeComponent(); }
            catch { /* BAML load failure in ALC — fields stay null; Load() guard handles it */ }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Populates controls from <see cref="ArchiveExplorerOptions.Instance"/>.</summary>
    public void Load()
    {
        if (ChkAutoShow is null) return;

        var opts = ArchiveExplorerOptions.Instance;
        ChkAutoShow.IsChecked  = opts.AutoShowOnArchiveOpen;
        ChkShowRatio.IsChecked = opts.ShowCompressionRatio;
        ChkShowBadge.IsChecked = opts.ShowFormatBadge;
        TxtPreviewMaxKb.Text   = opts.PreviewMaxSizeKb.ToString();
        TxtMaxDetectKb.Text    = opts.MaxFormatDetectionSizeKb.ToString();
        TxtExtractFolder.Text  = opts.DefaultExtractFolder;
    }

    /// <summary>Persists control values to <see cref="ArchiveExplorerOptions.Instance"/> and saves to disk.</summary>
    public void Save()
    {
        if (ChkAutoShow is null) return;

        var opts = ArchiveExplorerOptions.Instance;
        opts.AutoShowOnArchiveOpen = ChkAutoShow.IsChecked  == true;
        opts.ShowCompressionRatio  = ChkShowRatio.IsChecked == true;
        opts.ShowFormatBadge       = ChkShowBadge.IsChecked == true;
        opts.DefaultExtractFolder  = TxtExtractFolder.Text  ?? string.Empty;

        if (int.TryParse(TxtPreviewMaxKb.Text, out var prev) && prev > 0)
            opts.PreviewMaxSizeKb = prev;

        if (int.TryParse(TxtMaxDetectKb.Text, out var det) && det > 0)
            opts.MaxFormatDetectionSizeKb = det;

        opts.Save();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = ArchiveExplorerResources.ArchiveExplorer_SelectExtractFolder,
            InitialDirectory = TxtExtractFolder.Text is { Length: > 0 } p
                ? p
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() == true)
            TxtExtractFolder.Text = dlg.FolderName;
    }
}
