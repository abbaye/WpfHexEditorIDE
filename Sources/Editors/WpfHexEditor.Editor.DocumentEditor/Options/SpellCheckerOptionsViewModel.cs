// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Options/SpellCheckerOptionsViewModel.cs
// Description:
//     ViewModel for SpellCheckerOptionsPage.
//     Exposes the list of known languages with install status,
//     active language selection, and install/remove commands.
// ==========================================================

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.SpellCheck;

namespace WpfHexEditor.Editor.DocumentEditor.Options;

internal sealed class SpellCheckerOptionsViewModel : INotifyPropertyChanged
{
    private readonly SpellCheckerSettings _settings;
    private readonly DictionaryManager    _dictManager;

    public ObservableCollection<DictionaryRowViewModel> Languages { get; } = [];

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); _settings.IsEnabled = value; _settings.Save(); }
    }

    private bool _multiLanguageMode;
    public bool MultiLanguageMode
    {
        get => _multiLanguageMode;
        set { if (_multiLanguageMode == value) return; _multiLanguageMode = value; OnPropertyChanged(); _settings.MultiLanguageMode = value; _settings.Save(); }
    }

    private int _analysisDebounceMs;
    public int AnalysisDebounceMs
    {
        get => _analysisDebounceMs;
        set { if (_analysisDebounceMs == value) return; _analysisDebounceMs = Math.Clamp(value, 100, 5000); OnPropertyChanged(); _settings.AnalysisDebounceMs = _analysisDebounceMs; _settings.Save(); }
    }

    private int _maxSuggestions;
    public int MaxSuggestions
    {
        get => _maxSuggestions;
        set { if (_maxSuggestions == value) return; _maxSuggestions = Math.Clamp(value, 1, 20); OnPropertyChanged(); _settings.MaxSuggestions = _maxSuggestions; _settings.Save(); }
    }

    private int _detectionConfidencePercent;
    public int DetectionConfidencePercent
    {
        get => _detectionConfidencePercent;
        set { if (_detectionConfidencePercent == value) return; _detectionConfidencePercent = Math.Clamp(value, 1, 50); OnPropertyChanged(); _settings.DetectionConfidencePercent = _detectionConfidencePercent; _settings.Save(); }
    }

    private string _mirrorUrl = string.Empty;
    public string MirrorUrl
    {
        get => _mirrorUrl;
        set { if (_mirrorUrl == value) return; _mirrorUrl = value; OnPropertyChanged(); _settings.MirrorUrl = value; _settings.Save(); }
    }

    private string _dictionariesPath = string.Empty;
    public string DictionariesPath
    {
        get => _dictionariesPath;
        set { if (_dictionariesPath == value) return; _dictionariesPath = value; OnPropertyChanged(); _settings.DictionariesPath = value; _settings.Save(); }
    }

    public ObservableCollection<string> IgnoredWords { get; } = [];

    public string UserDictPath => System.IO.Path.Combine(_settings.DictionariesPath, "userdict.txt");

    public SpellCheckerOptionsViewModel(SpellCheckerSettings settings, DictionaryManager dictManager)
    {
        _settings                   = settings;
        _dictManager                = dictManager;
        _isEnabled                  = settings.IsEnabled;
        _multiLanguageMode          = settings.MultiLanguageMode;
        _analysisDebounceMs         = settings.AnalysisDebounceMs;
        _maxSuggestions             = settings.MaxSuggestions;
        _detectionConfidencePercent = settings.DetectionConfidencePercent;
        _mirrorUrl                  = settings.MirrorUrl;
        _dictionariesPath           = settings.DictionariesPath;
        foreach (var w in settings.IgnoredWords)
            IgnoredWords.Add(w);
        IgnoredWords.CollectionChanged += (_, _) =>
        {
            _settings.IgnoredWords = [.. IgnoredWords];
            _settings.Save();
        };
        Reload();
    }

    public void Reload()
    {
        foreach (var row in Languages)
            row.PropertyChanged -= OnRowPropertyChanged;
        Languages.Clear();
        var all = _dictManager.GetAllLanguages();
        foreach (var info in all)
            Languages.Add(new DictionaryRowViewModel(info, _settings.ActiveLanguage == info.LanguageCode));
        foreach (var row in Languages)
            row.PropertyChanged += OnRowPropertyChanged;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DictionaryRowViewModel.IsActive)) return;
        if (sender is not DictionaryRowViewModel row || !row.IsActive) return;
        // Deactivate others
        foreach (var r in Languages)
            if (r != row) r.SilentSetActive(false);
        _settings.ActiveLanguage = row.LanguageCode;
        _settings.Save();
    }

    public void RemoveIgnoredWord(string word) => IgnoredWords.Remove(word);

    public void ResetAdvancedToDefaults()
    {
        var def = new SpellCheckerSettings();
        MultiLanguageMode          = def.MultiLanguageMode;
        AnalysisDebounceMs         = def.AnalysisDebounceMs;
        MaxSuggestions             = def.MaxSuggestions;
        DetectionConfidencePercent = def.DetectionConfidencePercent;
        MirrorUrl                  = def.MirrorUrl;
        DictionariesPath           = def.DictionariesPath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class DictionaryRowViewModel : INotifyPropertyChanged
{
    private static readonly Brush InstalledBrush   = new SolidColorBrush(Color.FromRgb(46, 160, 67));
    private static readonly Brush NotInstalledBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

    public string LanguageCode  { get; }
    public string DisplayName   { get; }

    private bool _isInstalled;
    public bool IsInstalled
    {
        get => _isInstalled;
        private set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(DownloadVisible)); OnPropertyChanged(nameof(RemoveVisible)); OnPropertyChanged(nameof(BrowseVisible)); }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); }
    }

    private double _installProgress = 0;
    public double InstallProgress
    {
        get => _installProgress;
        set { _installProgress = Math.Max(0, Math.Min(100, value * 100)); OnPropertyChanged(); }
    }

    private bool _isInstalling;
    public bool IsNotInstalling => !_isInstalling;
    public void SetInstalling(bool value)
    {
        _isInstalling = value;
        OnPropertyChanged(nameof(IsNotInstalling));
        OnPropertyChanged(nameof(ProgressVisible));
    }

    public string StatusText => IsInstalled
        ? Application.Current.TryFindResource("SpellCheck_StatusInstalled") as string ?? "Installed"
        : Application.Current.TryFindResource("SpellCheck_StatusNotInstalled") as string ?? "Not installed";

    public Brush StatusColor => IsInstalled ? InstalledBrush : NotInstalledBrush;

    public Visibility ProgressVisible => _isInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DownloadVisible => IsInstalled   ? Visibility.Collapsed : Visibility.Visible;
    public Visibility BrowseVisible   => Visibility.Visible;
    public Visibility RemoveVisible   => IsInstalled   ? Visibility.Visible : Visibility.Collapsed;

    public DictionaryRowViewModel(DictionaryInfo info, bool isActive)
    {
        LanguageCode  = info.LanguageCode;
        DisplayName   = info.DisplayName;
        _isInstalled  = info.IsInstalled;
        _isActive     = isActive && info.IsInstalled;
    }

    public void SilentSetActive(bool value)
    {
        _isActive = value;
        OnPropertyChanged(nameof(IsActive));
    }

    public void RefreshInstalled(bool installed)
    {
        IsInstalled = installed;
        SetInstalling(false);
        InstallProgress = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
