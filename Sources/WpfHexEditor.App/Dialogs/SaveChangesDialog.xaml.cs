//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace WpfHexEditor.App.Dialogs;

public enum SaveChangesChoice { Save, DontSave, Cancel }

/// <summary>
/// VS-style "save before close" dialog.
/// Populate <see cref="DirtyItems"/> before showing; then read
/// <see cref="Choice"/> and <see cref="SelectedContentIds"/> after close.
/// </summary>
public sealed partial class SaveChangesDialog : Window
{
    private readonly List<FileEntry> _entries = [];
    private bool _updatingAll;

    // ── Input ─────────────────────────────────────────────────────────────

    /// <summary>
    /// List of (ContentId, display title) for each dirty document.
    /// </summary>
    public IReadOnlyList<(string ContentId, string Title)> DirtyItems
    {
        init
        {
            foreach (var (id, title) in value)
                _entries.Add(new FileEntry(id, title));
        }
    }

    // ── Output ────────────────────────────────────────────────────────────

    public SaveChangesChoice     Choice             { get; private set; } = SaveChangesChoice.Cancel;
    public IReadOnlyList<string> SelectedContentIds { get; private set; } = [];

    // ── Ctor ──────────────────────────────────────────────────────────────

    public SaveChangesDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HeaderText.Text = _entries.Count == 1
            ? $"Do you want to save changes to \"{_entries[0].Title}\"?"
            : "Do you want to save changes to the following files?";

        foreach (var entry in _entries)
            entry.PropertyChanged += OnEntryPropertyChanged;

        FileList.ItemsSource = _entries;

        if (_entries.Count > 1)
        {
            SelectAllCheck.Visibility = Visibility.Visible;
            UpdateMasterCheckState();
        }
    }

    // ── Master checkbox ────────────────────────────────────────────────────

    private void OnSelectAllChecked(object sender, RoutedEventArgs e)
    {
        if (_updatingAll) return;
        _updatingAll = true;
        foreach (var entry in _entries) entry.IsChecked = true;
        _updatingAll = false;
    }

    private void OnSelectAllUnchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingAll) return;
        _updatingAll = true;
        foreach (var entry in _entries) entry.IsChecked = false;
        _updatingAll = false;
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingAll || e.PropertyName != nameof(FileEntry.IsChecked)) return;
        UpdateMasterCheckState();
    }

    private void UpdateMasterCheckState()
    {
        int count = _entries.Count(x => x.IsChecked);
        SelectAllCheck.IsChecked = count == _entries.Count ? true
                                 : count == 0             ? false
                                 : null;
    }

    // ── Button handlers ────────────────────────────────────────────────────

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        Choice             = SaveChangesChoice.Save;
        SelectedContentIds = _entries.Where(x => x.IsChecked).Select(x => x.ContentId).ToList();
        DialogResult       = true;
    }

    private void OnDontSaveClicked(object sender, RoutedEventArgs e)
    {
        Choice       = SaveChangesChoice.DontSave;
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Choice       = SaveChangesChoice.Cancel;
        DialogResult = false;
    }

    // ── Entry model ───────────────────────────────────────────────────────

    private sealed class FileEntry(string contentId, string title) : INotifyPropertyChanged
    {
        private bool _isChecked = true;

        public string ContentId { get; } = contentId;
        public string Title     { get; } = title;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
