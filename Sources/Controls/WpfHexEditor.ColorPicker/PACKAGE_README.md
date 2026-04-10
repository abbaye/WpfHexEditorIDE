# WpfColorPicker

A modern, full-featured WPF Color Picker UserControl for .NET 8.

## Features

- **HSV Color Wheel** — intuitive hue/saturation selection with value slider
- **RGB / HSL Sliders** — precise numeric color adjustment
- **Hex Input** — type hex color codes directly (#RRGGBB / #AARRGGBB)
- **Opacity Support** — alpha channel slider for transparent colors
- **Standard Palettes** — predefined color sets for quick selection
- **Recent Colors** — automatically tracks recently used colors
- **Themeable** — uses WPF DynamicResources for seamless theme integration
- **Zero Dependencies** — standalone assembly, no external NuGet packages

## Quick Start

```xml
<Window xmlns:cp="clr-namespace:WpfHexEditor.ColorPicker.Controls;assembly=WpfHexEditor.ColorPicker">
    <cp:ColorPicker SelectedColor="Blue"
                    ColorChanged="ColorPicker_ColorChanged" />
</Window>
```

```csharp
private void ColorPicker_ColorChanged(object sender, Color color)
{
    // Use the selected color
    myBorder.Background = new SolidColorBrush(color);
}
```

## Theme Integration

The control uses these DynamicResources (optional — fallbacks are built-in):

| Resource Key | Usage |
|-------------|-------|
| `BorderBrush` | Control frame border |
| `SurfaceElevatedBrush` | Hex display background |
| `ForegroundBrush` | Text color |

## Installation

```
dotnet add package WpfColorPicker
```

## License

GNU AGPL v3.0 — [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
