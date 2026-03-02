# WpfHexEditor.Editor.AudioViewer

Audio waveform viewer and player implementing `IDocumentEditor` + `IOpenableDocument`.

## Supported file types

`.wav`, `.mp3`, `.ogg`, `.flac`, `.xm`, `.mod`, `.it`, `.s3m`, `.aiff`

## Status

**Stub** — structure and factory registered; waveform rendering and NAudio playback to be wired in a future sprint.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new AudioViewerFactory());

var editor = new AudioViewer();
await editor.OpenAsync("path/to/sound.wav");
myGrid.Children.Add(editor);
```

## License

Apache 2.0 — see repository root.
