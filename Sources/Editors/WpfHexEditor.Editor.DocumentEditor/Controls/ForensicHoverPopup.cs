// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/ForensicHoverPopup.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Hover popup shown when the user dwells over a forensic chip (PAR/LIS/…),
//     a forensic dot, or any block that carries a ForensicAlert in the
//     DocumentCanvasRenderer.  Pattern mirrors CodeEditor's QuickInfoPopup:
//     StaysOpen=true, 200 ms grace timer, custom placement, action links.
//
// Architecture Notes:
//     - Reuses CE_QuickInfo_* theme tokens (defined in all Shell themes).
//     - No async resolution needed — alert data is already in memory.
//     - Action links fire events back to the renderer (NavigateRequested,
//       CopyRequested, SuppressRequested, InspectRequested).
//     - Placement: below the anchor point, flipped above when close to bottom edge.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>Kind of element the popup is anchored to.</summary>
internal enum ForensicHoverTarget { KindChip, ForensicDot, Block }

/// <summary>
/// Interactive hover popup for forensic chips, dots, and alerted blocks
/// in the <see cref="DocumentCanvasRenderer"/>.
/// </summary>
internal sealed class ForensicHoverPopup : Popup
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private Border      _outerBorder = null!;
    private StackPanel  _content     = null!;
    private Point       _anchor;
    private bool        _insidePopup;

    private readonly DispatcherTimer _graceTimer;

    // Cached data for action-link callbacks
    private ForensicAlert?   _currentAlert;
    private DocumentBlock?   _currentBlock;
    private int              _currentBlockIndex;

    // Placement anchors: bottom = preferred position, top = flip-above position
    private double _anchorBottom;
    private double _anchorTop;

    // ── Events ───────────────────────────────────────────────────────────────

    internal event EventHandler<int>?           NavigateRequested;
    internal event EventHandler<string>?        CopyRequested;
    internal event EventHandler<ForensicAlert>? SuppressRequested;
    internal event EventHandler<DocumentBlock>? InspectRequested;

    // ── Properties ───────────────────────────────────────────────────────────

    internal bool IsShowing        => IsOpen;
    internal bool IsMouseOverPopup => _insidePopup;

    // ── Constructor ──────────────────────────────────────────────────────────

    internal ForensicHoverPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;

        _graceTimer       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _graceTimer.Tick += (_, _) => { _graceTimer.Stop(); IsOpen = false; };

        BuildUI();

        if (Application.Current is not null)
            Application.Current.Deactivated += (_, _) =>
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => IsOpen = false));
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the popup for a <paramref name="target"/> element.
    /// <paramref name="alert"/> may be null for kind-chip-only hover (no forensic alert).
    /// </summary>
    internal void Show(
        FrameworkElement    owner,
        Point               anchor,
        double              blockTop,
        double              blockBottom,
        ForensicHoverTarget target,
        DocumentBlock?      block,
        int                 blockIndex,
        ForensicAlert?      alert,
        string              kindLabel)
    {
        _anchor            = anchor;
        _anchorTop         = blockTop;
        _anchorBottom      = blockBottom;
        _currentAlert      = alert;
        _currentBlock      = block;
        _currentBlockIndex = blockIndex;

        _graceTimer.Stop();
        _insidePopup = false;

        PlacementTarget              = owner;
        Placement                    = PlacementMode.Custom;
        CustomPopupPlacementCallback = CalculatePlacement;

        PopulateContent(target, block, alert, kindLabel);
        IsOpen = true;
    }

    /// <summary>Immediately closes the popup, cancelling the grace timer.</summary>
    internal void Hide()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }

    /// <summary>
    /// Called by the renderer's MouseLeave handler — starts the 200 ms grace
    /// period so the user can move the mouse into the popup without it closing.
    /// </summary>
    internal void OnEditorMouseLeft()
    {
        if (!IsOpen || _insidePopup) return;
        _graceTimer.Stop();
        _graceTimer.Start();
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        _outerBorder = new Border
        {
            MinWidth        = 280,
            MaxWidth        = 520,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(12, 10, 12, 10),
            Effect          = new DropShadowEffect
            {
                Color       = Colors.Black,
                Opacity     = 0.35,
                BlurRadius  = 10,
                ShadowDepth = 3
            }
        };
        _outerBorder.SetResourceReference(Border.BackgroundProperty,  "CE_QuickInfo_Background");
        _outerBorder.SetResourceReference(Border.BorderBrushProperty, "CE_QuickInfo_Border");

        _content = new StackPanel { Orientation = Orientation.Vertical };
        _outerBorder.Child = _content;

        _outerBorder.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _outerBorder.MouseEnter          += (_, _) => { _insidePopup = true;  _graceTimer.Stop(); };
        _outerBorder.MouseLeave          += (_, _) => { _insidePopup = false; _graceTimer.Start(); };

        Child = _outerBorder;
    }

    // ── Content Population ───────────────────────────────────────────────────

    private void PopulateContent(
        ForensicHoverTarget target,
        DocumentBlock?      block,
        ForensicAlert?      alert,
        string              kindLabel)
    {
        _content.Children.Clear();

        // Header: kind chip label + optional severity icon
        AddHeader(kindLabel, alert);

        AddSeparator();

        // Alert description + suggestion
        if (alert is not null)
        {
            AddDescription(alert.Description);
            if (!string.IsNullOrWhiteSpace(alert.Suggestion))
                AddSuggestion(alert.Suggestion!);
            AddSeparator();
        }

        // Action links
        AddActionLinks(alert, block);
    }

    private void AddHeader(string kindLabel, ForensicAlert? alert)
    {
        var row = new DockPanel { LastChildFill = true };

        // Severity glyph (only when alert present)
        if (alert is not null)
        {
            bool isError = alert.Severity == ForensicSeverity.Error;
            var glyph = new TextBlock
            {
                Text              = isError ? "" : "", // ErrorBadge / Warning
                FontFamily        = new FontFamily("Segoe MDL2 Assets"),
                FontSize          = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 6, 0),
                Foreground        = isError
                    ? new SolidColorBrush(Color.FromRgb(255, 80, 80))
                    : new SolidColorBrush(Color.FromRgb(255, 200, 0))
            };
            DockPanel.SetDock(glyph, Dock.Left);
            row.Children.Add(glyph);
        }

        // Kind label
        var label = new TextBlock
        {
            Text              = kindLabel,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            TextWrapping      = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        // Alert kind badge (e.g. "OffsetGap")
        if (alert is not null)
        {
            var kindBadge = new TextBlock
            {
                Text              = $"  [{alert.Kind}]",
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            kindBadge.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_SignatureText");

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
            headerRow.Children.Add(label);
            headerRow.Children.Add(kindBadge);
            row.Children.Add(headerRow);
        }
        else
        {
            row.Children.Add(label);
        }

        _content.Children.Add(row);
    }

    private void AddDescription(string description)
    {
        var tb = new TextBlock
        {
            Text         = description,
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 2, 0, 0)
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");
        _content.Children.Add(tb);
    }

    private void AddSuggestion(string suggestion)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Padding         = new Thickness(8, 4, 8, 4),
            Margin          = new Thickness(0, 4, 0, 0)
        };
        border.SetResourceReference(Border.BackgroundProperty,  "CE_QuickInfo_DiagnosticWarning");
        border.SetResourceReference(Border.BorderBrushProperty, "CE_QuickInfo_Separator");

        var row = new DockPanel { LastChildFill = true };
        var icon = new TextBlock
        {
            Text              = "", // Lightbulb
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Top,
            Margin            = new Thickness(0, 0, 6, 0)
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        var msg = new TextBlock
        {
            Text         = suggestion,
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap
        };
        msg.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(msg);
        border.Child = row;
        _content.Children.Add(border);
    }

    private void AddSeparator()
    {
        var sep = new Border
        {
            Height          = 1,
            Margin          = new Thickness(0, 6, 0, 6),
            BorderThickness = new Thickness(0)
        };
        sep.SetResourceReference(Border.BackgroundProperty, "CE_QuickInfo_Separator");
        _content.Children.Add(sep);
    }

    private void AddActionLinks(ForensicAlert? alert, DocumentBlock? block)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
        bool first = true;

        void AddLink(string label, Action action)
        {
            if (!first)
            {
                var sep = new TextBlock { Text = "  |  ", FontSize = 11 };
                sep.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_TypeText");
                row.Children.Add(sep);
            }
            first = false;

            var tb = new TextBlock
            {
                Text       = label,
                FontSize   = 11,
                Cursor     = Cursors.Hand,
                Background = Brushes.Transparent
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_LinkText");
            tb.MouseEnter          += (_, _) => tb.TextDecorations = TextDecorations.Underline;
            tb.MouseLeave          += (_, _) => tb.TextDecorations = null;
            tb.MouseLeftButtonDown += (_, e) => { e.Handled = true; IsOpen = false; action(); };
            row.Children.Add(tb);
        }

        // Navigate — always available
        AddLink("Navigate", () => NavigateRequested?.Invoke(this, _currentBlockIndex));

        // Copy alert — only when alert present
        if (alert is not null)
        {
            AddLink("Copy alert", () => CopyRequested?.Invoke(this, alert.Description));
            AddLink("Mark false positive", () => SuppressRequested?.Invoke(this, alert));
        }

        // Inspect in Hex — only when block has a raw offset
        if (block is not null && block.RawOffset > 0)
            AddLink("Inspect in Hex", () => InspectRequested?.Invoke(this, block));

        _content.Children.Add(row);
    }

    // ── Placement ────────────────────────────────────────────────────────────

    private CustomPopupPlacement[] CalculatePlacement(Size popupSize, Size targetSize, Point offset)
    {
        const double Gap = 8;

        // X: align with mouse, clamped so popup stays inside the canvas
        double x = Math.Max(0, Math.Min(_anchor.X, targetSize.Width - popupSize.Width - 8));

        // Preferred: below the block bottom edge
        double yBelow = _anchorBottom + Gap;

        if (yBelow + popupSize.Height <= targetSize.Height - 8)
            return [new CustomPopupPlacement(new Point(x, yBelow), PopupPrimaryAxis.Vertical)];

        // Flip: above the block top edge — popup bottom aligns with block top - gap
        double yAbove = _anchorTop - popupSize.Height - Gap;
        if (yAbove >= 0)
            return [new CustomPopupPlacement(new Point(x, yAbove), PopupPrimaryAxis.Vertical)];

        // Last resort: best vertical fit
        double y = Math.Max(0, targetSize.Height - popupSize.Height - 8);
        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical)];
    }
}
