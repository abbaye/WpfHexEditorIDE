// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: NumericSliderEditor.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Inline numeric editor combining a TextBox and a Slider for
//     double / float / int DependencyProperty editing.
//
// Architecture Notes:
//     UserControl. Value DP bound two-way by PropertyInspectorPanel.
//     ValueCommitted event fires on slider thumb release or text box LostFocus.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Editor.XamlDesigner.Controls.Editors;

/// <summary>
/// Numeric slider + text-box inline editor.
/// </summary>
public partial class NumericSliderEditor : UserControl, INotifyPropertyChanged
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericSliderEditor),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((NumericSliderEditor)d).OnValueChanged((double)e.NewValue)));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericSliderEditor),
            new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericSliderEditor),
            new FrameworkPropertyMetadata(100.0));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(NumericSliderEditor),
            new FrameworkPropertyMetadata(1.0));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(NumericSliderEditor),
            new FrameworkPropertyMetadata(10.0));

    public double Value       { get => (double)GetValue(ValueProperty);       set => SetValue(ValueProperty, value); }
    public double Minimum     { get => (double)GetValue(MinimumProperty);     set => SetValue(MinimumProperty, value); }
    public double Maximum     { get => (double)GetValue(MaximumProperty);     set => SetValue(MaximumProperty, value); }
    public double SmallChange { get => (double)GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
    public double LargeChange { get => (double)GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

    // ── Constructor ───────────────────────────────────────────────────────────

    public NumericSliderEditor()
    {
        InitializeComponent();
        DataContext = this;

        TbValue.LostFocus += (_, _) =>
        {
            if (double.TryParse(TbValue.Text, out double v))
            {
                Value = v;
                ValueCommitted?.Invoke(this, v);
            }
        };

        SliderValue.ValueChanged += (_, e) => OnPropertyChanged(nameof(DisplayValue));

        SliderValue.AddHandler(
            System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
            new System.Windows.Controls.Primitives.DragCompletedEventHandler((_, _) =>
                ValueCommitted?.Invoke(this, Value)));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string DisplayValue
    {
        get => double.IsNaN(Value) ? "Auto" : Value.ToString("G4");
        set
        {
            if (value == "Auto")  { Value = double.NaN; return; }
            if (double.TryParse(value, out double d)) Value = d;
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<double>? ValueCommitted;

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnValueChanged(double _)
        => OnPropertyChanged(nameof(DisplayValue));
}
