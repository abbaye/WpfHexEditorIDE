// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Controls/UndoHistoryPopup.cs
// Contributors: Claude (Anthropic)
// Description:
//     VS-style undo/redo history dropdown popup.
//     Hovering over an item highlights all steps up to that point.
//     Clicking performs a multi-level undo or redo.
// Architecture:
//     Pure code-behind WPF Popup — no XAML, no extra DLL deps.
//     Reuses ET_* theme tokens for colors.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>
/// A VS-style popup listing undo or redo history steps.
/// Multi-level selection: hovering highlights all steps up to the hovered row;
/// clicking invokes the <see cref="StepsRequested"/> callback.
/// </summary>
internal sealed class UndoHistoryPopup
{
    private readonly Popup _popup;
    private readonly ListBox _list;
    private readonly TextBlock _statusText;
    private bool _isUndo;

    /// <summary>
    /// Raised when the user clicks a history item.
    /// Parameter is the number of steps to undo/redo (1-based).
    /// </summary>
    public event Action<int>? StepsRequested;

    public UndoHistoryPopup()
    {
        _list = BuildList();
        _statusText = new TextBlock
        {
            Margin = new Thickness(4, 2, 4, 4),
            FontSize = 11,
            Foreground = TryFindBrush("ET_MetaForeground") ?? Brushes.Gray,
        };

        var root = new Border
        {
            Background = TryFindBrush("ET_PopupBackground") ?? new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
            BorderBrush = TryFindBrush("ET_PopupBorderBrush") ?? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = new Thickness(1),
            MinWidth = 200,
            MaxWidth = 360,
            Child = new StackPanel
            {
                Children = { _list, _statusText }
            }
        };

        _popup = new Popup
        {
            Child = root,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
        };

        _popup.Closed += (_, _) => _list.SelectedIndex = -1;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Show the popup anchored below <paramref name="anchor"/>.
    /// </summary>
    public void Show(UIElement anchor, IReadOnlyList<string> descriptions, bool isUndo)
    {
        _isUndo = isUndo;
        _list.Items.Clear();

        if (descriptions.Count == 0)
            return;

        for (int i = 0; i < descriptions.Count; i++)
        {
            var item = new ListBoxItem
            {
                Content = descriptions[i],
                Tag = i + 1,           // 1-based step count
                Padding = new Thickness(8, 3, 8, 3),
                Foreground = TryFindBrush("ET_HeaderForeground") ?? Brushes.WhiteSmoke,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
            };
            _list.Items.Add(item);
        }

        UpdateStatus(0);

        _popup.PlacementTarget = anchor;
        _popup.IsOpen = true;
        _list.Focus();
    }

    public bool IsOpen => _popup.IsOpen;

    public void Close() => _popup.IsOpen = false;

    // ── List Construction ───────────────────────────────────────────────────

    private ListBox BuildList()
    {
        var list = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = 280,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);

        list.MouseMove += OnMouseMove;
        list.MouseLeave += OnMouseLeave;
        list.MouseLeftButtonUp += OnMouseUp;
        list.PreviewKeyDown += OnKeyDown;

        return list;
    }

    // ── Interaction ─────────────────────────────────────────────────────────

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_list.Items.Count == 0) return;

        var hit = _list.InputHitTest(e.GetPosition(_list)) as DependencyObject;
        var item = FindAncestor<ListBoxItem>(hit);
        if (item == null) return;

        int hoveredIndex = _list.Items.IndexOf(item); // 0-based
        HighlightUpTo(hoveredIndex);
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        ClearHighlights();
        UpdateStatus(0);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        int selected = CountHighlighted();
        if (selected <= 0) return;

        _popup.IsOpen = false;
        StepsRequested?.Invoke(selected);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _popup.IsOpen = false; e.Handled = true; }
        if (e.Key == Key.Enter)
        {
            int selected = CountHighlighted();
            if (selected > 0)
            {
                _popup.IsOpen = false;
                StepsRequested?.Invoke(selected);
            }
            e.Handled = true;
        }
    }

    // ── Highlighting ────────────────────────────────────────────────────────

    private static readonly Brush HighlightBrush =
        new SolidColorBrush(Color.FromArgb(60, 0x56, 0x9C, 0xD6));

    private void HighlightUpTo(int index)
    {
        var highlight = TryFindBrush("SE_HoverBrush") ?? HighlightBrush;
        for (int i = 0; i < _list.Items.Count; i++)
        {
            if (_list.Items[i] is ListBoxItem li)
                li.Background = i <= index ? highlight : Brushes.Transparent;
        }
        UpdateStatus(index + 1);
    }

    private void ClearHighlights()
    {
        foreach (var item in _list.Items)
            if (item is ListBoxItem li) li.Background = Brushes.Transparent;
    }

    private int CountHighlighted()
    {
        int count = 0;
        foreach (var item in _list.Items)
            if (item is ListBoxItem li && li.Background != Brushes.Transparent) count++;
        return count;
    }

    private void UpdateStatus(int count)
    {
        string verb = _isUndo ? "Undo" : "Redo";
        _statusText.Text = count <= 0
            ? string.Empty
            : $"{verb} {count} action{(count == 1 ? "" : "s")}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Brush? TryFindBrush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush b) return b;
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
