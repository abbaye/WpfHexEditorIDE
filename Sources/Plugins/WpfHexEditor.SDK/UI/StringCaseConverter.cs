// Project: WpfHexEditor.SDK
// File: UI/StringCaseConverter.cs
// Description: IValueConverter that transforms string casing (Upper, Lower, Title).
//              Use in any plugin XAML via {x:Static sdk:StringCaseConverter.Upper}.

using System.Globalization;
using System.Windows.Data;

namespace WpfHexEditor.SDK.UI;

public enum StringCase { Upper, Lower, Title }

[ValueConversion(typeof(string), typeof(string))]
public sealed class StringCaseConverter : IValueConverter
{
    public static readonly StringCaseConverter Upper = new() { Case = StringCase.Upper };
    public static readonly StringCaseConverter Lower = new() { Case = StringCase.Lower };
    public static readonly StringCaseConverter Title = new() { Case = StringCase.Title };

    public StringCase Case { get; init; } = StringCase.Upper;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return value;
        return Case switch
        {
            StringCase.Upper => s.ToUpperInvariant(),
            StringCase.Lower => s.ToLowerInvariant(),
            StringCase.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()),
            _                => s
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
