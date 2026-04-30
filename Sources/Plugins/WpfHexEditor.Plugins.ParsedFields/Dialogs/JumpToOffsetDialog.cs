// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: JumpToOffsetDialog.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Minimal code-only WPF dialog for entering a hex byte offset.
//     Moved from WpfHexEditor.Panels.BinaryAnalysis into the ParsedFields plugin.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.ParsedFields.Properties;

namespace WpfHexEditor.Plugins.ParsedFields.Dialogs;

/// <summary>
/// Simple dialog that accepts a hex or decimal byte offset from the user.
/// </summary>
internal sealed class JumpToOffsetDialog : Window
{
    private readonly TextBox _offsetBox;

    public JumpToOffsetDialog()
    {
        Title           = ParsedFieldsResources.ParsedFields_JumpToOffset;
        Width           = 320;
        Height          = 140;
        ResizeMode      = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar   = false;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text   = "Offset (hex 0x… or decimal):",
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(label, 0);

        _offsetBox = new TextBox
        {
            Text    = "0x",
            Margin  = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(4, 2, 4, 2),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _offsetBox.SelectAll();
        Grid.SetRow(_offsetBox, 1);

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var ok = new Button { Content = "OK", Width = 72, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) =>
        {
            if (TryParseOffset(_offsetBox.Text.Trim(), out _))
                DialogResult = true;
            else
                MessageBox.Show(ParsedFieldsResources.ParsedFields_Error_InvalidOffset,
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        var cancel = new Button { Content = "Cancel", Width = 72, IsCancel = true };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);

        grid.Children.Add(label);
        grid.Children.Add(_offsetBox);
        grid.Children.Add(buttons);
        Content = grid;

        Loaded += (_, _) => _offsetBox.Focus();
    }

    /// <summary>The parsed offset (valid only when <see cref="Window.ShowDialog"/> returns true).</summary>
    public long Offset
    {
        get
        {
            TryParseOffset(_offsetBox.Text.Trim(), out var value);
            return value;
        }
    }

    private static bool TryParseOffset(string text, out long value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);

        return long.TryParse(text, out value);
    }
}
