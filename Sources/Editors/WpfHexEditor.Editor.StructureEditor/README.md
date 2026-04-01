# WpfHexEditor.Editor.StructureEditor

Visual editor for `.whfmt` format definition files, implementing `IDocumentEditor` + `IOpenableDocument`.

Opens a `.whfmt` JSON structure definition and presents its block list in an editable DataGrid.

## Supported file types

`.whfmt`

## Features (V1)

- **Format header**: expandable panel showing name, category, extensions, version and description (read-only).
- **Block DataGrid**: edit Type, Name, Offset, Length, ValueType, StoreAs, Color for each block.
  - Type and ValueType are combo-boxes with predefined values.
  - Color column shows a colour swatch alongside the hex string.
- **Block Description**: inline editor for the description of the selected block.
- **Add / Delete / Move Up / Move Down** toolbar buttons.
- **Save** (Ctrl+S): serializes back to camelCase JSON, preserving `null` fields.
- Dirty tracking with `*` title suffix and `ModifiedChanged` event.

## Standalone usage

```csharp
EditorRegistry.Instance.Register(new StructureEditorFactory());

var editor = new StructureEditor();
await editor.OpenAsync("path/to/MyFormat.whfmt");
myGrid.Children.Add(editor);
```

## License

GNU Affero General Public License v3.0 — see repository root.
