//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/ExpressionCompletePopup.cs
// Description: Standalone autocomplete popup for ExpressionTextBox.
//              Inspired by VS debugger breakpoint condition editor.
//              Completely self-contained — no dependency on SmartCompletePopup
//              or any CodeEditor assembly type.
// Architecture Notes:
//     Popup stays non-focusable so keyboard focus remains in the host TextBox.
//     PlacementTarget is always the inner _box TextBox of ExpressionTextBox.
//     Commit fires SuggestionCommitted; the caller performs text replacement.
//     Reuses SC_Background / SC_BorderBrush / SC_TypeHintForeground theme tokens.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.StructureEditor.Models;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// Autocomplete dropdown for <see cref="ExpressionTextBox"/>.
/// Shows filtered suggestions with icon, display text, type hint, and a
/// signature strip at the bottom.
/// </summary>
internal sealed class ExpressionCompletePopup
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user commits a suggestion (Enter / Tab / double-click).</summary>
    internal event EventHandler<ExpressionCompleteSuggestion>? SuggestionCommitted;

    // ── Controls ──────────────────────────────────────────────────────────────

    private readonly Popup      _popup;
    private readonly ListBox    _list;
    private readonly TextBlock  _sigStrip;

    private IReadOnlyList<ExpressionCompleteSuggestion> _all   = [];
    private string                                       _filter = "";

    // ── Constructor ───────────────────────────────────────────────────────────

    internal ExpressionCompletePopup()
    {
        _list = new ListBox
        {
            MaxHeight           = 220,
            MinWidth            = 320,
            BorderThickness     = new Thickness(0),
            Focusable           = false,
            IsHitTestVisible    = true,
            FontSize            = 11,
            FontFamily          = new FontFamily("Consolas, Courier New"),
        };
        _list.SetResourceReference(Control.BackgroundProperty,   "SC_Background");
        _list.SetResourceReference(Control.ForegroundProperty,   "DockMenuForegroundBrush");
        _list.SelectionChanged  += OnSelectionChanged;
        _list.MouseDoubleClick  += OnMouseDoubleClick;

        _sigStrip = new TextBlock
        {
            FontSize            = 10,
            Padding             = new Thickness(6, 3, 6, 3),
            TextTrimming        = TextTrimming.CharacterEllipsis,
            TextWrapping        = TextWrapping.NoWrap,
        };
        _sigStrip.SetResourceReference(TextBlock.ForegroundProperty,   "SC_TypeHintForeground");
        _sigStrip.SetResourceReference(TextBlock.BackgroundProperty,   "SC_Background");

        var separator = new Border
        {
            Height          = 1,
            Margin          = new Thickness(0),
        };
        separator.SetResourceReference(Border.BackgroundProperty, "SC_BorderBrush");

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(_list);
        stack.Children.Add(separator);
        stack.Children.Add(_sigStrip);

        var outerBorder = new Border
        {
            BorderThickness = new Thickness(1),
            Child           = stack,
        };
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "SC_BorderBrush");
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "SC_Background");

        _popup = new Popup
        {
            Child           = outerBorder,
            Placement       = PlacementMode.Bottom,
            StaysOpen       = true,
            AllowsTransparency = true,
            Focusable       = false,
            PopupAnimation  = PopupAnimation.None,
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    internal bool IsOpen => _popup.IsOpen;

    /// <summary>
    /// Opens (or refreshes) the popup anchored below <paramref name="target"/>.
    /// </summary>
    internal void ShowFor(
        IReadOnlyList<ExpressionCompleteSuggestion> suggestions,
        string filter,
        TextBox target)
    {
        _all    = suggestions;
        _filter = filter;

        _popup.PlacementTarget = target;
        ApplyFilter();

        if (_list.Items.Count == 0)
        {
            _popup.IsOpen = false;
            return;
        }

        _popup.IsOpen = true;
        if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    /// <summary>Updates the filter word and refreshes the list.</summary>
    internal void UpdateFilter(string filter)
    {
        _filter = filter;
        ApplyFilter();

        if (_list.Items.Count == 0)
            _popup.IsOpen = false;
    }

    internal bool NavigateUp()
    {
        if (!_popup.IsOpen || _list.Items.Count == 0) return false;
        _list.SelectedIndex = Math.Max(0, _list.SelectedIndex - 1);
        ScrollIntoView();
        return true;
    }

    internal bool NavigateDown()
    {
        if (!_popup.IsOpen || _list.Items.Count == 0) return false;
        _list.SelectedIndex = Math.Min(_list.Items.Count - 1, _list.SelectedIndex + 1);
        ScrollIntoView();
        return true;
    }

    /// <summary>Commits the currently selected suggestion. Returns true if popup was open.</summary>
    internal bool CommitIfOpen()
    {
        if (!_popup.IsOpen) return false;
        CommitSelected();
        return true;
    }

    /// <summary>Closes the popup. Returns true if it was open.</summary>
    internal bool CloseIfOpen()
    {
        if (!_popup.IsOpen) return false;
        _popup.IsOpen = false;
        return true;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        _list.Items.Clear();
        foreach (var s in _all)
            _list.Items.Add(BuildListItem(s));

        if (_list.Items.Count > 0 && _list.SelectedIndex < 0)
            _list.SelectedIndex = 0;

        UpdateSigStrip();
    }

    private void CommitSelected()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: ExpressionCompleteSuggestion s })
        {
            _popup.IsOpen = false;
            SuggestionCommitted?.Invoke(this, s);
        }
    }

    private void ScrollIntoView()
    {
        if (_list.SelectedItem is not null)
            _list.ScrollIntoView(_list.SelectedItem);
        UpdateSigStrip();
    }

    private void UpdateSigStrip()
    {
        var s = (_list.SelectedItem as ListBoxItem)?.Tag as ExpressionCompleteSuggestion;
        if (s?.Documentation is not null)
        {
            _sigStrip.Text       = s.Documentation;
            _sigStrip.Visibility = Visibility.Visible;
        }
        else
        {
            _sigStrip.Text       = "";
            _sigStrip.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Builds a ListBoxItem for a suggestion with fuzzy-match highlighting.
    /// Matched characters are rendered in accent color + semi-bold.
    /// The suggestion is stored in the item's Tag for retrieval on commit.
    /// </summary>
    private static ListBoxItem BuildListItem(ExpressionCompleteSuggestion s)
    {
        // Icon glyph
        var icon = new TextBlock
        {
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 12,
            Text                = s.Icon,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(4, 0, 6, 0),
        };

        // Display text with matched chars highlighted
        var displayTb = new TextBlock
        {
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AppendHighlightedText(displayTb, s.DisplayText, s.MatchedIndices);

        // Type hint (muted)
        var hintTb = new TextBlock
        {
            FontSize          = 10,
            Text              = s.TypeHint ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 4, 0),
            Opacity           = 0.6,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(icon);
        row.Children.Add(displayTb);
        row.Children.Add(hintTb);

        return new ListBoxItem
        {
            Content         = row,
            Tag             = s,
            Height          = 22,
            Padding         = new Thickness(2, 0, 4, 0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
    }

    /// <summary>
    /// Populates <paramref name="tb"/> with Run inlines where matched indices
    /// are rendered in SC_MatchHighlight accent color (falls back to CornflowerBlue).
    /// </summary>
    private static void AppendHighlightedText(
        TextBlock tb,
        string    text,
        IReadOnlyList<int>? matchedIndices)
    {
        if (matchedIndices is null || matchedIndices.Count == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        var matchSet = new HashSet<int>(matchedIndices);
        var accent   = new SolidColorBrush(Color.FromRgb(86, 156, 214));  // VS blue accent

        int i = 0;
        while (i < text.Length)
        {
            if (matchSet.Contains(i))
            {
                // Collect consecutive matched run
                int start = i;
                while (i < text.Length && matchSet.Contains(i)) i++;
                tb.Inlines.Add(new Run(text[start..i])
                {
                    Foreground = accent,
                    FontWeight = FontWeights.SemiBold,
                });
            }
            else
            {
                int start = i;
                while (i < text.Length && !matchSet.Contains(i)) i++;
                tb.Inlines.Add(new Run(text[start..i]));
            }
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnSelectionChanged(object s, SelectionChangedEventArgs e) => UpdateSigStrip();

    private void OnMouseDoubleClick(object s, MouseButtonEventArgs e) => CommitSelected();

    // ── Template builders ─────────────────────────────────────────────────────

    private static DataTemplate BuildItemTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        // Icon glyph
        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 12.0);
        icon.SetValue(TextBlock.MarginProperty, new Thickness(4, 0, 6, 0));
        icon.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ExpressionCompleteSuggestion.Icon)));

        // Display text
        var display = new FrameworkElementFactory(typeof(TextBlock));
        display.SetValue(TextBlock.FontSizeProperty, 11.0);
        display.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        display.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ExpressionCompleteSuggestion.DisplayText)));

        // Type hint (muted)
        var hint = new FrameworkElementFactory(typeof(TextBlock));
        hint.SetValue(TextBlock.FontSizeProperty, 10.0);
        hint.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 4, 0));
        hint.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        hint.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ExpressionCompleteSuggestion.TypeHint)));

        // Apply muted foreground to hint via resource reference — done at ListBox level using ItemContainerStyle

        factory.AppendChild(icon);
        factory.AppendChild(display);
        factory.AppendChild(hint);

        return new DataTemplate { VisualTree = factory };
    }

    private static Style BuildItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 22.0));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2, 0, 4, 0)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        return style;
    }
}
