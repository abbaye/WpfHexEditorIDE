// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ThicknessEditor.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Inline Thickness editor with four independent numeric fields (L, T, R, B)
//     and an optional "link all" toggle for uniform editing.
//
// Architecture Notes:
//     UserControl with 4 double sub-properties and a Value (Thickness) DP.
//     ValueCommitted event fires when any field loses focus.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Editor.XamlDesigner.Controls.Editors;

/// <summary>
/// 4-field Thickness editor for the Property Inspector.
/// </summary>
public partial class ThicknessEditor : UserControl, INotifyPropertyChanged
{
    // ── Dependency Property ───────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(Thickness), typeof(ThicknessEditor),
            new FrameworkPropertyMetadata(
                new Thickness(0),
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((ThicknessEditor)d).OnValueChanged((Thickness)e.NewValue)));

    public Thickness Value
    {
        get => (Thickness)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _linked;
    private bool _updating;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ThicknessEditor()
    {
        InitializeComponent();
        DataContext = this;

        // Fire ValueCommitted on each field's LostFocus.
        TbLeft.LostFocus   += (_, _) => CommitFromFields();
        TbTop.LostFocus    += (_, _) => CommitFromFields();
        TbRight.LostFocus  += (_, _) => CommitFromFields();
        TbBottom.LostFocus += (_, _) => CommitFromFields();
    }

    // ── Sub-properties (bound by XAML) ────────────────────────────────────────

    public double Left
    {
        get => Value.Left;
        set
        {
            if (_linked)
                SetAll(value);
            else
                Value = new Thickness(value, Value.Top, Value.Right, Value.Bottom);
        }
    }

    public double Top
    {
        get => Value.Top;
        set
        {
            if (_linked)
                SetAll(value);
            else
                Value = new Thickness(Value.Left, value, Value.Right, Value.Bottom);
        }
    }

    public double Right
    {
        get => Value.Right;
        set
        {
            if (_linked)
                SetAll(value);
            else
                Value = new Thickness(Value.Left, Value.Top, value, Value.Bottom);
        }
    }

    public double Bottom
    {
        get => Value.Bottom;
        set
        {
            if (_linked)
                SetAll(value);
            else
                Value = new Thickness(Value.Left, Value.Top, Value.Right, value);
        }
    }

    public bool Linked
    {
        get => _linked;
        set { _linked = value; OnPropertyChanged(); }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<Thickness>? ValueCommitted;

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnValueChanged(Thickness t)
    {
        if (_updating) return;
        _updating = true;
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(Right));
        OnPropertyChanged(nameof(Bottom));
        _updating = false;
    }

    private void SetAll(double v)
        => Value = new Thickness(v);

    private void CommitFromFields()
    {
        if (double.TryParse(TbLeft.Text,   out double l) &&
            double.TryParse(TbTop.Text,    out double t) &&
            double.TryParse(TbRight.Text,  out double r) &&
            double.TryParse(TbBottom.Text, out double b))
        {
            var thickness = new Thickness(l, t, r, b);
            Value = thickness;
            ValueCommitted?.Invoke(this, thickness);
        }
    }
}
