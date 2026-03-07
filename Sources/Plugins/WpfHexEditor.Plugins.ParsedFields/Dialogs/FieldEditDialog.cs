// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: FieldEditDialog.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Minimal code-only WPF dialog for editing a single parsed field value.
//     Moved from WpfHexEditor.Panels.BinaryAnalysis into the ParsedFields plugin.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.ParsedFields.Dialogs;

/// <summary>
/// Simple dialog that lets the user edit the value of a <see cref="ParsedFieldViewModel"/>.
/// </summary>
internal sealed class FieldEditDialog : Window
{
    private readonly TextBox _valueBox;

    public FieldEditDialog(ParsedFieldViewModel field)
    {
        Title           = $"Edit Field — {field.Name}";
        Width           = 360;
        Height          = 160;
        ResizeMode      = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar   = false;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text       = $"Value ({field.ValueType}):",
            Margin     = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(label, 0);

        _valueBox = new TextBox
        {
            Text              = field.FormattedValue ?? string.Empty,
            Margin            = new Thickness(0, 0, 0, 12),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding           = new Thickness(4, 2, 4, 2)
        };
        _valueBox.SelectAll();
        Grid.SetRow(_valueBox, 1);

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var ok = new Button { Content = "OK", Width = 72, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => { DialogResult = true; };

        var cancel = new Button { Content = "Cancel", Width = 72, IsCancel = true };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);

        grid.Children.Add(label);
        grid.Children.Add(_valueBox);
        grid.Children.Add(buttons);
        Content = grid;

        Loaded += (_, _) => _valueBox.Focus();
    }

    /// <summary>The value string entered by the user (valid only when <see cref="Window.ShowDialog"/> returns true).</summary>
    public string EditedValue => _valueBox.Text;
}
