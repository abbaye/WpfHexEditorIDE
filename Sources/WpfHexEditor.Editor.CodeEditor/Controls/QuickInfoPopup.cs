// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/QuickInfoPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Interactive Quick Info hover popup shown when the user hovers over a
//     symbol in the CodeEditor.  Displays symbol kind, type signature,
//     documentation, optional diagnostic section, and action links.
//
// Architecture Notes:
//     Pattern: mirrors ReferencesPopup — derives from Popup with StaysOpen=true.
//     Stay-open: grace timer inside this class; popup stays open when mouse
//     moves from editor into the popup (200 ms grace period before close).
//     All brushes via SetResourceReference — no hardcoded colors.
//     Symbol kind → Segoe MDL2 Assets glyph mapping in KindToGlyph().
//     Markdown: rendered as plain text with line-break splitting (full Markdown
//     rendering is out of scope; this is a follow-up phase).
//     Dismiss: Hide() called by CodeEditor on click / Escape / scroll / text change.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.SDK.ExtensionPoints;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Event args raised when the user clicks an action link inside the popup.
/// </summary>
internal sealed class QuickInfoActionEventArgs : EventArgs
{
    internal string Command { get; init; } = string.Empty;
}

/// <summary>
/// Floating interactive popup showing Quick Info hover data.
/// </summary>
internal sealed class QuickInfoPopup : Popup
{
    #region Fields

    private Border         _outerBorder   = null!;
    private StackPanel     _content       = null!;
    private Point          _anchor;
    private bool           _insidePopup;

    // Grace timer: delays auto-close so the user can move mouse into the popup
    private readonly DispatcherTimer _graceTimer;

    #endregion

    #region Events

    /// <summary>Fired when the user clicks an action link (e.g. Go to Definition).</summary>
    internal event EventHandler<QuickInfoActionEventArgs>? ActionRequested;

    #endregion

    #region Properties

    internal bool IsShowing       => IsOpen;
    internal bool IsMouseOverPopup => _insidePopup;

    #endregion

    #region Constructor

    internal QuickInfoPopup()
    {
        StaysOpen          = true;
        AllowsTransparency = true;

        _graceTimer          = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _graceTimer.Tick    += (_, _) => { _graceTimer.Stop(); IsOpen = false; };

        BuildUI();
        PreviewKeyDown += OnPreviewKeyDown;

        if (Application.Current is not null)
            Application.Current.Deactivated += OnApplicationDeactivated;
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(DispatcherPriority.Background,
               new Action(() => IsOpen = false));

    #endregion

    #region Public API

    /// <summary>Shows the popup anchored at <paramref name="anchor"/> (relative to <paramref name="owner"/>).</summary>
    internal void Show(FrameworkElement owner, QuickInfoResult result, Point anchor)
    {
        _anchor              = anchor;
        _graceTimer.Stop();
        _insidePopup         = false;

        PlacementTarget              = owner;
        Placement                    = PlacementMode.Custom;
        CustomPopupPlacementCallback = CalculatePlacement;

        PopulateContent(result);
        IsOpen = true;
    }

    /// <summary>Immediately closes the popup, cancelling the grace timer.</summary>
    internal void Hide()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }

    /// <summary>
    /// Called by CodeEditor.OnMouseLeave — starts the 200 ms grace period
    /// before auto-closing, allowing the user to move the mouse into the popup.
    /// </summary>
    internal void OnEditorMouseLeft()
    {
        if (!IsOpen || _insidePopup) return;
        _graceTimer.Stop();
        _graceTimer.Start();
    }

    internal void Dispose()
    {
        if (Application.Current is not null)
            Application.Current.Deactivated -= OnApplicationDeactivated;
    }

    #endregion

    #region UI Construction

    private void BuildUI()
    {
        _outerBorder = new Border
        {
            MinWidth        = 320,
            MaxWidth        = 600,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(12, 10, 12, 10),
            Effect          = new DropShadowEffect
            {
                Color       = Colors.Black,
                Opacity     = 0.40,
                BlurRadius  = 10,
                ShadowDepth = 3
            }
        };
        _outerBorder.SetResourceReference(Border.BackgroundProperty,  "CE_QuickInfo_Background");
        _outerBorder.SetResourceReference(Border.BorderBrushProperty, "CE_QuickInfo_Border");

        _content = new StackPanel { Orientation = Orientation.Vertical };
        _outerBorder.Child = _content;

        // Swallow clicks so they don't bubble through to CodeEditor.OnMouseDown
        _outerBorder.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _outerBorder.MouseEnter          += (_, _) => { _insidePopup = true;  _graceTimer.Stop(); };
        _outerBorder.MouseLeave          += (_, _) => { _insidePopup = false; _graceTimer.Start(); };

        Child = _outerBorder;
    }

    #endregion

    #region Content Population

    private void PopulateContent(QuickInfoResult result)
    {
        _content.Children.Clear();

        AddHeader(result.SymbolName, result.SymbolKind);

        if (!string.IsNullOrWhiteSpace(result.TypeSignature))
            AddSignature(result.TypeSignature!);

        AddSeparator();

        if (!string.IsNullOrWhiteSpace(result.DocumentationMarkdown))
            AddDocumentation(result.DocumentationMarkdown!);

        if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
            AddDiagnosticSection(result.DiagnosticMessage!, result.DiagnosticSeverity ?? "error");

        if (result.ActionLinks.Count > 0)
            AddActionLinks(result.ActionLinks);
    }

