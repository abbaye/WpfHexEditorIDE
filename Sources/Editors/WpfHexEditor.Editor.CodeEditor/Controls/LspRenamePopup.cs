// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/LspRenamePopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Inline rename popup (a single TextBox) shown over the caret when the user
//     presses F2 in the CodeEditor. Returns the new name via ShowAsync(), or null
//     when the user cancels with Escape.
//
// Architecture Notes:
//     Pattern: Promise-based Popup.
//     Brushes reuse CE_* theme tokens to stay consistent with the editor chrome.
// ==========================================================

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Inline popup containing a single <see cref="TextBox"/> for symbol rename.
/// Call <see cref="ShowAsync"/> to display it; it resolves with the new name
/// (Enter) or <c>null</c> (Escape / dismissed).
/// </summary>
internal sealed class LspRenamePopup : Popup
{
    private readonly TextBox                   _textBox;
    private TaskCompletionSource<string?>?      _tcs;

    internal LspRenamePopup(UIElement placementTarget)
    {
        PlacementTarget    = placementTarget;
        Placement          = PlacementMode.Absolute;
        StaysOpen          = false;
        AllowsTransparency = true;
        PopupAnimation     = PopupAnimation.None;

        _textBox = new TextBox
        {
            MinWidth        = 160,
            Padding         = new Thickness(6, 3, 6, 3),
            BorderThickness = new Thickness(1),
            FontSize        = 13,
        };
        _textBox.SetResourceReference(TextBox.BackgroundProperty,  "CE_Background");
        _textBox.SetResourceReference(TextBox.ForegroundProperty,  "CE_Foreground");
        _textBox.SetResourceReference(TextBox.BorderBrushProperty, "CE_BorderBrush");
        _textBox.SetResourceReference(TextBox.CaretBrushProperty,  "CE_CaretBrush");
        _textBox.KeyDown += OnTextBoxKeyDown;

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(3),
            Padding         = new Thickness(0),
            Child           = _textBox,
            Effect = new DropShadowEffect
            {
                Direction   = 270,
                ShadowDepth = 3,
                BlurRadius  = 6,
                Opacity     = 0.35,
            },
        };
        border.SetResourceReference(Border.BorderBrushProperty, "CE_BorderBrush");

        Child = border;

        Closed += (_, _) => _tcs?.TrySetResult(null);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the rename popup at the given screen coordinates with the word
    /// pre-filled and selected. Returns the entered name, or <c>null</c> if cancelled.
    /// </summary>
    internal Task<string?> ShowAsync(string currentWord, double screenX, double screenY)
    {
        _textBox.Text           = currentWord;
        _textBox.SelectAll();

        HorizontalOffset = screenX;
        VerticalOffset   = screenY;

        _tcs = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        IsOpen = true;
        _textBox.Focus();
        return _tcs.Task;
    }

    // ── Keyboard handler ──────────────────────────────────────────────────────

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            {
                var name = _textBox.Text?.Trim();
                IsOpen = false;
                _tcs?.TrySetResult(string.IsNullOrEmpty(name) ? null : name);
                e.Handled = true;
                break;
            }
            case Key.Escape:
                IsOpen = false;
                e.Handled = true;
                break;
        }
    }
}
