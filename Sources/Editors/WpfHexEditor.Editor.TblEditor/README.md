# WpfHexEditor.Editor.TblEditor

Character-table (TBL / TBLX) editor for [WpfHexEditor](https://github.com/abbaye/WpfHexEditorIDE), targeting ROM translation and custom-encoding workflows.

## Features

- **Full `IDocumentEditor` + `IOpenableDocument`** integration (Undo/Redo, Save, Dirty tracking)
- **`IDiagnosticSource`** — live validation with line-level diagnostics surfaced in the Error List panel
- **`IStatusBarContributor`** — entry count, conflict count, and encoding info in the status bar
- **`IEditorToolbarContributor`** — TBL-specific toolbar items injected into the host toolbar
- **`ISearchTarget`** — in-editor search with type filter (`DteType`) and "Conflicts only" checkbox
- **`IFileValidator`** — headless file validation via `TblRepairService`; safe for background threads
- **`IPropertyProviderSource`** — exposes entry statistics in the Properties panel (F4)
- **TBL services**: `TblValidationService`, `TblConflictAnalyzer`, `TblRepairService`, `TblExportService`, `TblImportService`, `TblGeneratorService`, `TblTemplateService`, `TblSearchService`
- **TBLX support** via `TblxService` (extended format with metadata)
- **`IEditorFactory`** registration for `.tbl` and `.tblx` files

## Standalone Usage

```csharp
using WpfHexEditor.Editor.TblEditor;
using WpfHexEditor.Editor.Core;

// Register for automatic open-by-extension
EditorRegistry.Instance.Register(new TblEditorFactory());

// Or create and open directly:
var editor = new WpfHexEditor.Editor.TblEditor.Controls.TblEditor();
await ((IOpenableDocument)editor).OpenAsync("path/to/table.tbl");
myGrid.Children.Add(editor);

// Headless validation (no UI):
var diagnostics = await new TblEditorFactory().ValidateAsync("path/to/table.tbl");
```

## License

GNU Affero General Public License v3.0
