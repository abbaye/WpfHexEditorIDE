//////////////////////////////////////////////
// Project      : WpfHexEditor.Editor.StructureEditor
// File         : Controls/InputFilter.cs
// Description  : Attached properties for character-level input filtering on TextBox controls.
//                Provides HexOnly, NumericOnly and DecimalOnly filters via PreviewTextInput interception.
// Architecture : Pure attached-property static class; no side-effects on ViewModel.
//////////////////////////////////////////////

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>Attached properties that restrict TextBox input to specific character sets.</summary>
public static class InputFilter
{
    // ── HexOnly ──────────────────────────────────────────────────────────────

    public static readonly DependencyProperty HexOnlyProperty =
        DependencyProperty.RegisterAttached("HexOnly", typeof(bool), typeof(InputFilter),
            new PropertyMetadata(false, OnHexOnlyChanged));

    public static bool GetHexOnly(TextBox tb) => (bool)tb.GetValue(HexOnlyProperty);
    public static void SetHexOnly(TextBox tb, bool value) => tb.SetValue(HexOnlyProperty, value);

    private static void OnHexOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.PreviewTextInput -= BlockNonHex;
        if ((bool)e.NewValue) tb.PreviewTextInput += BlockNonHex;
    }

    private static void BlockNonHex(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^[0-9A-Fa-f]+$");

    // ── NumericOnly ───────────────────────────────────────────────────────────

    public static readonly DependencyProperty NumericOnlyProperty =
        DependencyProperty.RegisterAttached("NumericOnly", typeof(bool), typeof(InputFilter),
            new PropertyMetadata(false, OnNumericOnlyChanged));

    public static bool GetNumericOnly(TextBox tb) => (bool)tb.GetValue(NumericOnlyProperty);
    public static void SetNumericOnly(TextBox tb, bool value) => tb.SetValue(NumericOnlyProperty, value);

    private static void OnNumericOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.PreviewTextInput -= BlockNonNumeric;
        if ((bool)e.NewValue) tb.PreviewTextInput += BlockNonNumeric;
    }

    private static void BlockNonNumeric(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");

    // ── DecimalOnly ───────────────────────────────────────────────────────────

    public static readonly DependencyProperty DecimalOnlyProperty =
        DependencyProperty.RegisterAttached("DecimalOnly", typeof(bool), typeof(InputFilter),
            new PropertyMetadata(false, OnDecimalOnlyChanged));

    public static bool GetDecimalOnly(TextBox tb) => (bool)tb.GetValue(DecimalOnlyProperty);
    public static void SetDecimalOnly(TextBox tb, bool value) => tb.SetValue(DecimalOnlyProperty, value);

    private static void OnDecimalOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.PreviewTextInput -= BlockNonDecimal;
        if ((bool)e.NewValue) tb.PreviewTextInput += BlockNonDecimal;
    }

    private static void BlockNonDecimal(object sender, TextCompositionEventArgs e)
    {
        // Allow digits, one decimal separator, and leading minus
        var tb = (TextBox)sender;
        var proposed = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength).Insert(tb.SelectionStart, e.Text);
        e.Handled = !Regex.IsMatch(proposed, @"^-?\d*\.?\d*$");
    }
}
