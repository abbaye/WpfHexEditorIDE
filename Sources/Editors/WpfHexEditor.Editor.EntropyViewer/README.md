# WpfHexEditor.Editor.EntropyViewer

Entropy and byte-distribution analyser for binary files in [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE), visualising Shannon entropy and frequency histograms to detect compressed or encrypted regions.

## Features

- **`IDocumentEditor` + `IOpenableDocument`** integration — read-only, no dirty state
- **Per-block Shannon entropy chart** — canvas bar chart; configurable window size (256 B to 65 536 B); color-coded green (low entropy / structured) to red (high entropy / compressed or encrypted)
- **Byte-frequency histogram** — 256-bar canvas chart; accented with the active theme's `AccentColor` brush
- **`DataStatisticsService`** backend — computes overall entropy, estimated data type, unique byte count, null byte %, printable ASCII %, most common byte
- **Mouse hover tooltip** — shows block index, file offset, and entropy value
- **Stats text panel** — human-readable summary of all metrics
- **`IEditorFactory`** registration — opened from `Tools` menu, not by file extension (avoids intercepting files intended for the hex editor)

## Standalone Usage

```csharp
using WpfHexEditor.Editor.EntropyViewer;
using WpfHexEditor.Editor.EntropyViewer.Controls;
using WpfHexEditor.Editor.Core;

// Register the factory (required for IDE integration)
EditorRegistry.Instance.Register(new EntropyViewerFactory());

// Or create and open directly:
var viewer = new EntropyViewer();
await ((IOpenableDocument)viewer).OpenAsync("path/to/firmware.bin");
myGrid.Children.Add(viewer);
```

## License

GNU Affero General Public License v3.0
