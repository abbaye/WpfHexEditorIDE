// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ColorPickerEditor.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Thin wrapper hosting WpfHexEditor.ColorPicker.Controls.ColorPicker
//     in a popup. Exposes a Value (Color) DP and ValueCommitted event to match
//     the PropertyInspectorPanel contract.
//
// Architecture Notes:
//     Adapter pattern — translates ColorPicker.ColorChanged → ValueCommitted.
//     Feedback loop prevented by WPF DP equality check inside OnValueChanged:
//     setting InnerPicker.SelectedColor to the same value is a no-op in WPF.
//
// Theme: XD_ColorSwatchBorder applied to swatch border via DynamicResource.
//        Inner ColorPicker uses its own theme tokens (DockBorderBrush, etc.).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls.Editors;

/// <summary>
/// Inline color swatch that opens the full project ColorPicker in a popup.
/// </summary>
public partial class ColorPickerEditor : UserControl
{
    // ── Dependency Property ───────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(Color), typeof(ColorPickerEditor),
            new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((ColorPickerEditor)d).OnValueChanged((Color)e.NewValue)));

    public Color Value
    {
        get => (Color)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised whenever the user selects a color in the popup.</summary>
    public event EventHandler<Color>? ValueCommitted;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ColorPickerEditor()
    {
        InitializeComponent();

        // Propagate inner picker selection → Value DP + ValueCommitted event.
        InnerPicker.ColorChanged += (_, newColor) =>
        {
            Value = newColor;
            ValueCommitted?.Invoke(this, newColor);
        };

        // Toggle popup visibility on swatch click.
        SwatchBorder.MouseLeftButtonUp += (_, _) =>
            InnerPickerPopup.IsOpen = !InnerPickerPopup.IsOpen;
    }

    // ── Callback ──────────────────────────────────────────────────────────────

    private void OnValueChanged(Color c)
    {
        SwatchFill.Color = c;

        // Sync the inner picker only when truly different to prevent a
        // feedback loop: same-value DP set is a no-op in WPF.
        if (InnerPicker.SelectedColor != c)
            InnerPicker.SelectedColor = c;
    }
}
