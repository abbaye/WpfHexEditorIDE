//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: Controls/ByteValueTooltip.cs
// Description:
//     Shows a compact tooltip when hovering over a byte in the hex viewport.
//     Displays: hex, decimal, binary, ASCII, signed int8, and if 2+ bytes
//     are selected: int16/32, float, UTF-8 preview.
// Architecture:
//     Static helper — creates and populates a WPF ToolTip from byte data.
//////////////////////////////////////////////

using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>
/// Creates rich tooltips showing byte value interpretations.
/// </summary>
public static class ByteValueTooltip
{
    /// <summary>
    /// Creates a tooltip showing interpretations of the byte(s) at the given position.
    /// </summary>
    public static ToolTip Create(byte[] data, long offset, int availableBytes)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        var mono = new FontFamily("Consolas");

        int idx = (int)Math.Min(offset, data.Length - 1);
        if (idx < 0 || idx >= data.Length)
            return new ToolTip { Content = "No data" };

        byte b = data[idx];

        // Header
        panel.Children.Add(new TextBlock
        {
            Text = $"Offset 0x{offset:X}",
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
        });

        // Single byte interpretations
        AddRow(panel, mono, "Hex",     $"0x{b:X2}");
        AddRow(panel, mono, "Dec",     $"{b}");
        AddRow(panel, mono, "Signed",  $"{(sbyte)b}");
        AddRow(panel, mono, "Binary",  Convert.ToString(b, 2).PadLeft(8, '0'));
        AddRow(panel, mono, "ASCII",   b >= 0x20 && b < 0x7F ? $"'{(char)b}'" : "(non-printable)");
        AddRow(panel, mono, "Octal",   Convert.ToString(b, 8).PadLeft(3, '0'));

        // Multi-byte interpretations (if enough bytes available)
        int remaining = data.Length - idx;

        if (remaining >= 2)
        {
            panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
            short i16 = BitConverter.ToInt16(data, idx);
            ushort u16 = BitConverter.ToUInt16(data, idx);
            AddRow(panel, mono, "Int16",  $"{i16} (0x{u16:X4})");
        }

        if (remaining >= 4)
        {
            int i32 = BitConverter.ToInt32(data, idx);
            uint u32 = BitConverter.ToUInt32(data, idx);
            float f32 = BitConverter.ToSingle(data, idx);
            AddRow(panel, mono, "Int32",   $"{i32} (0x{u32:X8})");
            if (!float.IsNaN(f32) && !float.IsInfinity(f32))
                AddRow(panel, mono, "Float32", $"{f32:G6}");
        }

        if (remaining >= 8)
        {
            long i64 = BitConverter.ToInt64(data, idx);
            double f64 = BitConverter.ToDouble(data, idx);
            AddRow(panel, mono, "Int64",   $"{i64}");
            if (!double.IsNaN(f64) && !double.IsInfinity(f64))
                AddRow(panel, mono, "Float64", $"{f64:G6}");
        }

        // UTF-8 preview (up to 16 bytes)
        if (remaining >= 1)
        {
            int previewLen = Math.Min(remaining, 16);
            try
            {
                var utf8 = Encoding.UTF8.GetString(data, idx, previewLen).Replace("\0", "\\0");
                if (utf8.Length > 0)
                {
                    panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
                    AddRow(panel, mono, "UTF-8", $"\"{utf8}\"");
                }
            }
            catch { /* invalid UTF-8 sequence */ }
        }

        return new ToolTip
        {
            Content = panel,
            MaxWidth = 350,
        };
    }

    private static void AddRow(StackPanel parent, FontFamily mono, string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}:",
            Width = 55,
            FontSize = 11,
            Foreground = Brushes.Gray,
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontFamily = mono,
            FontSize = 11,
        });
        parent.Children.Add(row);
    }
}
