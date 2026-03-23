// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/SignatureHelpPopup.cs
// Description:
//     Lightweight non-blocking popup that shows the active method signature
//     when the user types '(' in the code editor and an LSP server is active.
//
// Architecture Notes:
//     Owned by CodeEditor. Shown on '(' keystroke via TriggerSignatureHelpAsync.
//     Dismissed on ')' keystroke, Escape, or mouse click outside.
//     Brushes sourced from theme tokens (CE_Background / CE_Foreground) so it
//     participates in the IDE theme system without adding new tokens.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Floating popup that displays the active method signature hint
/// sourced from an LSP <c>textDocument/signatureHelp</c> response.
/// </summary>
internal sealed class SignatureHelpPopup : Popup
{
    private readonly TextBlock _label;

    internal SignatureHelpPopup(UIElement placementTarget)
    {
        PlacementTarget      = placementTarget;
        Placement            = PlacementMode.Absolute;
        StaysOpen            = false;
        AllowsTransparency   = true;
        PopupAnimation       = PopupAnimation.None;

        var container = new Border
        {
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Direction   = 270,
                ShadowDepth = 3,
                BlurRadius  = 6,
                Opacity     = 0.35,
                Color       = Colors.Black,
            }
        };
        container.SetResourceReference(Border.BackgroundProperty,   "CE_Background");
        container.SetResourceReference(Border.BorderBrushProperty,  "CE_GutterBorder");

        _label = new TextBlock { FontSize = 12 };
        _label.SetResourceReference(TextBlock.ForegroundProperty, "CE_Foreground");

        container.Child = _label;
        Child           = container;

        placementTarget.PreviewKeyDown += OnOwnerKeyDown;
        placementTarget.PreviewMouseDown += OnOwnerMouseDown;
    }

    /// <summary>Shows the popup anchored just above the given screen point.</summary>
    internal void Show(string signature, Point screenPt)
    {
        _label.Text      = signature;
        HorizontalOffset = screenPt.X;
        VerticalOffset   = screenPt.Y - 28; // position above caret baseline
        IsOpen           = true;
    }

    private void OnOwnerKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpen) return;
        if (e.Key is Key.Escape or Key.Return)
            IsOpen = false;
    }

    private void OnOwnerMouseDown(object sender, MouseButtonEventArgs e)
        => IsOpen = false;
}
