// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramScopeChips.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-09
// Description:
//     Thin chip row below the nav bar that shows the active scope filters.
//     Each chip has an × button to drop that dimension from the scope.
//     Collapses (Height=0) automatically when scope is All.
//
// Architecture Notes:
//     Pure code-behind. Chips are built dynamically from ScopeFilter.
//     ScopeChanged event propagates upward to ClassDiagramSplitHost.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Compact chip row that shows active scope constraints from <see cref="ScopeFilter"/>.
/// Collapses automatically when <see cref="ScopeFilter.IsAll"/> is true.
/// </summary>
public sealed class DiagramScopeChips : Border
{
    private readonly WrapPanel _panel;
    private ScopeFilter        _current = ScopeFilter.All;

    /// <summary>Fired when the user removes a scope dimension via the chip × button.</summary>
    public event EventHandler<ScopeFilter>? ScopeChanged;

    public DiagramScopeChips()
    {
        _panel          = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 1, 4, 1) };
        Child           = _panel;
        Padding         = new Thickness(2, 0, 2, 0);
        Visibility      = Visibility.Collapsed;
        this.SetResourceReference(BackgroundProperty,  "CD_NavBarBackground");
        this.SetResourceReference(BorderBrushProperty, "CD_NavBarBorder");
        BorderThickness = new Thickness(0, 0, 0, 1);
    }

    /// <summary>Updates the chip row to reflect the given scope.</summary>
    public void SetScope(ScopeFilter filter)
    {
        _current = filter;
        _panel.Children.Clear();

        if (filter.IsAll)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        if (filter.ProjectName is not null)
            _panel.Children.Add(MakeChip($"Project: {filter.ProjectName}", RemoveProject));

        if (filter.Namespace is not null)
            _panel.Children.Add(MakeChip($"Namespace: {filter.Namespace}", RemoveNamespace));

        Visibility = _panel.Children.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void RemoveProject()
    {
        var next = new ScopeFilter { ProjectName = null, Namespace = _current.Namespace };
        ScopeChanged?.Invoke(this, next);
    }

    private void RemoveNamespace()
    {
        var next = new ScopeFilter { ProjectName = _current.ProjectName, Namespace = null };
        ScopeChanged?.Invoke(this, next);
    }

    private Border MakeChip(string text, Action onRemove)
    {
        var label = new TextBlock
        {
            Text              = text,
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 3, 0)
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarChipForeground");

        var closeBtn = new TextBlock
        {
            Text              = "×",
            FontSize          = 11,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        closeBtn.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarChipForeground");
        closeBtn.MouseLeftButtonDown += (_, _) => onRemove();

        var inner = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 0) };
        inner.Children.Add(label);
        inner.Children.Add(closeBtn);

        var chip = new Border
        {
            Child           = inner,
            CornerRadius    = new CornerRadius(3),
            Margin          = new Thickness(0, 1, 4, 1),
            Padding         = new Thickness(0, 1, 0, 1),
            BorderThickness = new Thickness(1)
        };
        chip.SetResourceReference(BackgroundProperty,   "CD_NavBarChipBackground");
        chip.SetResourceReference(BorderBrushProperty,  "CD_NavBarChipBorder");
        return chip;
    }
}
