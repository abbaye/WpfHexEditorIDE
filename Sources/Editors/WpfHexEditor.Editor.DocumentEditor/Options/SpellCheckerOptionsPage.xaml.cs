// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Options/SpellCheckerOptionsPage.xaml.cs
// Description: Code-behind for SpellCheckerOptionsPage.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Editor.DocumentEditor.SpellCheck;

namespace WpfHexEditor.Editor.DocumentEditor.Options;

public sealed partial class SpellCheckerOptionsPage : UserControl
{
    private SpellCheckerOptionsViewModel? _vm;
    private DictionaryManager?            _dictManager;
    private HunspellSpellChecker?         _checker;

    public SpellCheckerOptionsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    internal void Initialize(
        SpellCheckerSettings  settings,
        DictionaryManager     dictManager,
        HunspellSpellChecker  checker)
    {
        _dictManager = dictManager;
        _checker     = checker;
        _vm          = new SpellCheckerOptionsViewModel(settings, dictManager);
        DataContext  = _vm;

        DictGrid.ItemsSource = _vm.Languages;
        ChkEnabled.IsChecked = _vm.IsEnabled;
        TxtUserDictPath.Text = _vm.UserDictPath;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { /* no-op: Initialize called externally */ }

    private void OnEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.IsEnabled = ChkEnabled.IsChecked == true;
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_dictManager is null || sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DictionaryRowViewModel row) return;

        row.SetInstalling(true);
        try
        {
            var progress = new Progress<double>(p => row.InstallProgress = p);
            await _dictManager.InstallFromUrlAsync(row.LanguageCode, progress);
            row.RefreshInstalled(true);
            await _checker!.LoadAsync(row.LanguageCode);
        }
        catch (Exception ex)
        {
            row.RefreshInstalled(false);
            MessageBox.Show(
                Window.GetWindow(this),
                (TryFindResource("SpellCheck_DownloadError") as string ?? "Download failed: {0}")
                    .Replace("{0}", ex.Message),
                (TryFindResource("SpellCheck_DownloadErrorTitle") as string ?? "Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (_dictManager is null || sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DictionaryRowViewModel row) return;

        var dlg = new OpenFileDialog
        {
            Title  = TryFindResource("SpellCheck_BrowseDicTitle") as string ?? "Select .dic file",
            Filter = "Hunspell dictionary (*.dic)|*.dic"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        var dicPath = dlg.FileName;
        var affPath = Path.ChangeExtension(dicPath, ".aff");
        if (!File.Exists(affPath))
        {
            var affDlg = new OpenFileDialog
            {
                Title  = TryFindResource("SpellCheck_BrowseAffTitle") as string ?? "Select .aff file",
                Filter = "Hunspell affix (*.aff)|*.aff",
                InitialDirectory = Path.GetDirectoryName(dicPath)
            };
            if (affDlg.ShowDialog(Window.GetWindow(this)) != true) return;
            affPath = affDlg.FileName;
        }

        try
        {
            _dictManager.InstallFromFile(dicPath, affPath, row.LanguageCode);
            row.RefreshInstalled(true);
            _ = _checker!.LoadAsync(row.LanguageCode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (_dictManager is null || sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DictionaryRowViewModel row) return;

        _dictManager.Remove(row.LanguageCode);
        row.RefreshInstalled(false);
    }

    private void OnEditUserDictClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var path = _vm.UserDictPath;
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
