# WpfHexEditor.Events

**Type:** Class Library (`net8.0-windows`, UseWPF: false)
**Role:** IDE-wide event bus contracts and all well-known domain event records.

---

## Responsibility

Defines the strongly-typed, decoupled event system used by every part of the IDE and all plugins. Zero external dependencies — no WPF, no other project references — to prevent circular dependencies.

---

## Architecture

### Core Contracts

| Type | Purpose |
|------|---------|
| `IIDEEventBus` | Publish/Subscribe generic methods; exposes `IEventRegistry` |
| `IEventRegistry` | Catalog of registered event types with subscriber counts |
| `IDEEventBase` | Abstract base record for all events (EventId, Source, Timestamp, CorrelationId) |
| `IDEEventContext` | Context carried to handlers (PublisherPluginId, IsFromSandbox, CancellationToken) |

### Well-Known Events

All inherit `IDEEventBase` (immutable records):

**File / Document:**
- `FileOpenedEvent` — FilePath, FileExtension, FileSize
- `DocumentSavedEvent` — FilePath
- `FileClosedEvent` — FilePath
- `WorkspaceChangedEvent` — WorkspacePath, PreviousWorkspacePath

**Editor:**
- `EditorSelectionChangedEvent` — FilePath, SelectionStart, SelectionLength
- `CodeEditorCursorMovedEvent` — FilePath, Line, Column
- `CodeEditorTextSelectionChangedEvent` — FilePath, SelectedText (max 4096), SelectionStart, SelectionLength
- `CodeEditorDocumentOpenedEvent` — FilePath, LanguageId
- `CodeEditorDocumentClosedEvent` — FilePath
- `CodeEditorDiagnosticsUpdatedEvent` — FilePath, ErrorCount, WarningCount
- `CodeEditorCommandExecutedEvent` — FilePath, CommandName
- `CodeEditorFoldingChangedEvent` — FilePath, CollapsedCount

**Build:**
- `BuildStartedEvent` — ProjectPath, Configuration, StartedAt
- `BuildSucceededEvent` — ProjectPath, Duration, StartedAt, WarningCount, counts
- `BuildFailedEvent` — ProjectPath, ErrorMessage, Duration, ErrorCount, Warnings, counts
- `BuildCancelledEvent`
- `BuildOutputLineEvent` — Line
- `BuildProgressUpdatedEvent` — ProgressPercent, StatusText

**Plugin:**
- `PluginLoadedEvent` — PluginId, Version
- `PluginUnloadedEvent` — PluginId

**Terminal:**
- `TerminalCommandExecutedEvent` — CommandName, Output

---

## Design Rules

- **Leaf node** — no ProjectReferences; importing this project never introduces cycles
- All events are C# `record` types — immutable, value-comparable, thread-safe
- `CorrelationId` on `IDEEventBase` enables distributed tracing across plugin sandboxes

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Domain Event** | All `IDEEventBase` records |
| **Observer** | `IIDEEventBus` Subscribe/Publish |
| **Registry** | `IEventRegistry` subscriber catalog |
