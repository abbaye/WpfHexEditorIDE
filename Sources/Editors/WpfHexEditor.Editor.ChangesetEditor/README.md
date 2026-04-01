# WpfHexEditor.Editor.ChangesetEditor

Viewer and editor for `.whchg` companion changeset files in [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE), displaying pending byte modifications produced by the hex editor.

## Features

- **`IDocumentEditor` + `IOpenableDocument`** integration — opens `.whchg` files by extension
- **`IEditorToolbarContributor`** — injects "Apply to Disk" and "Discard" toolbar items into the host toolbar (no embedded toolbar in the control itself)
- **Three virtualized DataGrid tabs** — Modified, Inserted, and Deleted entries, with live tab header counts
- **`ChangesetEditorViewModel`** — orchestrates `ModifiedEntries`, `InsertedEntries`, and `DeletedRanges` observable collections
- **`ChangesetSerializer`** — async binary deserialization of `.whchg` files
- **Source hash display** — companion file integrity hash shown in the status bar
- **Dirty tracking** with `*` title suffix
- **`IEditorFactory`** registration for `.whchg` files

## Standalone Usage

```csharp
using WpfHexEditor.Editor.ChangesetEditor;
using WpfHexEditor.Editor.Core;

// Register for automatic open-by-extension
EditorRegistry.Instance.Register(new ChangesetEditorFactory());

// Or create and open directly:
var editor = new WpfHexEditor.Editor.ChangesetEditor.Controls.ChangesetEditorControl();
await ((IOpenableDocument)editor).OpenAsync("path/to/myfile.whchg");
myGrid.Children.Add(editor);
```

## License

GNU Affero General Public License v3.0
