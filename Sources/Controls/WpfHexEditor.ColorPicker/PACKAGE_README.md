# WpfColorPicker

A modern, full-featured WPF Color Picker UserControl for .NET 8.


> **Full documentation**: [WpfColorPicker-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Controls/WpfHexEditor.ColorPicker/WpfColorPicker-guide.md) — API reference, architecture, integration guides, and usage examples.

## What's New in 2.0.2

- **+10 UI localizations** added — uk-UA, cs-CZ, vi-VN, hu-HU, ro-RO, id-ID, th-TH, el-GR, da-DK, fi-FI — reaching 28 satellite resource locales (Phase 6 wave).
- **Self-contained NuGet localization fix** — subclass resources are now loaded correctly when the package is consumed standalone (Phase 5 fix for the constructor-ordering bug that prevented satellite assemblies from resolving).
- **`NeutralLanguage=en-US`** declared so `ResourceManager` no longer warns at build time.
- **No public API changes** — drop-in upgrade from 2.0.1.

## What's New in 2.0.1

- **HSV color wheel + RGB/HSL sliders + opacity + recent colors + eyedropper** — full feature set wired with `DynamicResource` theming so the picker follows the host app's theme.

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
