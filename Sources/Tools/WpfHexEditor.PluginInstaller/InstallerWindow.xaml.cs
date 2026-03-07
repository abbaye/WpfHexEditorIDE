//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.PluginInstaller;

/// <summary>
/// Installation dialog for a .whxplugin package.
/// Shows metadata, permissions, trust badge, and SHA-256 verification result,
/// then extracts the package to %AppData%\WpfHexEditor\Plugins\.
/// </summary>
public partial class InstallerWindow : Window
{
    private readonly PluginPackageExtractor _extractor = new();
    private readonly string _packagePath;
    private PluginMetadata? _meta;
    private CancellationTokenSource? _cts;

    public InstallerWindow(string packagePath)
    {
        InitializeComponent();
        _packagePath = packagePath;
        Loaded += OnLoaded;
    }

    // -- Startup ----------------------------------------------------------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _meta = await _extractor.InspectAsync(_packagePath).ConfigureAwait(true);
            PopulateUI(_meta);
        }
        catch (PluginInstallException ex)
        {
            ShowError($"Cannot read package:\n{ex.Message}");
            BtnInstall.IsEnabled = false;
        }
    }

    private void PopulateUI(PluginMetadata meta)
    {
        TxtPluginName.Text   = meta.Name;
        TxtVersion.Text      = meta.Version;
        TxtAuthor.Text       = string.IsNullOrWhiteSpace(meta.Author) ? "—" : meta.Author;
        TxtMinIde.Text       = string.IsNullOrWhiteSpace(meta.MinIDEVersion) ? "—" : meta.MinIDEVersion;
        TxtAssembly.Text     = meta.AssemblyFile;
        TxtDescription.Text  = string.IsNullOrWhiteSpace(meta.Description) ? "No description provided." : meta.Description;

        // Trust badge
        if (meta.TrustedPublisher)
        {
            TrustBadge.Style = (Style)Resources["TrustedBadgeStyle"];
            TrustLabel.Text  = "Trusted Publisher";
        }
        else
        {
            TrustBadge.Style = (Style)Resources["UntrustedBadgeStyle"];
            TrustLabel.Text  = "Unverified Publisher";
        }

        // Permissions: show rows that are declared
        // We have no capabilities on the inspector — show all rows as warnings for now.
        // (Phase 4+ plugins declare capabilities in manifest.permissions)
        PermHexEditor.Opacity     = 1;
        PermFileSystem.Opacity    = 1;
        PermRegisterMenus.Opacity = 1;
        PermWriteOutput.Opacity   = 1;

        // Hash status
        if (string.IsNullOrEmpty(meta.AssemblySha256))
        {
            TxtHashLabel.Text  = "No SHA-256 in manifest — integrity cannot be verified.";
            TxtHashBadge.Text  = "⚠ Unverified";
            TxtHashBadge.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xBB, 0x33));
        }
        else
        {
            TxtHashLabel.Text  = $"SHA-256: {meta.AssemblySha256[..16]}…";
            TxtHashBadge.Text  = "Will verify on install";
            TxtHashBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x68, 0xC5, 0xFF));
        }
    }

    // -- Install -----------------------------------------------------------

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_meta is null) return;

        BtnInstall.IsEnabled = false;
        Progress.Visibility  = Visibility.Visible;
        Progress.IsIndeterminate = true;
        ResultPanel.Visibility   = Visibility.Collapsed;

        _cts = new CancellationTokenSource();
        try
        {
            var targetDir = await _extractor.ExtractAsync(_packagePath, _cts.Token).ConfigureAwait(true);
            ShowSuccess($"Plugin installed successfully.\nLocation: {targetDir}");
            BtnClose.Content = "Close";
        }
        catch (OperationCanceledException)
        {
            ShowError("Installation cancelled.");
            BtnInstall.IsEnabled = true;
        }
        catch (PluginInstallException ex)
        {
            ShowError($"Installation failed:\n{ex.Message}");
            BtnInstall.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowError($"Unexpected error:\n{ex.Message}");
            BtnInstall.IsEnabled = true;
        }
        finally
        {
            Progress.Visibility = Visibility.Collapsed;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    // -- UI helpers --------------------------------------------------------

    private void ShowSuccess(string message)
    {
        ResultPanel.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x1F, 0x7F, 0x3F));
        TxtResult.Foreground   = new SolidColorBrush(Color.FromRgb(0x4C, 0xC4, 0x72));
        TxtResult.Text         = message;
        ResultPanel.Visibility = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        ResultPanel.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xAA, 0x22, 0x22));
        TxtResult.Foreground   = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66));
        TxtResult.Text         = message;
        ResultPanel.Visibility = Visibility.Visible;
    }
}
