# WpfHexEditor.ColorPicker

> Compact WPF color picker UserControl — RGB / HSV sliders, hex input, and opacity control. Zero third-party dependencies.

[![.NET](https://img.shields.io/badge/.NET-net48%20%7C%20net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](../../LICENSE)

---

## Architecture

```mermaid
graph TB
    subgraph ColorPicker["WpfHexEditor.ColorPicker"]
        CP["ColorPicker (UserControl)\nMain entry point"]

        subgraph CTRL["Controls/"]
            CC["ColorCanvas\n(2D hue/saturation picker)"]
            CS["ColorSlider\n(value / opacity sliders)"]
            HCI["HexColorInput\n(#RRGGBB text input)"]
            SB["SpectrumSlider\n(hue rainbow)"]
        end

        subgraph CNV["Converters/"]
            C2B["ColorToSolidBrushConverter"]
            H2C["HsvToColorConverter"]
            D2C["DoubleToColorConverter"]
        end

        subgraph HLP["Helpers/"]
            CH["ColorHelper\n(HSV ↔ RGB math)"]
        end
    end

    CP --> CC & CS & HCI & SB
    CC --> CH
    CS --> CH
    CP --> CNV

    style CP fill:#e8f5e9,stroke:#388e3c,stroke-width:3px
    style CC fill:#f1f8e9,stroke:#558b2f,stroke-width:2px
    style CH fill:#fff8e1,stroke:#f57c00,stroke-width:2px
```

---

## Project Structure

```
WpfHexEditor.ColorPicker/
├── Controls/
│   ├── ColorPicker.xaml(.cs)      ← Main UserControl
│   ├── ColorCanvas.xaml(.cs)      ← 2D saturation/value picker
│   ├── SpectrumSlider.xaml(.cs)   ← Hue spectrum strip
│   ├── ColorSlider.xaml(.cs)      ← Generic color channel slider
│   └── HexColorInput.xaml(.cs)   ← Hex string input (#RRGGBB / #AARRGGBB)
│
├── Converters/
│   ├── ColorToSolidBrushConverter.cs
│   ├── HsvToColorConverter.cs
│   └── DoubleToColorConverter.cs
│
└── Helpers/
    └── ColorHelper.cs             ← HSV ↔ RGB conversion math
```

---

## Features

| Feature | Description |
|---------|-------------|
| **2D picker** | `ColorCanvas` — click to select hue+saturation in a 2D square |
| **Spectrum slider** | Hue rainbow strip (0–360°) |
| **Channel sliders** | Individual R, G, B, Alpha sliders |
| **Hex input** | Type `#FF6B3FA0` directly — supports ARGB and RGB formats |
| **Real-time preview** | Selected color shown as a live swatch |
| **Opacity** | Alpha channel support |
| **Theme-aware** | Inherits foreground/background from parent |
| **Multi-target** | .NET 4.8 and .NET 8.0-windows |

---

## Usage

### XAML

```xml
<Window xmlns:cp="clr-namespace:WpfHexEditor.ColorPicker.Controls;assembly=WpfHexEditor.ColorPicker">

    <cp:ColorPicker x:Name="Picker"
                    SelectedColor="{Binding MyColor, Mode=TwoWay}" />
</Window>
```

### Code-behind

```csharp
// Get/set color
Picker.SelectedColor = Colors.Purple;
Color chosen = Picker.SelectedColor;

// React to selection
Picker.SelectedColorChanged += (s, e) =>
    MyBrush = new SolidColorBrush(e.NewValue);
```

### Embedded in a dialog

```csharp
var dialog = new Window
{
    Title = "Choose color",
    Content = new ColorPicker { SelectedColor = current },
    SizeToContent = SizeToContent.WidthAndHeight,
    ResizeMode = ResizeMode.NoResize
};
if (dialog.ShowDialog() == true)
    ApplyColor(((ColorPicker)dialog.Content).SelectedColor);
```

---

## Color Model

```mermaid
flowchart LR
    HEX["#AARRGGBB\nhex string"] <--> RGB["RGB\n(byte R, G, B, A)"]
    RGB <--> HSV["HSV\n(Hue 0–360, Sat 0–1, Val 0–1)"]
    HSV --> CC["ColorCanvas position\n(X=Saturation, Y=1-Value)"]
    HSV --> SS["SpectrumSlider\n(Hue)"]
    RGB --> SLIDERS["R / G / B / A sliders"]
```

---

## Integration in WpfHexEditor

`ColorPicker` is used in:
- **HexEditor** — bookmark color picker, custom background block color selection, highlight color selection
- **ParsedFieldsPanel** — field color annotation

---

## Dependencies

`WpfHexEditor.ColorPicker` has **zero project-level dependencies**.

---

## License

Apache 2.0 — Copyright 2016–2026 Derek Tremblay. See [LICENSE](../../LICENSE).
