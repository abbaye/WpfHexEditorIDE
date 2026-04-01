// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/SignatureHelpPopup.cs
// Description:
//     VS2026-style SignatureHelp popup — displays all overloads with the active
//     parameter highlighted in bold, an overload counter, and optional documentation.
//
// Architecture Notes:
//     Owned by CodeEditor. Created once and reused across invocations.
//     Shown on '(' / ',' keystrokes via TriggerSignatureHelpAsync (CodeEditor.LSP.cs).
//     The active parameter is updated in-place via AdvanceParameter() as the user types ','.
//     Dismissed on ')' when nesting depth reaches zero, on Escape, or mouse click outside.
//     All brushes sourced from theme tokens so the popup participates in IDE theme switching.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Floating popup that shows textDocument/signatureHelp results with full overload cycling
/// and active-parameter inline highlighting.
/// </summary>
internal sealed class SignatureHelpPopup : Popup
{
    // ── State ─────────────────────────────────────────────────────────────────────
    private LspSignatureHelpResult? _result;
    private int                     _activeSignatureIndex;
    private int                     _activeParameterIndex;

    // ── UI nodes ──────────────────────────────────────────────────────────────────
    private readonly TextBlock _overloadCounter;   // "1 of 3  ↑↓"
    private readonly TextBlock _signature;         // inline-highlighted signature
    private readonly TextBlock _documentation;     // optional doc paragraph
    private readonly StackPanel _docRow;           // hides when no doc

    // ── Constructor ───────────────────────────────────────────────────────────────

    internal SignatureHelpPopup(UIElement placementTarget)
    {
        PlacementTarget    = placementTarget;
        Placement          = PlacementMode.Bottom;
        StaysOpen          = false;
        AllowsTransparency = true;
        PopupAnimation     = PopupAnimation.None;

        // ── Outer container ──────────────────────────────────────────────────────
        var container = new Border
        {
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(10, 5, 10, 6),
            BorderThickness = new Thickness(1),
            MinWidth        = 280,
            MaxWidth        = 650,
            Effect          = new DropShadowEffect
            {
                Direction   = 270,
                ShadowDepth = 4,
                BlurRadius  = 8,
                Opacity     = 0.35,
                Color       = Colors.Black,
            },
        };
        container.SetResourceReference(Border.BackgroundProperty,  "CE_Background");
        container.SetResourceReference(Border.BorderBrushProperty, "CE_GutterBorder");

        // ── Overload counter (top-right) + signature (top-left) ──────────────────
        _overloadCounter = new TextBlock
        {
            FontSize       = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin         = new Thickness(8, 0, 0, 0),
            Visibility     = Visibility.Collapsed,
        };
        _overloadCounter.SetResourceReference(TextBlock.ForegroundProperty, "SH_OverloadCountForeground");

        _signature = new TextBlock
        {
            FontSize   = 12,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            TextWrapping = TextWrapping.Wrap,
        };
        _signature.SetResourceReference(TextBlock.ForegroundProperty, "CE_Foreground");

        var topRow = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_overloadCounter, Dock.Right);
        topRow.Children.Add(_overloadCounter);
        topRow.Children.Add(_signature);

        // ── Documentation row ─────────────────────────────────────────────────────
        var separator = new Border
        {
            Height     = 1,
            Margin     = new Thickness(0, 4, 0, 4),
            Visibility = Visibility.Collapsed,
        };
        separator.SetResourceReference(Border.BackgroundProperty, "CE_GutterBorder");

        _documentation = new TextBlock
        {
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth     = 620,
        };
        _documentation.SetResourceReference(TextBlock.ForegroundProperty, "CE_CommentForeground");

        _docRow = new StackPanel();
        _docRow.Children.Add(separator);
        _docRow.Children.Add(_documentation);
        _docRow.Visibility = Visibility.Collapsed;

        // ── Layout ────────────────────────────────────────────────────────────────
        var root = new StackPanel();
        root.Children.Add(topRow);
        root.Children.Add(_docRow);
        container.Child = root;
        Child           = container;

        // ── Dismiss handlers ──────────────────────────────────────────────────────
        placementTarget.PreviewKeyDown   += OnOwnerKeyDown;
        placementTarget.PreviewMouseDown += OnOwnerMouseDown;
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the popup for a fresh <see cref="LspSignatureHelpResult"/> (new '(' or re-query).
    /// </summary>
    internal void Show(LspSignatureHelpResult result, UIElement placementTarget, Rect caretRect)
    {
        _result               = result;
        _activeSignatureIndex = result.ActiveSignatureIndex;
        _activeParameterIndex = result.ActiveParameterIndex;

        PlacementTarget = placementTarget;
        PlacementRectangle = caretRect;

        RefreshUI();
        IsOpen = true;
    }

    /// <summary>
    /// Updates the active parameter index after a ',' keystroke.
    /// Re-queries the server result if needed; otherwise refreshes in-place.
    /// </summary>
    internal void AdvanceParameter()
    {
        if (!IsOpen || _result is null) return;
        var sig = _result.Signatures[_activeSignatureIndex];
        if (sig.Parameters is null || sig.Parameters.Count == 0) return;

        _activeParameterIndex = Math.Min(_activeParameterIndex + 1, sig.Parameters.Count - 1);
        RefreshSignatureInlines();
    }

