// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: PathToFilenameConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF IValueConverter that extracts the filename (with extension) from a
//     full file path string. Used in bindings where only the file name should
//     be displayed rather than the full path.
//
// Architecture Notes:
//     Stateless sealed converter using System.IO.Path.GetFileName.
//
// ==========================================================

using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace WpfHexEditor.HexEditor.Converters
{
    /// <summary>
    /// Used to get the filename with extention.
    /// </summary>
    public sealed class PathToFilenameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is string filename)
                ? Path.GetFileName(filename)
                : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }
}
