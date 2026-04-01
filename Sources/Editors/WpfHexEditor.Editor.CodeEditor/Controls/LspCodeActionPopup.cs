// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/LspCodeActionPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Floating popup that lists available LSP code actions (quick fixes / refactors).
//     Triggered by Ctrl+. in the CodeEditor. The caller awaits ShowAsync() which
//     returns the user-selected LspCodeAction, or null if dismissed.
//
// Architecture Notes:
//     Pattern: Promise-based Popup — TaskCompletionSource<T> allows the
//     caller to await the result without blocking the UI thread.
//     Brushes reuse existing CE_* theme tokens — no new tokens required.
// ==========================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Floating popup that presents a list of <see cref="LspCodeAction"/> items
/// and returns the user's selection via an async API.
/// </summary>
internal sealed class LspCodeActionPopup : Popup
{
    private readonly ListBox                              _list;
    private TaskCompletionSource<LspCodeAction?>?         _tcs;

    internal LspCodeActionPopup(UIElement placementTarget)
    {
        PlacementTarget    = placementTarget;
        Placement          = PlacementMode.Absolute;
        StaysOpen          = false;
        AllowsTransparency = true;
        PopupAnimation     = PopupAnimation.None;

        _list = new ListBox
        {
            MaxHeight           = 240,
            MinWidth            = 280,
            BorderThickness     = new Thickness(0),
            FocusVisualStyle    = null,
        };
        _list.SetResourceReference(ListBox.BackgroundProperty,  "CE_Background");
        _list.SetResourceReference(ListBox.ForegroundProperty,  "CE_Foreground");
        _list.SetResourceReference(ListBox.BorderBrushProperty, "CE_BorderBrush");
        _list.KeyDown          += OnListKeyDown;
        _list.MouseDoubleClick += OnListDoubleClick;

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(0, 2, 0, 2),
            Child           = _list,
            Effect = new DropShadowEffect
            {
                Direction   = 270,
                ShadowDepth = 4,
                BlurRadius  = 8,
                Opacity     = 0.4,
            },
        };
        border.SetResourceReference(Border.BackgroundProperty,  "CE_Background");
        border.SetResourceReference(Border.BorderBrushProperty, "CE_BorderBrush");

        Child = border;

        Closed += (_, _) => _tcs?.TrySetResult(null);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the popup anchored to the given screen coordinates and returns
    /// the action selected by the user, or <c>null</c> when dismissed.
    /// </summary>
    internal Task<LspCodeAction?> ShowAsync(
        IReadOnlyList<LspCodeAction> actions,
        double screenX, double screenY)
    {
        _list.Items.Clear();
        foreach (var action in actions)
        {
            var item = new ListBoxItem
            {
                Content  = action.Title,
                Tag      = action,
                FontWeight = action.IsPreferred ? FontWeights.SemiBold : FontWeights.Normal,
                Padding  = new Thickness(8, 3, 8, 3),
            };
            _list.Items.Add(item);
        }

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;

        HorizontalOffset = screenX;
        VerticalOffset   = screenY;

        _tcs = new TaskCompletionSource<LspCodeAction?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        IsOpen = true;
        _list.Focus();
        return _tcs.Task;
    }

    // ── Keyboard / mouse handlers ─────────────────────────────────────────────

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
        => Commit();

    private void Commit()
    {
        var action = (_list.SelectedItem as ListBoxItem)?.Tag as LspCodeAction;
        IsOpen = false;
        _tcs?.TrySetResult(action);
    }
}
