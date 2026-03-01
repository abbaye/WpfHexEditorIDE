//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
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

    // ── Input ─────────────────────────────────────────────────────────────

    /// <summary>List of (ContentId, display title) for each dirty document.</summary>
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
        HeaderText.Text    = _entries.Count == 1
            ? $"Voulez-vous enregistrer les modifications apportées à \"{_entries[0].Title}\" ?"
            : "Voulez-vous enregistrer les modifications apportées aux fichiers suivants ?";
        FileList.ItemsSource = _entries;
    }

    // ── Handlers ──────────────────────────────────────────────────────────

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

    private sealed class FileEntry(string contentId, string title)
    {
        public string ContentId { get; } = contentId;
        public string Title     { get; } = title;
        public bool   IsChecked { get; set; } = true;
    }
}
