// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeEditorNavigationBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     VS2022-style navigation bar shown at the top of the CodeEditor.
//     Contains three ComboBoxes (Namespaces / Types / Members) plus the
//     split-view toggle button docked to the far right.
//     Parses the document on a background thread (500 ms debounce) and
//     updates the combo selections as the caret moves.
//
// Architecture Notes:
//     Observer — subscribes to CodeEditor.CaretMoved and Document.TextChanged.
//     Strategy — CodeStructureParser is stateless; this class owns the snapshot.
//     WPF Theme — all brushes resolved via SetResourceReference (CE_NavBarBg,
//                 CE_NavBarBorder, CE_Foreground, CE_Background tokens).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Models;
using CE = WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor;
using ModelTextChangedEventArgs = WpfHexEditor.Editor.CodeEditor.Models.TextChangedEventArgs;

namespace WpfHexEditor.Editor.CodeEditor.NavigationBar;

/// <summary>
/// VS2022-style navigation bar: [Namespace ▾] [Type ▾] [Member ▾]  [⊟ split]
/// </summary>
public sealed class CodeEditorNavigationBar : Grid
{
    // ── Child controls ────────────────────────────────────────────────────────
    private readonly ComboBox     _nsCombo;
    private readonly ComboBox     _typeCombo;
    private readonly ComboBox     _memberCombo;
    private readonly Border       _bottomBorder;

    // ── State ─────────────────────────────────────────────────────────────────
    private CE?              _editor;
    private CodeStructureSnapshot    _snapshot   = CodeStructureSnapshot.Empty;
    private CancellationTokenSource? _parseCts;
    private bool                     _updating;   // suppresses combo event re-entrancy

    // ── Constructor ───────────────────────────────────────────────────────────

    public CodeEditorNavigationBar()
    {
        Height = 22;

        // Four columns: Namespace(1*) | Type(1*) | Member(1*) | SplitToggle(Auto)
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SetResourceReference(BackgroundProperty, "CE_NavBarBg");

        _nsCombo     = BuildCombo("(global namespace)"); SetColumn(_nsCombo,     0);
        _typeCombo   = BuildCombo("(no type)");          SetColumn(_typeCombo,   1);
        _memberCombo = BuildCombo("(no member)");        SetColumn(_memberCombo, 2);

        _nsCombo    .SelectionChanged += OnNsSelected;
        _typeCombo  .SelectionChanged += OnTypeSelected;
        _memberCombo.SelectionChanged += OnMemberSelected;

        Children.Add(_nsCombo);
        Children.Add(_typeCombo);
        Children.Add(_memberCombo);

        // Bottom border — 1 px separator between nav bar and editor content
        _bottomBorder = new Border
        {
            Height              = 1,
            VerticalAlignment   = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _bottomBorder.SetResourceReference(Border.BackgroundProperty, "CE_NavBarBorder");
        SetColumnSpan(_bottomBorder, 4);
        Children.Add(_bottomBorder);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches the navigation bar to a <see cref="CodeEditor"/> instance.
    /// Call once after construction.
    /// </summary>
    public void Attach(CE editor)
    {
        _editor = editor;
        editor.CaretMoved += OnCaretMoved;
        // Document is not yet loaded at construction; re-subscribe in Loaded.
        editor.Loaded += OnEditorLoaded;
    }

    /// <summary>
    /// Places <paramref name="splitToggle"/> in column 3 (far right of the bar).
    /// Call from <see cref="Controls.CodeEditorSplitHost"/> to relocate the button.
    /// </summary>
    public void AddSplitToggle(ToggleButton splitToggle)
    {
        splitToggle.VerticalAlignment = VerticalAlignment.Center;
        splitToggle.Margin            = new Thickness(2, 0, 4, 0);
        SetColumn(splitToggle, 3);
        Children.Add(splitToggle);
    }

    // ── Editor lifecycle ──────────────────────────────────────────────────────

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeToDocument(_editor?.Document);
        ScheduleParse();
    }

    private void SubscribeToDocument(CodeDocument? doc)
    {
        if (doc == null) return;
        doc.TextChanged += OnDocumentTextChanged;
    }

    private void OnDocumentTextChanged(object? sender, ModelTextChangedEventArgs e)
        => ScheduleParse();

    // ── Parse pipeline (debounced 500 ms, background thread) ──────────────────

    private void ScheduleParse()
    {
        _parseCts?.Cancel();
        _parseCts = new CancellationTokenSource();
        var ct    = _parseCts.Token;
        var lines = _editor?.Document?.Lines;
        if (lines == null) return;

        // Snapshot on the UI thread before handing off — ObservableCollection is not thread-safe.
        var linesCopy = lines.ToArray();

        Task.Delay(500, ct).ContinueWith(_ =>
        {
            if (ct.IsCancellationRequested) return;
            var snapshot = CodeStructureParser.Parse(linesCopy);
            Dispatcher.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;
                _snapshot = snapshot;
                RepopulateAll();
                RefreshSelections(_editor?.CursorLine ?? 0);
            });
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    // ── Combo population ──────────────────────────────────────────────────────

    private void RepopulateAll()
    {
        _updating = true;
        try
        {
            _nsCombo    .ItemsSource = _snapshot.Namespaces;
            _typeCombo  .ItemsSource = _snapshot.Types;
            _memberCombo.ItemsSource = _snapshot.Members;
        }
        finally { _updating = false; }
    }

    // ── Caret tracking ────────────────────────────────────────────────────────

    private void OnCaretMoved(object? sender, EventArgs e)
        => RefreshSelections(_editor?.CursorLine ?? 0);

    private void RefreshSelections(int caretLine)
    {
        _updating = true;
        try
        {
            // Left combo — last namespace whose line ≤ caretLine
            _nsCombo.SelectedItem = LastBefore(_snapshot.Namespaces, caretLine);

            // Middle combo — last type whose line ≤ caretLine
            var currentType = LastBefore(_snapshot.Types, caretLine);
            _typeCombo.SelectedItem = currentType;

            // Right combo — last member whose line ≤ caretLine
            _memberCombo.SelectedItem = LastBefore(_snapshot.Members, caretLine);
        }
        finally { _updating = false; }
    }

    private static NavigationBarItem? LastBefore(IReadOnlyList<NavigationBarItem> items, int line)
    {
        NavigationBarItem? result = null;
        foreach (var item in items)
        {
            if (item.Line <= line) result = item;
            else break;
        }
        return result;
    }

    // ── Combo selection → navigate ────────────────────────────────────────────

    private void OnNsSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _editor == null) return;
        if (_nsCombo.SelectedItem is NavigationBarItem item)
            _editor.NavigateToLine(item.Line);
    }

