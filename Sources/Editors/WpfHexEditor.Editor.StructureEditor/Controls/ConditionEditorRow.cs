//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/ConditionEditorRow.cs
// Description: Code-behind form for ConditionDefinition editing.
//              Follows the ConditionRow pattern from BreakpointConditionDialog.
//              Switches layout when Operator = "expression" (single expr TextBox).
//              Field box and value box use ExpressionTextBox for autocomplete.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.Services;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// A self-contained condition form row bound to a <see cref="ConditionViewModel"/>.
/// Uses code-behind layout like <c>ConditionRow</c> in the Debugger plugin.
/// </summary>
internal sealed class ConditionEditorRow : Border
{
    internal event EventHandler? Changed;

    // ── Controls ──────────────────────────────────────────────────────────────

    private readonly ComboBox            _fieldTypeCombo;
    private readonly ExpressionTextBox   _fieldBox;
    private readonly ComboBox            _operatorCombo;
    private readonly ExpressionTextBox   _valueBox;
    private readonly TextBox             _lengthBox;
    private readonly StackPanel          _secondary;

    private ConditionViewModel? _vm;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal ConditionEditorRow()
    {
        Padding = new Thickness(0, 4, 0, 4);

        _fieldTypeCombo = MakeComboBox(130, "Field reference", "Field reference", "Variable reference", "Expression");
        _fieldTypeCombo.SelectedIndex = 0;
        _fieldTypeCombo.SelectionChanged += OnFieldTypeChanged;

        _fieldBox = new ExpressionTextBox
        {
            Placeholder = "offset:0 or var:name or expression",
            MinWidth    = 200,
            MaxWidth    = 360,
            Margin      = new Thickness(0, 0, 6, 0),
        };

        _operatorCombo = MakeComboBox(110, "equals",
            "equals", "notEquals", "greaterThan", "lessThan", "expression");
        _operatorCombo.SelectedIndex = 0;
        _operatorCombo.SelectionChanged += OnComboChanged;

        _valueBox = new ExpressionTextBox
        {
            Placeholder = "Value (hex: 0xFF or decimal)",
            MinWidth    = 120,
            MaxWidth    = 220,
            Margin      = new Thickness(0, 0, 6, 0),
        };

        _lengthBox = MakeTextBox("1", minWidth: 50, maxWidth: 60);
        _lengthBox.Text = "1";
        InputFilter.SetNumericOnly(_lengthBox, true);

        _secondary = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
        row1.Children.Add(_fieldTypeCombo);
        row1.Children.Add(_fieldBox);

        var outer = new StackPanel { Orientation = Orientation.Vertical };
        outer.Children.Add(row1);
        outer.Children.Add(_secondary);
        Child = outer;

        RebuildSecondary();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    internal void Load(ConditionViewModel vm)
    {
        _vm = vm;

        // Detect field type prefix
        if (vm.Field.StartsWith("var:", StringComparison.Ordinal))
        {
            _fieldTypeCombo.SelectedIndex = 1;
            _fieldBox.Text = vm.Field["var:".Length..];
        }
        else if (vm.Field.StartsWith("expr:", StringComparison.Ordinal) ||
                 vm.Field.StartsWith("expression:", StringComparison.Ordinal))
        {
            _fieldTypeCombo.SelectedIndex = 2;
            _fieldBox.Text = vm.Field;
        }
        else
        {
            _fieldTypeCombo.SelectedIndex = 0;
            _fieldBox.Text = vm.Field;
        }

        _operatorCombo.SelectedItem = vm.Operator;
        if (_operatorCombo.SelectedIndex < 0) _operatorCombo.SelectedIndex = 0;
        _valueBox.Text  = vm.Value;
        _lengthBox.Text = vm.Length.ToString();
        RebuildSecondary();
    }

    internal void Commit()
    {
        if (_vm is null) return;

        var rawField = _fieldBox.Text.Trim();
        _vm.Field = _fieldTypeCombo.SelectedIndex switch
        {
            1 => $"var:{rawField}",
            _ => rawField,
        };
        _vm.Operator = _operatorCombo.SelectedItem?.ToString() ?? "equals";
        _vm.Value    = _valueBox.Text.Trim();
        int.TryParse(_lengthBox.Text, out var len);
        _vm.Length   = len > 0 ? len : 1;
    }

    /// <summary>Injects a live variable source into both expression boxes.</summary>
    internal void SetVariableSource(IVariableSource source)
    {
        _fieldBox.VariableSource = source;
        _valueBox.VariableSource = source;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnFieldTypeChanged(object s, SelectionChangedEventArgs e)
    {
        RebuildSecondary();
        NotifyChanged();
    }

    private void OnComboChanged(object s, SelectionChangedEventArgs e) => NotifyChanged();

    private void RebuildSecondary()
    {
        _secondary.Children.Clear();
        if (_fieldTypeCombo.SelectedIndex < 2)
        {
            _secondary.Children.Add(new TextBlock
            {
                Text = "Length:",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            });
            _secondary.Children.Add(_lengthBox);
            _secondary.Children.Add(_operatorCombo);
            _secondary.Children.Add(_valueBox);
        }
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ComboBox MakeComboBox(double width, string placeholder, params string[] items)
    {
        var cb = new ComboBox
        {
            Width    = width,
            Height   = 22,
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin   = new Thickness(0, 0, 6, 0),
        };
        cb.SetResourceReference(Control.ForegroundProperty,  "DockMenuForegroundBrush");
        cb.SetResourceReference(Control.BackgroundProperty,  "DockMenuBackgroundBrush");
        cb.SetResourceReference(Control.BorderBrushProperty, "DockBorderBrush");
        foreach (var item in items)
            cb.Items.Add(new ComboBoxItem { Content = item, FontSize = 11 });
        return cb;
    }

    private static TextBox MakeTextBox(string placeholder, double minWidth = 180, double maxWidth = 300)
    {
        var tb = new TextBox
        {
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            FontSize = 11,
            Padding  = new Thickness(4, 2, 4, 2),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin  = new Thickness(0, 0, 6, 0),
        };
        tb.SetResourceReference(Control.ForegroundProperty,  "DockMenuForegroundBrush");
        tb.SetResourceReference(Control.BackgroundProperty,  "DockMenuBackgroundBrush");
        tb.SetResourceReference(Control.BorderBrushProperty, "DockBorderBrush");
        tb.Text = placeholder;
        return tb;
    }
}
