// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramFilterBar.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Inline filter toolbar that appears inside DiagramCanvas on Ctrl+F.
//     Provides real-time text search with 150ms debounce, kind/visibility
//     chip filters, and focus-mode toggle (dim non-matching nodes).
//
// Architecture Notes:
//     Pattern: Overlay control hosted directly as a Canvas child (ZIndex=200).
//     Fires FilterChanged event with the active filter state.
//     DiagramCanvas applies focus mode via DiagramVisualLayer.SetFocusNodes.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Compact filter bar overlaid in DiagramCanvas, activated by Ctrl+F.
/// </summary>
public sealed class DiagramFilterBar : Border
{
    // ── UI elements ───────────────────────────────────────────────────────────

    private readonly TextBox    _searchBox   = new();
    private readonly Button     _closeBtn    = new();
    private readonly TextBlock  _matchLabel  = new();
    private readonly CheckBox   _focusCheck  = new();

    // ── Debounce timer ────────────────────────────────────────────────────────

    private readonly DispatcherTimer _debounce = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the filter text or focus-mode toggle changes.</summary>
    public event EventHandler<DiagramFilterArgs>? FilterChanged;

    /// <summary>Fired when the user closes the bar (Escape or × button).</summary>
    public event EventHandler? CloseRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramFilterBar()
    {
        Background  = new SolidColorBrush(Color.FromArgb(230, 35, 35, 45));
        BorderBrush = new SolidColorBrush(Color.FromArgb(180, 100, 100, 140));
        BorderThickness = new Thickness(1);
        CornerRadius    = new CornerRadius(4);
        Padding         = new Thickness(6, 4, 6, 4);
        Focusable       = false;

        // Search box
        _searchBox.Width             = 220;
        _searchBox.Height            = 24;
        _searchBox.Background        = new SolidColorBrush(Color.FromRgb(45, 45, 58));
        _searchBox.Foreground        = Brushes.White;
        _searchBox.CaretBrush        = Brushes.White;
        _searchBox.BorderBrush       = new SolidColorBrush(Color.FromRgb(80, 80, 110));
        _searchBox.BorderThickness   = new Thickness(1);
        _searchBox.Padding           = new Thickness(4, 2, 4, 2);
        _searchBox.FontSize          = 12;
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.TextChanged      += (_, _) => _debounce.Start();
        _searchBox.KeyDown          += OnSearchKeyDown;

        // Match count label
        _matchLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 200));
        _matchLabel.FontSize   = 11;
        _matchLabel.Margin     = new Thickness(6, 0, 0, 0);
        _matchLabel.VerticalAlignment = VerticalAlignment.Center;
        _matchLabel.Text       = string.Empty;

        // Focus mode check
        _focusCheck.Content             = "Focus";
        _focusCheck.Foreground          = new SolidColorBrush(Color.FromRgb(200, 200, 220));
        _focusCheck.FontSize            = 11;
        _focusCheck.Margin              = new Thickness(8, 0, 0, 0);
        _focusCheck.VerticalAlignment   = VerticalAlignment.Center;
        _focusCheck.IsChecked           = true;
        _focusCheck.Checked            += (_, _) => RaiseFilter();
        _focusCheck.Unchecked          += (_, _) => RaiseFilter();

        // Close button
        _closeBtn.Content          = "×";
        _closeBtn.Width            = 20;
        _closeBtn.Height           = 20;
        _closeBtn.Padding          = new Thickness(0);
        _closeBtn.FontSize         = 14;
        _closeBtn.Background       = Brushes.Transparent;
        _closeBtn.Foreground       = new SolidColorBrush(Color.FromRgb(180, 180, 200));
        _closeBtn.BorderThickness  = new Thickness(0);
        _closeBtn.Margin           = new Thickness(6, 0, 0, 0);
        _closeBtn.VerticalAlignment = VerticalAlignment.Center;
        _closeBtn.Click           += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        // Layout: horizontal StackPanel
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text          = "Filter: ",
            Foreground    = new SolidColorBrush(Color.FromRgb(180, 180, 200)),
            FontSize      = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin        = new Thickness(0, 0, 4, 0)
        });
        panel.Children.Add(_searchBox);
        panel.Children.Add(_matchLabel);
        panel.Children.Add(_focusCheck);
        panel.Children.Add(_closeBtn);

        Child = panel;

        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            RaiseFilter();
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Focuses the search text box.</summary>
    public void FocusInput() => _searchBox.Focus();

    /// <summary>The current filter text.</summary>
    public string FilterText => _searchBox.Text;

    /// <summary>Whether focus mode (dim non-matching) is active.</summary>
    public bool IsFocusMode => _focusCheck.IsChecked == true;

    /// <summary>Updates the match count label.</summary>
    public void SetMatchCount(int matched, int total)
    {
        _matchLabel.Text = matched == total
            ? string.Empty
            : $"{matched}/{total}";
    }

    /// <summary>Clears the search text and hides the bar.</summary>
    public void Clear()
    {
        _searchBox.Text = string.Empty;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void RaiseFilter()
    {
        FilterChanged?.Invoke(this, new DiagramFilterArgs(
            Text:      _searchBox.Text.Trim(),
            FocusMode: _focusCheck.IsChecked == true));
    }
}

/// <summary>Arguments for <see cref="DiagramFilterBar.FilterChanged"/>.</summary>
/// <param name="Text">Filter term (empty = no filter).</param>
/// <param name="FocusMode">True = dim non-matching nodes.</param>
public sealed record DiagramFilterArgs(string Text, bool FocusMode);
