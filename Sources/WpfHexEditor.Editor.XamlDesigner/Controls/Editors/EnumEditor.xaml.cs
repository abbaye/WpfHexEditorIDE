// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: EnumEditor.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Auto-generated ComboBox editor for any Enum DependencyProperty.
//     Populates itself via Enum.GetValues() when EnumType is set.
//
// Architecture Notes:
//     UserControl. EnumType DP drives item population.
//     SelectedValue DP represents the current enum value.
//     ValueCommitted event fires on ComboBox selection change.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Editor.XamlDesigner.Controls.Editors;

/// <summary>
/// ComboBox-based editor for enum-typed DependencyProperties.
/// </summary>
public partial class EnumEditor : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty EnumTypeProperty =
        DependencyProperty.Register(nameof(EnumType), typeof(Type), typeof(EnumEditor),
            new PropertyMetadata(null, (d, e) => ((EnumEditor)d).PopulateItems((Type?)e.NewValue)));

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(EnumEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public Type?   EnumType      { get => (Type?)GetValue(EnumTypeProperty);   set => SetValue(EnumTypeProperty, value); }
    public object? SelectedValue { get => GetValue(SelectedValueProperty);     set => SetValue(SelectedValueProperty, value); }

    // ── Constructor ───────────────────────────────────────────────────────────

    public EnumEditor()
    {
        InitializeComponent();
        DataContext = this;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<object>? ValueCommitted;

    // ── Private ───────────────────────────────────────────────────────────────

    private void PopulateItems(Type? enumType)
    {
        CbEnum.Items.Clear();
        if (enumType is null || !enumType.IsEnum) return;

        foreach (var val in Enum.GetValues(enumType))
            CbEnum.Items.Add(val);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CbEnum.SelectedItem is not null)
            ValueCommitted?.Invoke(this, CbEnum.SelectedItem);
    }
}
