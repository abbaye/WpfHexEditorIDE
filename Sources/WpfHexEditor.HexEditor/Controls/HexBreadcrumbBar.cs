// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Controls/HexBreadcrumbBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     A compact breadcrumb bar shown above the hex editor.
//     Displays current byte offset, selection length (when > 0),
//     and the detected format name when a format has been identified.
//
// Architecture Notes:
//     Driven by HexEditor.BreadcrumbBar.cs (partial) which subscribes
//     to SelectionStartChanged, SelectionStopChanged, and FormatDetected.
//     Hidden (Visibility.Collapsed) when ShowBreadcrumbBar DP is false.
//     Theme tokens: HexEditor_HeaderBackgroundColor, HexEditor_HeaderForegroundColor,
//                   HexEditor_ColumnSeparatorColor, HexEditor_ForegroundOffSetHeaderColor.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>
/// Breadcrumb bar for the hex editor — shows offset, selection length, and format name.
/// </summary>
public sealed class HexBreadcrumbBar : Border
{
    // ── Child controls ────────────────────────────────────────────────────────
    private readonly StackPanel _panel;
    private readonly TextBlock  _offsetText;
    private readonly TextBlock  _selectionText;
    private readonly TextBlock  _formatText;
    private readonly TextBlock  _sepSel;
    private readonly TextBlock  _sepFmt;

    // ── State ─────────────────────────────────────────────────────────────────
    private long   _currentOffset;
    private long   _selectionLength;
    private string _formatName = string.Empty;

    private static readonly string Sep = "  ›  ";

    // ── Constructor ───────────────────────────────────────────────────────────

    public HexBreadcrumbBar()
    {
        Height     = 22;
        Padding    = new Thickness(8, 0, 8, 0);
        BorderThickness = new Thickness(0, 0, 0, 1);
        SetResourceReference(BackgroundProperty,  "HeaderBrush");
        SetResourceReference(BorderBrushProperty, "ColumnSeparatorBrush");

        _offsetText    = MakeText(isBold: true);
        _selectionText = MakeText();
        _formatText    = MakeText(isItalic: true);
        _sepSel = MakeSep();
        _sepFmt = MakeSep();

        _selectionText.Visibility = Visibility.Collapsed;
        _sepSel.Visibility        = Visibility.Collapsed;
        _formatText.Visibility    = Visibility.Collapsed;
        _sepFmt.Visibility        = Visibility.Collapsed;

        _panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _panel.Children.Add(_offsetText);
        _panel.Children.Add(_sepSel);
        _panel.Children.Add(_selectionText);
        _panel.Children.Add(_sepFmt);
        _panel.Children.Add(_formatText);

        Child = _panel;

        // Ensure offset shows "0x00000000" on initial render
        Render();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates offset and selection length displayed in the bar.</summary>
    public void UpdateOffset(long offset, long selectionLength)
    {
        _currentOffset   = offset;
        _selectionLength = selectionLength;
        Render();
    }

    /// <summary>Sets the detected format name. Pass null or empty to hide.</summary>
    public void SetFormatName(string? formatName)
    {
        _formatName = formatName ?? string.Empty;
        Render();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void Render()
    {
        _offsetText.Text = $"0x{_currentOffset:X8}";

        bool hasSel = _selectionLength > 0;
        _selectionText.Text        = hasSel ? $"{_selectionLength} byte{(_selectionLength == 1 ? "" : "s")}" : string.Empty;
        _selectionText.Visibility  = hasSel ? Visibility.Visible : Visibility.Collapsed;
        _sepSel.Visibility         = hasSel ? Visibility.Visible : Visibility.Collapsed;

        bool hasFmt = !string.IsNullOrEmpty(_formatName);
        _formatText.Text       = hasFmt ? _formatName : string.Empty;
        _formatText.Visibility = hasFmt ? Visibility.Visible : Visibility.Collapsed;
        _sepFmt.Visibility     = hasFmt ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextBlock MakeText(bool isBold = false, bool isItalic = false)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 11,
            FontFamily        = new FontFamily("Consolas"),
            FontWeight        = isBold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle         = isItalic ? FontStyles.Italic : FontStyles.Normal,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "HeaderTextBrush");
        return tb;
    }

    private static TextBlock MakeSep()
    {
        var tb = new TextBlock
        {
            Text              = Sep,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 11,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "OffsetBrush");
        return tb;
    }
}
