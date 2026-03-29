//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.JsonEditor
// File: Controls/JsonEditorControl.cs
// Description:
//     Dedicated JSON/JSONC editor wrapping CodeEditorSplitHost with
//     JSON-specific toolbar commands: Format (pretty-print), Minify,
//     and real-time validation status.
// Architecture:
//     Composition — embeds CodeEditorSplitHost in a Grid with a
//     JSON toolbar row. All IDocumentEditor/IOpenableDocument calls
//     delegate to the inner host. JSON operations use System.Text.Json.
//////////////////////////////////////////////

using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.JsonEditor.Controls;

/// <summary>
/// JSON/JSONC editor with format, minify, and validation toolbar.
/// </summary>
public sealed class JsonEditorControl : Grid, IDocumentEditor, IOpenableDocument
{
    private readonly CodeEditorSplitHost _host;
    private readonly TextBlock _validationStatus;
    private string _filePath = string.Empty;

    public JsonEditorControl()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── Toolbar ──────────────────────────────────────────────────────────
        var toolbar = new WrapPanel { Margin = new Thickness(4, 2, 4, 2) };
        toolbar.Children.Add(MakeButton("{ }", "Format JSON (pretty-print)", OnFormatClick));
        toolbar.Children.Add(MakeButton("{}", "Minify JSON", OnMinifyClick));
        toolbar.Children.Add(new Separator { Margin = new Thickness(4, 0, 4, 0) });
        toolbar.Children.Add(MakeButton("\u2713", "Validate JSON", OnValidateClick));
        toolbar.Children.Add(new Separator { Margin = new Thickness(4, 0, 4, 0) });

        _validationStatus = new TextBlock
        {
            Text = "JSON",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 11,
        };
        toolbar.Children.Add(_validationStatus);

        SetRow(toolbar, 0);
        Children.Add(toolbar);

        // ── CodeEditor host ──────────────────────────────────────────────────
        _host = new CodeEditorSplitHost();
        SetRow(_host, 1);
        Children.Add(_host);
    }

    // ── IDocumentEditor — full delegation ────────────────────────────────────

    public bool   IsDirty    => _host.IsDirty;
    public bool   CanUndo    => _host.CanUndo;
    public bool   CanRedo    => _host.CanRedo;
    public bool   IsReadOnly { get => _host.IsReadOnly; set => _host.IsReadOnly = value; }
    // UndoCount/RedoCount use IDocumentEditor default implementations (return 0)
    public string Title      => _host.Title;
    public bool   IsBusy     => _host.IsBusy;

    public ICommand? UndoCommand      => _host.UndoCommand;
    public ICommand? RedoCommand      => _host.RedoCommand;
    public ICommand? SaveCommand      => _host.SaveCommand;
    public ICommand? CopyCommand      => _host.CopyCommand;
    public ICommand? CutCommand       => _host.CutCommand;
    public ICommand? PasteCommand     => _host.PasteCommand;
    public ICommand? DeleteCommand    => _host.DeleteCommand;
    public ICommand? SelectAllCommand => _host.SelectAllCommand;

    public void Undo()            => _host.Undo();
    public void Redo()            => _host.Redo();
    public void Save()            => _host.Save();
    public void Copy()            => _host.Copy();
    public void Cut()             => _host.Cut();
    public void Paste()           => _host.Paste();
    public void Delete()          => _host.Delete();
    public void SelectAll()       => _host.SelectAll();
    public void Close()           => _host.Close();
    public void CancelOperation() => _host.CancelOperation();

    public Task SaveAsync(CancellationToken ct = default)                    => _host.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => _host.SaveAsAsync(filePath, ct);

    public event EventHandler?         ModifiedChanged  { add => _host.ModifiedChanged  += value; remove => _host.ModifiedChanged  -= value; }
    public event EventHandler?         CanUndoChanged   { add => _host.CanUndoChanged   += value; remove => _host.CanUndoChanged   -= value; }
    public event EventHandler?         CanRedoChanged   { add => _host.CanRedoChanged   += value; remove => _host.CanRedoChanged   -= value; }
    public event EventHandler<string>? TitleChanged     { add => _host.TitleChanged     += value; remove => _host.TitleChanged     -= value; }
    public event EventHandler<string>? StatusMessage    { add => _host.StatusMessage    += value; remove => _host.StatusMessage    -= value; }
    public event EventHandler<string>? OutputMessage    { add => _host.OutputMessage    += value; remove => _host.OutputMessage    -= value; }
    public event EventHandler?         SelectionChanged { add => _host.SelectionChanged += value; remove => _host.SelectionChanged -= value; }

    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted   { add => _host.OperationStarted   += value; remove => _host.OperationStarted   -= value; }
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress  { add => _host.OperationProgress  += value; remove => _host.OperationProgress  -= value; }
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted { add => _host.OperationCompleted += value; remove => _host.OperationCompleted -= value; }

    // ── IOpenableDocument ────────────────────────────────────────────────────

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        _filePath = filePath;
        await ((IOpenableDocument)_host).OpenAsync(filePath, ct);
        ValidateJson();
    }

    // ── JSON Operations ──────────────────────────────────────────────────────

    private void OnFormatClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = _host.PrimaryEditor.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
            {
                doc.WriteTo(writer);
            }

            _host.PrimaryEditor.LoadText(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
            SetStatus("Valid JSON (formatted)", Brushes.Green);
        }
        catch (JsonException ex) { SetStatus($"Parse error: {ex.Message}", Brushes.OrangeRed); }
    }

    private void OnMinifyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = _host.PrimaryEditor.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
            {
                doc.WriteTo(writer);
            }

            _host.PrimaryEditor.LoadText(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
            SetStatus("Valid JSON (minified)", Brushes.Green);
        }
        catch (JsonException ex) { SetStatus($"Parse error: {ex.Message}", Brushes.OrangeRed); }
    }

    private void OnValidateClick(object sender, RoutedEventArgs e) => ValidateJson();

    private void ValidateJson()
    {
        try
        {
            var text = _host.PrimaryEditor.GetText();
            if (string.IsNullOrWhiteSpace(text)) { SetStatus("Empty", Brushes.Gray); return; }

            using var _ = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            SetStatus("Valid JSON", Brushes.Green);
        }
        catch (JsonException ex) { SetStatus($"Line {ex.LineNumber}: {ex.Message}", Brushes.OrangeRed); }
    }

    private void SetStatus(string text, Brush color)
    {
        _validationStatus.Text = text;
        _validationStatus.Foreground = color;
    }

    private static Button MakeButton(string content, string tooltip, RoutedEventHandler handler)
    {
        var btn = new Button
        {
            Content = content, ToolTip = tooltip,
            Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(2, 0, 2, 0),
            MinWidth = 28, FontSize = 12,
        };
        btn.Click += handler;
        return btn;
    }
}
