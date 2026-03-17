// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeEditorSplitHost.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Host container that wraps two CodeEditor instances sharing the same
//     CodeDocument. A toggle button lets the user split the view horizontally.
//     Implements IDocumentEditor by delegating to the last-focused editor
//     so the host (docking, menu) interacts transparently.
//     Implements IOpenableDocument by delegating to _primaryEditor.LoadFromFile
//     (both editors share the same CodeDocument, so the secondary view is
//     automatically updated).
//
// Architecture Notes:
//     Proxy / Delegate Pattern — IDocumentEditor forwarded to _activeEditor.
//     Composite — wraps two CodeEditor children sharing one CodeDocument.
//     Observer  — GotFocus on each CodeEditor updates _activeEditor reference.
// ==========================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// A split-view host for <see cref="CodeEditor"/>.
/// Both editors share the same <see cref="CodeDocument"/>; scroll positions
/// and caret positions are independent.
/// </summary>
public sealed class CodeEditorSplitHost : Grid, IDocumentEditor, IOpenableDocument
{
    #region Child controls

    private readonly CodeEditor    _primaryEditor;
    private readonly CodeEditor    _secondaryEditor;
    private readonly GridSplitter  _splitter;
    private readonly ToggleButton  _splitToggle;

    private readonly RowDefinition _primaryRow;
    private readonly RowDefinition _splitterRow;
    private readonly RowDefinition _secondaryRow;

    // The editor that most recently received focus — commands delegate to this one.
    private CodeEditor _activeEditor;

    #endregion

    #region Constructor

    public CodeEditorSplitHost()
    {
        // -- Row layout ------------------------------------------------------
        _primaryRow   = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
        _splitterRow  = new RowDefinition { Height = GridLength.Auto };
        _secondaryRow = new RowDefinition { Height = new GridLength(0) };  // collapsed initially

        RowDefinitions.Add(_primaryRow);
        RowDefinitions.Add(_splitterRow);
        RowDefinitions.Add(_secondaryRow);

        // -- Primary editor --------------------------------------------------
        _primaryEditor = new CodeEditor();
        SetRow(_primaryEditor, 0);
        Children.Add(_primaryEditor);

        // -- Splitter (hidden while not split) -------------------------------
        _splitter = new GridSplitter
        {
            Height            = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility        = Visibility.Collapsed,
            Background        = SystemColors.ControlDarkBrush
        };
        SetRow(_splitter, 1);
        Children.Add(_splitter);

        // -- Secondary editor (shares the same document) ---------------------
        _secondaryEditor = new CodeEditor();
        SetRow(_secondaryEditor, 2);
        Children.Add(_secondaryEditor);

        // -- Split toggle button (top-right overlay on primary editor) --------
        // Styled flat/borderless like VS2022 — transparent background, subtle
        // hover highlight using theme brushes, no 3-D default WPF button look.
        _splitToggle = new ToggleButton
        {
            Content             = "\uE8A5",  // Segoe MDL2 "SplitView" glyph
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 10,
            Width               = 16,
            Height              = 16,
            Padding             = new Thickness(0),
            VerticalAlignment   = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip             = "Split Editor",
            Style               = BuildFlatToggleButtonStyle(),
        };
        Panel.SetZIndex(_splitToggle, 99);
        SetRow(_splitToggle, 0);
        _splitToggle.Checked   += OnSplitToggleChecked;
        _splitToggle.Unchecked += OnSplitToggleUnchecked;
        Children.Add(_splitToggle);

        // -- Initialise active editor and wire focus tracking -----------------
        _activeEditor = _primaryEditor;

        _primaryEditor.GotFocus   += (_, _) => _activeEditor = _primaryEditor;
        _secondaryEditor.GotFocus += (_, _) => _activeEditor = _secondaryEditor;

        // -- Forward events from primary editor (document is shared) -----------
        _primaryEditor.ModifiedChanged  += (s, e) => ModifiedChanged?.Invoke(this, e);
        _primaryEditor.CanUndoChanged   += (s, e) => CanUndoChanged?.Invoke(this, e);
        _primaryEditor.CanRedoChanged   += (s, e) => CanRedoChanged?.Invoke(this, e);
        _primaryEditor.TitleChanged     += (s, e) => TitleChanged?.Invoke(this, e);
        _primaryEditor.StatusMessage    += (s, e) => StatusMessage?.Invoke(this, e);
        _primaryEditor.OutputMessage    += (s, e) => OutputMessage?.Invoke(this, e);
        _primaryEditor.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);
        _primaryEditor.OperationStarted   += (s, e) => OperationStarted?.Invoke(this, e);
        _primaryEditor.OperationProgress  += (s, e) => OperationProgress?.Invoke(this, e);
        _primaryEditor.OperationCompleted += (s, e) => OperationCompleted?.Invoke(this, e);

