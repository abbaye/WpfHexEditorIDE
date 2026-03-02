//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Docking.Wpf.Dialogs;

/// <summary>
/// Live-preview options dialog for the document tab bar.
/// Changes are applied immediately to the shared <see cref="DocumentTabBarSettings"/> instance.
/// </summary>
public partial class TabSettingsDialog : Window, INotifyPropertyChanged
{
    // ─── Dependencies ─────────────────────────────────────────────────────────

    private DocumentTabBarSettings? _settings;

    /// <summary>
    /// The shared settings object this dialog reads from and writes to.
    /// Must be set before calling <see cref="ShowDialog"/>.
    /// </summary>
    public DocumentTabBarSettings? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            SyncFromSettings();
        }
    }

    // ─── Constructor ─────────────────────────────────────────────────────────

    public TabSettingsDialog()
    {
        InitializeComponent();
        RegexGrid.ItemsSource = _regexVmList;
    }

    // ─── Placement ────────────────────────────────────────────────────────────

    public bool PlacementTop
    {
        get => _settings?.TabPlacement == DocumentTabPlacement.Top;
        set { if (value && _settings is not null) _settings.TabPlacement = DocumentTabPlacement.Top; }
    }

    public bool PlacementLeft
    {
        get => _settings?.TabPlacement == DocumentTabPlacement.Left;
        set { if (value && _settings is not null) _settings.TabPlacement = DocumentTabPlacement.Left; }
    }

    public bool PlacementRight
    {
        get => _settings?.TabPlacement == DocumentTabPlacement.Right;
        set { if (value && _settings is not null) _settings.TabPlacement = DocumentTabPlacement.Right; }
    }

    // ─── Color mode ───────────────────────────────────────────────────────────

    public bool ColorNone
    {
        get => _settings?.ColorMode == DocumentTabColorMode.None;
        set { if (value && _settings is not null) { _settings.ColorMode = DocumentTabColorMode.None; OnColorModeChanged(); } }
    }

    public bool ColorByExtension
    {
        get => _settings?.ColorMode == DocumentTabColorMode.FileExtension;
        set { if (value && _settings is not null) { _settings.ColorMode = DocumentTabColorMode.FileExtension; OnColorModeChanged(); } }
    }

    public bool ColorByProject
    {
        get => _settings?.ColorMode == DocumentTabColorMode.Project;
        set { if (value && _settings is not null) { _settings.ColorMode = DocumentTabColorMode.Project; OnColorModeChanged(); } }
    }

    public bool ColorByRegex
    {
        get => _settings?.ColorMode == DocumentTabColorMode.Regex;
        set { if (value && _settings is not null) { _settings.ColorMode = DocumentTabColorMode.Regex; OnColorModeChanged(); } }
    }

    // ─── Multi-row ────────────────────────────────────────────────────────────

    public bool MultiRowTabs
    {
        get => _settings?.MultiRowTabs ?? false;
        set
        {
            if (_settings is not null)
            {
                _settings.MultiRowTabs = value;
                Notify();
                Notify(nameof(MultiRowWithMouseWheel));
            }
        }
    }

    public bool MultiRowWithMouseWheel
    {
        get => _settings?.MultiRowWithMouseWheel ?? true;
        set { if (_settings is not null) _settings.MultiRowWithMouseWheel = value; }
    }

    // ─── Regex rules VM list ──────────────────────────────────────────────────

    private readonly ObservableCollection<RegexColorRuleVm> _regexVmList = [];

    private void SyncFromSettings()
    {
        _regexVmList.Clear();

        if (_settings is null) return;

        foreach (var rule in _settings.RegexRules)
            _regexVmList.Add(new RegexColorRuleVm
            {
                Pattern  = rule.Pattern,
                ColorHex = rule.ColorHex
            });

        // Subscribe to VM list changes to keep Settings.RegexRules in sync
        _regexVmList.CollectionChanged += (_, _) => SyncRegexRulesToSettings();

        NotifyAll();
    }

    private void SyncRegexRulesToSettings()
    {
        if (_settings is null) return;

        _settings.RegexRules.Clear();
        foreach (var vm in _regexVmList)
            _settings.RegexRules.Add(new RegexColorRule { Pattern = vm.Pattern, ColorHex = vm.ColorHex });
    }

    private void OnColorModeChanged()
    {
        NotifyAll();
        SyncRegexRulesToSettings();
    }

    // ─── Event handlers ───────────────────────────────────────────────────────

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // ─── INotifyPropertyChanged ───────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void NotifyAll()
    {
        Notify(nameof(PlacementTop));
        Notify(nameof(PlacementLeft));
        Notify(nameof(PlacementRight));
        Notify(nameof(ColorNone));
        Notify(nameof(ColorByExtension));
        Notify(nameof(ColorByProject));
        Notify(nameof(ColorByRegex));
        Notify(nameof(MultiRowTabs));
        Notify(nameof(MultiRowWithMouseWheel));
    }

    // ─── Inner VM for regex rules DataGrid ───────────────────────────────────

    /// <summary>
    /// Lightweight view-model for DataGrid editing of regex color rules.
    /// Exposes the color as a hex string and a preview <see cref="Brush"/>.
    /// </summary>
    public class RegexColorRuleVm : INotifyPropertyChanged
    {
        private string _pattern = "";
        private string _colorHex = "#FF6495ED";

        public string Pattern
        {
            get => _pattern;
            set { _pattern = value; Notify(); }
        }

        public string ColorHex
        {
            get => _colorHex;
            set
            {
                _colorHex = value;
                Notify();
                Notify(nameof(ColorBrush));
            }
        }

        public Brush ColorBrush
        {
            get
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex)); }
                catch { return Brushes.CornflowerBlue; }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
