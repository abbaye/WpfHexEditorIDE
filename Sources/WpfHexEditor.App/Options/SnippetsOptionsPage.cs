//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : Options/SnippetsOptionsPage.cs
// Description  : Options page for viewing and editing user-defined code
//                snippets persisted via UserSnippetStore.
// Architecture : Code-behind-only UserControl implementing IOptionsPage.
//                Mirrors the KeyboardShortcutsPage pattern — no XAML file,
//                DataGrid + body editor TextBox + Add/Remove buttons.
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Editor.CodeEditor.Properties;
using WpfHexEditor.Editor.CodeEditor.Snippets;

namespace WpfHexEditor.App.Options;

/// <summary>IDE options page — user-defined Code Snippets.</summary>
public sealed class SnippetsOptionsPage : UserControl, IOptionsPage
{
    private readonly UserSnippetStore _store;
    private readonly DataGrid         _grid;
    private readonly TextBox          _bodyEditor;
    private readonly ObservableCollection<StoredSnippet> _rows = [];

    public event EventHandler? Changed;

    public SnippetsOptionsPage() : this(new UserSnippetStore()) { }

    public SnippetsOptionsPage(UserSnippetStore store)
    {
        _store = store;

        // -- Toolbar ---------------------------------------------------------
        var addBtn = new Button
        {
            Content = CodeEditorResources.Snippets_Page_Add,
            Margin  = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 3, 10, 3),
        };
        addBtn.Click += (_, _) => OnAdd();

        var removeBtn = new Button
        {
            Content = CodeEditorResources.Snippets_Page_Remove,
            Margin  = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 3, 10, 3),
        };
        removeBtn.Click += (_, _) => OnRemove();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        toolbar.Children.Add(addBtn);
        toolbar.Children.Add(removeBtn);

        // -- DataGrid --------------------------------------------------------
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            HeadersVisibility   = DataGridHeadersVisibility.Column,
            SelectionMode       = DataGridSelectionMode.Single,
            ItemsSource         = _rows,
            Height              = 220,
        };
        _grid.Columns.Add(MakeTextColumn(CodeEditorResources.Snippets_Page_ColLanguage,    nameof(StoredSnippet.LanguageId),  100));
        _grid.Columns.Add(MakeTextColumn(CodeEditorResources.Snippets_Page_ColTrigger,     nameof(StoredSnippet.Trigger),     120));
        _grid.Columns.Add(MakeTextColumn(CodeEditorResources.Snippets_Page_ColDescription, nameof(StoredSnippet.Description), 260));
        _grid.SelectionChanged += OnSelectionChanged;
        _grid.CellEditEnding   += (_, _) => Persist();

        // -- Body editor + variables hint ------------------------------------
        var bodyLabel = new TextBlock
        {
            Text        = CodeEditorResources.Snippets_Page_BodyLabel,
            Margin      = new Thickness(0, 8, 0, 4),
            FontWeight  = FontWeights.SemiBold,
        };
        _bodyEditor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab    = true,
            Height        = 140,
            TextWrapping  = TextWrapping.NoWrap,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _bodyEditor.SetResourceReference(TextBox.FontFamilyProperty, "CE_FontFamily");
        _bodyEditor.SetResourceReference(TextBox.FontSizeProperty,   "CE_FontSize");
        _bodyEditor.LostFocus += (_, _) => Persist();

        var varsHint = new TextBlock
        {
            Text         = CodeEditorResources.Snippets_Page_VariablesHint,
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.7,
            FontSize     = 11,
            Margin       = new Thickness(0, 4, 0, 0),
        };

        // -- Layout ---------------------------------------------------------
        var root = new StackPanel { Margin = new Thickness(8) };
        root.Children.Add(toolbar);
        root.Children.Add(_grid);
        root.Children.Add(bodyLabel);
        root.Children.Add(_bodyEditor);
        root.Children.Add(varsHint);

        Content = root;

        ReloadRows();
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────
    // Snippet data lives in its own JSON file (UserSnippetStore), not in
    // AppSettings — Load/Flush are no-ops; persistence happens inside Persist().

    public void Load(AppSettings settings) => ReloadRows();
    public void Flush(AppSettings settings) { /* nothing to push back into AppSettings */ }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DataGridTextColumn MakeTextColumn(string header, string binding, double width) => new()
    {
        Header  = header,
        Binding = new System.Windows.Data.Binding(binding) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
        Width   = width,
    };

    private void ReloadRows()
    {
        _rows.Clear();
        foreach (var s in _store.GetAll())
            _rows.Add(new StoredSnippet
            {
                LanguageId  = s.LanguageId,
                Trigger     = s.Trigger,
                Body        = s.Body,
                Description = s.Description,
            });
    }

    private void OnAdd()
    {
        var snippet = new StoredSnippet
        {
            LanguageId  = "*",
            Trigger     = "new",
            Description = CodeEditorResources.Snippets_Page_DefaultDescription,
            Body        = "$cursor",
        };
        _rows.Add(snippet);
        _grid.SelectedItem = snippet;
        _bodyEditor.Text   = snippet.Body;
        Persist();
    }

    private void OnRemove()
    {
        if (_grid.SelectedItem is not StoredSnippet selected) return;
        _rows.Remove(selected);
        _bodyEditor.Clear();
        Persist();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_grid.SelectedItem is StoredSnippet s)
            _bodyEditor.Text = s.Body;
    }

    private void Persist()
    {
        if (_grid.SelectedItem is StoredSnippet selected)
            selected.Body = _bodyEditor.Text ?? string.Empty;

        // Snapshot _rows up-front so concurrent UI edits during the (locked)
        // disk write don't bleed half-mutated state into the store.
        _store.ReplaceAll(_rows.ToList());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
