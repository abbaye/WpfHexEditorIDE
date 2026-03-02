# WpfHexEditor.Editor.ImageViewer

Read-only image viewer for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorControl) with zoom, pan, and pixel inspection.

## Features

- **Native WPF rendering** (BitmapImage) — no external dependencies
- **Zoom** (toolbar buttons, Ctrl+Wheel, Ctrl+=/-)
- **Pan** (Middle-click drag or Alt+Left-click drag)
- **Pixel inspector** — mouse hover shows RGB/ARGB values and hex code
- **Checkerboard background** for transparency visualization
- **Status bar**: dimensions, pixel format, file size, DPI
- **Copy to clipboard** via CopyCommand

## Supported Formats

`.png`, `.bmp`, `.jpg` / `.jpeg`, `.gif`, `.ico`, `.tiff`, `.webp`, `.dds`, `.tga`

## Standalone Usage

```csharp
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ImageViewer;
using WpfHexEditor.Editor.ImageViewer.Controls;

// Register with the editor registry (optional — for automatic open-by-extension)
EditorRegistry.Instance.Register(new ImageViewerFactory());

// Or create directly:
var viewer = new ImageViewer();
await viewer.OpenAsync("path/to/sprite.png");
myGrid.Children.Add(viewer);
```

## Theme Integration

The image viewer uses the `TE_Background` and `TE_LineNumberBackground` keys from the host theme for the toolbar and status bar backgrounds. These are defined in `WpfHexEditor.Editor.TextEditor/Themes/Generic.xaml` and overridden by the host application's `Colors.xaml`.

## License

MIT
