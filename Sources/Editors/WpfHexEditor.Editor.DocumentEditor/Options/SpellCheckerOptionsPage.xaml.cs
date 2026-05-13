// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Options/SpellCheckerOptionsPage.xaml.cs
// Description: Code-behind for SpellCheckerOptionsPage.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Core.SpellCheck;
using WpfHexEditor.Editor.Core.Dialogs;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Editor.DocumentEditor.Options;

public sealed partial class SpellCheckerOptionsPage : UserControl, WpfHexEditor.Core.Options.IOptionsPage
{
    private SpellCheckerOptionsViewModel? _vm;
    private DictionaryManager?            _dictManager;
    private HunspellSpellChecker?         _checker;
    private IDocumentHostService?         _documentHost;

    public SpellCheckerOptionsPage()
    {
        InitializeComponent();
    }

    public event EventHandler? Changed;
    public void Load(WpfHexEditor.Core.Options.AppSettings settings) { }
    public void Flush(WpfHexEditor.Core.Options.AppSettings settings) { }

    internal void Initialize(
        SpellCheckerSettings  settings,
        DictionaryManager     dictManager,
        HunspellSpellChecker  checker,
        IDocumentHostService? documentHost = null)
    {
        _dictManager  = dictManager;
        _checker      = checker;
        _documentHost = documentHost;
        _vm           = new SpellCheckerOptionsViewModel(settings, dictManager);
        DataContext   = _vm;

        DictGrid.ItemsSource          = _vm.Languages;
        LstIgnoredWords.ItemsSource   = _vm.IgnoredWords;
        ChkEnabled.IsChecked          = _vm.IsEnabled;
        ChkMultiLanguage.IsChecked    = _vm.MultiLanguageMode;
        TxtUserDictPath.Text          = _vm.UserDictPath;
        TxtMirrorUrl.Text             = _vm.MirrorUrl;
        TxtDictPath.Text              = _vm.DictionariesPath;

        SldrDebounce.Value        = _vm.AnalysisDebounceMs;
        SldrMaxSuggestions.Value  = _vm.MaxSuggestions;
        SldrConfidence.Value      = _vm.DetectionConfidencePercent;
        UpdateDebounceLabel();
        UpdateMaxSuggestionsLabel();
        UpdateConfidenceLabel();
    }

    private void OnEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.IsEnabled = ChkEnabled.IsChecked == true;
    }

    private void OnMultiLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.MultiLanguageMode = ChkMultiLanguage.IsChecked == true;
    }

    private void OnDebounceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_vm is null) return;
        _vm.AnalysisDebounceMs = (int)SldrDebounce.Value;
        UpdateDebounceLabel();
    }

    private void OnMaxSuggestionsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_vm is null) return;
        _vm.MaxSuggestions = (int)SldrMaxSuggestions.Value;
        UpdateMaxSuggestionsLabel();
    }

    private void OnConfidenceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_vm is null) return;
        _vm.DetectionConfidencePercent = (int)SldrConfidence.Value;
        UpdateConfidenceLabel();
    }

    private void OnMirrorUrlChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.MirrorUrl = TxtMirrorUrl.Text.Trim();
    }

    private void OnDictPathChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.DictionariesPath = TxtDictPath.Text.Trim();
        TxtUserDictPath.Text = _vm.UserDictPath;
    }

    private void OnBrowseDictPathClick(object sender, RoutedEventArgs e)
    {
        // Use a dummy OpenFileDialog pointed at a placeholder file so the user
        // can navigate to the desired folder without requiring WinForms.
        var dlg = new OpenFileDialog
        {
            Title            = TryFindResource("SpellCheck_BrowseDictPathTitle") as string ?? "Select dictionaries folder",
            Filter           = "Folder|*.nofile",
            FileName         = "Select Folder",
            CheckFileExists  = false,
            CheckPathExists  = true,
            ValidateNames    = false,
            InitialDirectory = Directory.Exists(TxtDictPath.Text) ? TxtDictPath.Text : string.Empty
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        var folder = Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrEmpty(folder)) return;
        TxtDictPath.Text = folder;
        OnDictPathChanged(sender, e);
    }

    private void OnRemoveIgnoredWordClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || LstIgnoredWords.SelectedItem is not string word) return;
        _vm.RemoveIgnoredWord(word);
    }

    private void OnResetAdvancedClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.ResetAdvancedToDefaults();
        ChkMultiLanguage.IsChecked = _vm.MultiLanguageMode;
        SldrDebounce.Value         = _vm.AnalysisDebounceMs;
        SldrMaxSuggestions.Value   = _vm.MaxSuggestions;
        SldrConfidence.Value       = _vm.DetectionConfidencePercent;
        TxtMirrorUrl.Text          = _vm.MirrorUrl;
        TxtDictPath.Text           = _vm.DictionariesPath;
        UpdateDebounceLabel();
        UpdateMaxSuggestionsLabel();
        UpdateConfidenceLabel();
    }

    private void UpdateDebounceLabel() =>
        TxtDebounceValue.Text = $"{_vm?.AnalysisDebounceMs ?? 800} ms";

    private void UpdateMaxSuggestionsLabel() =>
        TxtMaxSuggestionsValue.Text = $"{_vm?.MaxSuggestions ?? 5}";

    private void UpdateConfidenceLabel() =>
        TxtConfidenceValue.Text = $"{_vm?.DetectionConfidencePercent ?? 4} %";

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
            IdeMessageBox.Show(
                (TryFindResource("SpellCheck_DownloadError") as string ?? "Download failed: {0}")
                    .Replace("{0}", ex.Message),
                TryFindResource("SpellCheck_DownloadErrorTitle") as string ?? "Dictionary download error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                Window.GetWindow(this));
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
                Title            = TryFindResource("SpellCheck_BrowseAffTitle") as string ?? "Select .aff file",
                Filter           = "Hunspell affix (*.aff)|*.aff",
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
            IdeMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
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

            // Open in IDE text editor if available; fall back to shell open (Notepad etc.)
            if (_documentHost is not null)
                _documentHost.OpenDocument(path, preferredEditorId: "text-editor");
            else
            {
                var psi = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
        }
    }
}