    private void OnTypeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _editor == null) return;
        if (_typeCombo.SelectedItem is NavigationBarItem item)
            _editor.NavigateToLine(item.Line);
    }

    private void OnMemberSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _editor == null) return;
        if (_memberCombo.SelectedItem is NavigationBarItem item)
            _editor.NavigateToLine(item.Line);
    }

    // ── ComboBox factory ──────────────────────────────────────────────────────

    private static ComboBox BuildCombo(string placeholder)
    {
        var cb = new ComboBox
        {
            Margin              = new Thickness(0, 1, 1, 1),
            VerticalAlignment   = VerticalAlignment.Stretch,
            FontSize            = 11,
            BorderThickness     = new Thickness(0),
            IsEditable          = false,
            ItemTemplate        = BuildItemTemplate(),
        };
        cb.SetResourceReference(Control.BackgroundProperty,  "CE_NavBarBg");
        cb.SetResourceReference(Control.ForegroundProperty,  "CE_Foreground");
        cb.SetResourceReference(Control.BorderBrushProperty, "CE_NavBarBorder");
        return cb;
    }

    /// <summary>
    /// Builds a DataTemplate that renders a NavigationBarItem as:
    ///   [IconGlyph TextBlock]  [Name TextBlock]
    /// Icon colour comes from NavigationBarItem.IconBrush (VS2022 palette).
    /// </summary>
    private static DataTemplate BuildItemTemplate()
    {
        // Root: horizontal StackPanel
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panel.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

        // Icon glyph — width 18, centred, bound to IconGlyph + IconBrush
        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.WidthProperty,               18.0);
        icon.SetValue(TextBlock.TextAlignmentProperty,       TextAlignment.Center);
        icon.SetValue(TextBlock.FontWeightProperty,          FontWeights.Bold);
        icon.SetValue(TextBlock.FontSizeProperty,            10.0);
        icon.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
        icon.SetBinding(TextBlock.TextProperty,              new Binding(nameof(NavigationBarItem.IconGlyph)));
        icon.SetBinding(TextBlock.ForegroundProperty,        new Binding(nameof(NavigationBarItem.IconBrush)));

        // Member name — small left margin, inherits combo foreground
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetValue(TextBlock.MarginProperty,              new Thickness(2, 0, 0, 0));
        name.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
        name.SetBinding(TextBlock.TextProperty,              new Binding(nameof(NavigationBarItem.Name)));

        panel.AppendChild(icon);
        panel.AppendChild(name);

        return new DataTemplate { VisualTree = panel };
    }
}
