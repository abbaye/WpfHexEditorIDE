# WpfHexEditor.LSP

**Type:** Class Library (`net8.0-windows`, UseWPF: false)
**Role:** Language-server-style analysis engine — multi-language tokenization, symbol resolution, incremental parsing, SmartComplete, refactoring, and diagnostics.

---

## Responsibility

Provides language intelligence features to the code editor without a Roslyn or external LSP dependency. Driven by `.whlang` JSON language definitions.

---

## Architecture

### Parser Pipeline

```
LanguageDefinition (.whlang JSON)
        ↓
    Lexer  (tokenize lines)
        ↓
IncrementalParser  (per-document parse state)
        ↓
SymbolTableManager  (extract symbols)
        ↓
DiagnosticsEngine  (apply IDiagnosticRule instances)
```

---

## Key Classes

### Language Loading

| Class | Responsibility |
|-------|---------------|
| `LanguageDefinitionManager` | Registry + factory for language definitions; lazy-loads built-in `.whlang` from `WpfHexEditor.Definitions`; priority system: UserCreated > Imported > BuiltIn |

### Parsing & Tokenization

| Class | Responsibility |
|-------|---------------|
| `Lexer` | Line-by-line tokenizer driven by LanguageDefinition; maintains block-comment state across lines; pre-compiles regex rules (Lazy<>) |
| `Token` | Immutable record — TokenType, Text, StartColumn, Line |
| `ParseResult` | Tokens by line, diagnostics, updated line ranges |
| `IncrementalParser` | Per-document incremental parse state (stub — ready for full implementation) |

### Symbol System

| Class | Responsibility |
|-------|---------------|
| `Symbol` | Immutable — Name, Kind, FilePath, Line, Column |
| `SymbolTable` | Per-document symbol store; FindByName(), FindDefinition(), GetInScope() |
| `SymbolTableManager` | Workspace-level registry; thread-safe; FindWorkspaceSymbol(), GetAllSymbolNames(), FindAllReferences() |
| `WorkspaceSymbolTableManager` | Aggregated cross-file queries for SmartComplete and refactoring |

### SmartComplete

| Class | Responsibility |
|-------|---------------|
| `BoostedSmartCompleteManager` | Aggregates keywords + workspace symbols + snippets; priority-ranked (keywords > local > cross-file > snippets) |
| `CompletionItem` | Label, CompletionKind, InsertText, Detail, SortPriority |
| `HoverProvider` | Quick-info text for identifiers on hover |
| `LspSnippetsManager` | Per-language snippet catalog |

### Navigation & Refactoring

| Class | Responsibility |
|-------|---------------|
| `NavigationProvider` | Go-to-definition / find-references locations |
| `RefactoringEngine` | Registry of `IRefactoring` implementations; GetAvailable(context) |
| `RenameRefactoring` | Workspace-wide symbol rename |
| `ExtractMethodRefactoring` | Extract code block into new method |
| `CodeFormatter` | Language-aware document formatting |

### Diagnostics

| Class | Responsibility |
|-------|---------------|
| `DiagnosticsEngine` | Applies registered `IDiagnosticRule` instances on background thread |

### Integration Bridges

| Class | Responsibility |
|-------|---------------|
| `EventBusIntegration` | Subscribes to FileOpened/DocumentSaved events → invalidates caches |
| `CommandIntegration` | Exposes refactoring commands to IDE menus via IIDEEventBus |
| `FoldingManagerIntegration` | Provides folding regions from parse results to code editor |

---

## Key Models

| Model | Key Fields |
|-------|-----------|
| `LanguageDefinition` | Id, Name, Category, Priority, Extensions[], LineComment, BlockComment*, Rules[], Keywords[], Operators[] |
| `LanguageRule` | Type, Pattern (regex), ColorKey, IsBold, IsItalic |
| `TokenType` | Keyword / Identifier / Type / Number / String / Comment / Operator / Preprocessor / Punctuation / Unknown |
| `SymbolKind` | Class / Interface / Struct / Enum / Function / Method / Property / Field / Event / … |
| `RefactoringContext` | FilePath, CursorLine, CursorColumn, SelectedText |

---

## Key Interfaces

| Interface | Contract |
|-----------|---------|
| `IRefactoring` | `CanApply(context) → bool` · `Apply(context) → Task` |
| `IDiagnosticRule` | `LanguageId` · `Evaluate(text, filePath) → IReadOnlyList<DiagnosticEntry>` |
| `ILspClient` | SDK contract for external LSP client integration |

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.Editor.Core` | `IDocumentEditor` types |
| `WpfHexEditor.Editor.CodeEditor` | CodeEditor models, folding |
| `WpfHexEditor.SDK` | Extension points, ILspClient |
| `WpfHexEditor.Definitions` | Built-in `.whlang` language definitions |
| `WpfHexEditor.Events` | IDE event subscriptions |

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Strategy** | `Lexer` + `LanguageDefinition` (swappable per language) |
| **Registry** | `LanguageDefinitionManager`, `RefactoringEngine` |
| **Pipeline** | `DiagnosticsEngine` rule pipeline |
| **Factory** | `LanguageDefinitionManager.Create()` |
| **Aggregator** | `BoostedSmartCompleteManager` merges multiple sources |