    private void AddHeader(string symbolName, string symbolKind)
    {
        var row = new DockPanel { LastChildFill = false };

        // Kind glyph
        var glyph = new TextBlock
        {
            Text              = KindToGlyph(symbolKind),
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0)
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_SignatureText");

        // Symbol name
        var name = new TextBlock
        {
            Text              = symbolName,
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 13,
            FontWeight        = FontWeights.Bold,
            TextWrapping      = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        DockPanel.SetDock(glyph, Dock.Left);
        row.Children.Add(glyph);
        row.Children.Add(name);

        _content.Children.Add(row);
    }

    private void AddSignature(string signature)
    {
        var tb = new TextBlock
        {
            Text         = signature,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 3, 0, 0)
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_SignatureText");
        _content.Children.Add(tb);
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

    private void AddDocumentation(string markdown)
    {
        var scroll = new ScrollViewer
        {
            MaxHeight                     = 200,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin                        = new Thickness(0, 0, 0, 6)
        };

        var panel = new StackPanel { Orientation = Orientation.Vertical };

        // Strip markdown code fences; render plain text paragraphs
        string text = markdown
            .Replace("```csharp", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty)
            .Trim();

        foreach (string para in text.Split('\n'))
        {
            string trimmed = para.TrimEnd('\r').Trim();
            if (trimmed.Length == 0) continue;

            var tb = new TextBlock
            {
                Text         = trimmed,
                FontSize     = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 2)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_TypeText");
            panel.Children.Add(tb);
        }

        scroll.Content = panel;
        _content.Children.Add(scroll);
    }

    private void AddDiagnosticSection(string message, string severity)
    {
        string severityKey = severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
            ? "CE_QuickInfo_DiagnosticWarning"
            : "CE_QuickInfo_DiagnosticError";

        string glyph = severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
            ? "\uE7BA"   // Segoe MDL2: Warning
            : "\uE783";  // Segoe MDL2: ErrorBadge

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Padding         = new Thickness(8, 5, 8, 5),
            Margin          = new Thickness(0, 0, 0, 6)
        };
        border.SetResourceReference(Border.BackgroundProperty,  severityKey);
        border.SetResourceReference(Border.BorderBrushProperty, "CE_QuickInfo_Separator");

        var row = new DockPanel { LastChildFill = true };

        var icon = new TextBlock
        {
            Text              = glyph,
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 13,
            VerticalAlignment = VerticalAlignment.Top,
            Margin            = new Thickness(0, 0, 6, 0)
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        var msg = new TextBlock
        {
            Text         = message,
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap
        };
        msg.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_Text");

        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(msg);

        border.Child = row;
        _content.Children.Add(border);
    }

    private void AddActionLinks(IReadOnlyList<QuickInfoActionLink> links)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];

            if (i > 0)
            {
                var sep = new TextBlock { Text = "  |  ", FontSize = 11 };
                sep.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_TypeText");
                row.Children.Add(sep);
            }

            var tb = new TextBlock
            {
                Text              = link.Label,
                FontSize          = 11,
                Cursor            = Cursors.Hand,
                Background        = Brushes.Transparent
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "CE_QuickInfo_LinkText");
            tb.MouseEnter += (_, _) => tb.TextDecorations = TextDecorations.Underline;
            tb.MouseLeave += (_, _) => tb.TextDecorations = null;

            string command = link.Command;
            tb.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                IsOpen = false;
                ActionRequested?.Invoke(this, new QuickInfoActionEventArgs { Command = command });
            };

            row.Children.Add(tb);
        }

        _content.Children.Add(row);
    }

    #endregion

    #region Placement

    private CustomPopupPlacement[] CalculatePlacement(
        Size popupSize, Size targetSize, Point offset)
    {
        double x = Math.Min(_anchor.X, Math.Max(0, targetSize.Width  - popupSize.Width  - 8));
        double y = _anchor.Y;
        if (y + popupSize.Height > targetSize.Height - 8)
            y = Math.Max(0, _anchor.Y - popupSize.Height - 4);
        return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical) };
    }

    #endregion

    #region Keyboard

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsOpen    = false;
            e.Handled = true;
        }
    }

    #endregion

    #region Glyph mapping

    private static string KindToGlyph(string kind) => kind?.ToLowerInvariant() switch
    {
        "method"    => "\uE8A4",   // Settings (method icon approximation)
        "class"     => "\uE7C3",   // Page
        "interface" => "\uE8D4",   // Contact
        "struct"    => "\uE7F4",   // View
        "enum"      => "\uE8B1",   // List
        "property"  => "\uE9E9",   // Code
        "field"     => "\uE713",   // Edit
        "event"     => "\uE7C1",   // Flag
        "keyword"   => "\uE8D2",   // Tag
        "namespace" => "\uE8B7",   // Library
        "error"     => "\uE783",   // ErrorBadge
        _           => "\uE8A5"    // Code (default)
    };

    #endregion
}