    /// <summary>
    /// Updates the active parameter index based on the server response (e.g. after re-query).
    /// </summary>
    internal void UpdateActiveParameter(int paramIndex)
    {
        if (!IsOpen || _result is null) return;
        _activeParameterIndex = paramIndex;
        RefreshSignatureInlines();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (_result is null) return;

        UpdateOverloadCounter();
        RefreshSignatureInlines();
        RefreshDocumentation();
    }

    private void UpdateOverloadCounter()
    {
        if (_result is null) return;
        var count = _result.Signatures.Count;
        if (count <= 1)
        {
            _overloadCounter.Visibility = Visibility.Collapsed;
            return;
        }
        _overloadCounter.Text       = $"{_activeSignatureIndex + 1} of {count}  ↑↓";
        _overloadCounter.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Rebuilds the inline run list inside <see cref="_signature"/> so the active parameter
    /// appears in bold with <c>SH_ActiveParamForeground</c> and all other text is normal.
    /// </summary>
    private void RefreshSignatureInlines()
    {
        if (_result is null) return;
        var sig = _result.Signatures[_activeSignatureIndex];
        _signature.Inlines.Clear();

        var parameters = sig.Parameters;
        if (parameters is null || parameters.Count == 0)
        {
            // No parameters — show the whole label as plain text
            _signature.Inlines.Add(new Run(sig.Label));
            return;
        }

        // Locate the active parameter substring inside the label and split
        // the label into three parts: before, active param, after.
        var activeParam  = (_activeParameterIndex >= 0 && _activeParameterIndex < parameters.Count)
            ? parameters[_activeParameterIndex].Label
            : null;

        if (string.IsNullOrEmpty(activeParam))
        {
            _signature.Inlines.Add(new Run(sig.Label));
            return;
        }

        // Find the nth occurrence of the parameter label (simplistic but correct for most cases).
        // Use the active-parameter index to find the correct occurrence when multiple params
        // share the same type name.
        int searchStart = 0;
        int occurrence  = 0;
        int idx         = -1;
        while (occurrence <= _activeParameterIndex)
        {
            idx = sig.Label.IndexOf(activeParam!, searchStart, StringComparison.Ordinal);
            if (idx < 0) break;
            if (occurrence == _activeParameterIndex) break;
            searchStart = idx + activeParam!.Length;
            occurrence++;
        }

        if (idx < 0)
        {
            // Fallback: can't locate — render plain
            _signature.Inlines.Add(new Run(sig.Label));
            return;
        }

        // Before the active param
        if (idx > 0)
            _signature.Inlines.Add(new Run(sig.Label.Substring(0, idx)));

        // Active param — bold + accent colour
        var activeRun = new Run(activeParam!)
        {
            FontWeight = FontWeights.Bold,
        };
        activeRun.SetResourceReference(Run.ForegroundProperty, "SH_ActiveParamForeground");
        _signature.Inlines.Add(activeRun);

        // After the active param
        int afterStart = idx + activeParam!.Length;
        if (afterStart < sig.Label.Length)
            _signature.Inlines.Add(new Run(sig.Label.Substring(afterStart)));
    }

    private void RefreshDocumentation()
    {
        if (_result is null) return;
        var sig = _result.Signatures[_activeSignatureIndex];
        var doc = sig.Documentation;

        // Also show active parameter documentation if available
        if (sig.Parameters is { Count: > 0 } &&
            _activeParameterIndex >= 0 && _activeParameterIndex < sig.Parameters.Count)
        {
            var paramDoc = sig.Parameters[_activeParameterIndex].Documentation;
            if (!string.IsNullOrWhiteSpace(paramDoc))
                doc = string.IsNullOrWhiteSpace(doc) ? paramDoc : $"{doc}\n\n**{sig.Parameters[_activeParameterIndex].Label}**: {paramDoc}";
        }

        if (string.IsNullOrWhiteSpace(doc))
        {
            _docRow.Visibility = Visibility.Collapsed;
            return;
        }

        _documentation.Text = doc!.Trim();
        _docRow.Visibility  = Visibility.Visible;
        // Show separator only when doc is visible
        ((Border)_docRow.Children[0]).Visibility = Visibility.Visible;
    }

    // ── Input handlers ────────────────────────────────────────────────────────────

    private void OnOwnerKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpen) return;
        switch (e.Key)
        {
            case Key.Escape:
                IsOpen = false;
                e.Handled = false; // let editor handle Escape too
                break;

            case Key.Up:
                CycleOverload(-1);
                e.Handled = true;
                break;

            case Key.Down:
                CycleOverload(+1);
                e.Handled = true;
                break;
        }
    }

    private void OnOwnerMouseDown(object sender, MouseButtonEventArgs e)
        => IsOpen = false;

    private void CycleOverload(int delta)
    {
        if (_result is null) return;
        var count = _result.Signatures.Count;
        _activeSignatureIndex = (_activeSignatureIndex + delta + count) % count;
        UpdateOverloadCounter();
        RefreshSignatureInlines();
        RefreshDocumentation();
    }
}