        // Connect the secondary editor to the same document after primary is loaded.
        Loaded += OnHostLoaded;
    }

    #endregion

    #region Public API — document access

    /// <summary>The primary (top) code editor instance.</summary>
    public CodeEditor PrimaryEditor => _primaryEditor;

    /// <summary>The secondary (bottom) code editor instance — only visible when split.</summary>
    public CodeEditor SecondaryEditor => _secondaryEditor;

    /// <summary>Whether the split view is currently active.</summary>
    public bool IsSplit => _splitToggle.IsChecked == true;

    /// <summary>
    /// Programmatically toggles the split view.
    /// </summary>
    public void ToggleSplit() => _splitToggle.IsChecked = !_splitToggle.IsChecked;

    #endregion

    #region Split button style

    /// <summary>
    /// Builds a flat, borderless VS2022-like ToggleButton style.
    /// Uses dynamic resource references so the button respects the active theme.
    /// States: Normal=transparent | Hover=CE_Selection@30% | Checked=CE_Selection@60%
    /// </summary>
    private static Style BuildFlatToggleButtonStyle()
    {
        // Reusable transparent + 1px transparent border template
        var template = new ControlTemplate(typeof(ToggleButton));

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
        border.AppendChild(content);

        template.VisualTree = border;

        // Hover trigger — light theme-aware fill
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(50, 100, 100, 100)),
            "border"));
        hoverTrigger.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(80, 150, 150, 150)),
            "border"));
        template.Triggers.Add(hoverTrigger);

        // Checked trigger — more prominent fill
        var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(
            Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(90, 100, 140, 200)),
            "border"));
        checkedTrigger.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(120, 100, 150, 220)),
            "border"));
        template.Triggers.Add(checkedTrigger);

        // Name the border so ControlTemplate Trigger Setters can target it by name.
        border.Name = "border";

        var style = new Style(typeof(ToggleButton));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180))));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        style.Seal();
        return style;
    }

    #endregion

    #region Split toggle handlers

    private void OnSplitToggleChecked(object sender, RoutedEventArgs e)
    {
        // Expand the secondary row to half the current height.
        double currentHeight = _primaryRow.ActualHeight;
        double half = Math.Max(50, currentHeight / 2);
        _primaryRow.Height   = new GridLength(half, GridUnitType.Star);
        _secondaryRow.Height = new GridLength(half, GridUnitType.Star);
        _splitter.Visibility = Visibility.Visible;
        _secondaryEditor.Focus();
    }

    private void OnSplitToggleUnchecked(object sender, RoutedEventArgs e)
    {
        _secondaryRow.Height = new GridLength(0);
        _splitter.Visibility = Visibility.Collapsed;
        _primaryEditor.Focus();
    }

    #endregion

    #region Loaded — share document between editors

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        // Share the primary editor's document with the secondary editor.
        // Both will render and edit the same CodeDocument instance.
        var doc = _primaryEditor.Document;
        if (doc != null)
            _secondaryEditor.SetDocument(doc);
    }

    #endregion

    #region IOpenableDocument — delegates to primary editor

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        // Lazy highlighter resolution before loading so the first render is already coloured.
        if (_primaryEditor.ExternalHighlighter is null)
        {
            var language = LanguageRegistry.Instance.GetLanguageForFile(filePath);
            if (language is not null)
            {
                var highlighter = CodeEditorFactory.BuildHighlighter(language);
                _primaryEditor.ExternalHighlighter   = highlighter;
                _secondaryEditor.ExternalHighlighter = highlighter;
            }
        }

        // Delegate to the primary editor's async open (file I/O runs off the UI thread).
        // Both editors share the same CodeDocument, so the secondary view updates automatically.
        await ((IOpenableDocument)_primaryEditor).OpenAsync(filePath, ct);
    }

    #endregion

    #region IDocumentEditor — proxy to _activeEditor

    // Helper: cast to the interface so explicit implementations are accessible.
    private IDocumentEditor Active => _activeEditor;

    public bool     IsDirty    => Active.IsDirty;
    public bool     CanUndo    => Active.CanUndo;
    public bool     CanRedo    => Active.CanRedo;
    public bool     IsReadOnly { get => Active.IsReadOnly; set { Active.IsReadOnly = value; ((IDocumentEditor)_secondaryEditor).IsReadOnly = value; } }
    public string   Title      => Active.Title;
    public bool     IsBusy     => Active.IsBusy;

    public ICommand UndoCommand      => Active.UndoCommand;
    public ICommand RedoCommand      => Active.RedoCommand;
    public ICommand SaveCommand      => Active.SaveCommand;
    public ICommand CopyCommand      => Active.CopyCommand;
    public ICommand CutCommand       => Active.CutCommand;
    public ICommand PasteCommand     => Active.PasteCommand;
    public ICommand DeleteCommand    => Active.DeleteCommand;
    public ICommand SelectAllCommand => Active.SelectAllCommand;

    public void Undo()        => Active.Undo();
    public void Redo()        => Active.Redo();
    public void Save()        => Active.Save();
    public Task SaveAsync(CancellationToken ct = default)                     => Active.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default)  => Active.SaveAsAsync(filePath, ct);
    public void Copy()        => Active.Copy();
    public void Cut()         => Active.Cut();
    public void Paste()       => Active.Paste();
    public void Delete()      => Active.Delete();
    public void SelectAll()   => Active.SelectAll();
    public void Close()       { ((IDocumentEditor)_primaryEditor).Close(); ((IDocumentEditor)_secondaryEditor).Close(); }
    public void CancelOperation() => Active.CancelOperation();

    public event EventHandler?         ModifiedChanged;
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    #endregion
}
