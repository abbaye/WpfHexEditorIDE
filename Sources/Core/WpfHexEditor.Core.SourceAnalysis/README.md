# WpfHexEditor.Core.SourceAnalysis

**Type:** Class Library (`net8.0` — pure BCL, no WPF, no Windows dependency)
**Role:** Lightweight regex-based source outline engine for `.cs` and `.xaml` files.

---

## Responsibility

Provides a fast, BCL-only code structure outline (types, members, named elements) without Roslyn, supporting the navigation bar and outline panel in the code editor. Results are cached by file path + last-write-time for performance.

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `SourceOutlineEngine` | Sealed BCL-only regex line-scanner; parses `.cs` (types + members) and `.xaml` (`x:Class` + `x:Name` elements); caches by `(filePath, lastWriteTime)` |

---

## Key Interface

| Interface | Contract |
|-----------|---------|
| `ISourceOutlineService` | `CanOutline(path) → bool` · `GetOutlineAsync(path, ct) → Task<SourceOutlineModel?>` · `Invalidate(path)` |

---

## Models

| Model | Fields |
|-------|--------|
| `SourceOutlineModel` | FilePath, Kind, XamlClass?, XamlElements[], Types[], ParsedAt |
| `SourceTypeModel` | Name, Kind, LineNumber, IsPublic, IsAbstract, IsStatic, Members[], NestedTypes[] |
| `SourceMemberModel` | Name, ReturnType, Kind, LineNumber, IsPublic, IsStatic, IsOverride, IsAsync |
| `XamlNamedElement` | Name, TypeHint (element tag), LineNumber |

### Enums

| Enum | Values |
|------|--------|
| `SourceFileKind` | CSharp · Xaml |
| `SourceTypeKind` | Class · Struct · Interface · Enum · Record · RecordStruct |
| `SourceMemberKind` | Constructor · Method · Property · Field · Event |

---

## Parser Details

**C# Parser:**
- Pre-compiled regex patterns for type + member declarations
- Brace-depth stack for nested type tracking
- Heuristic extraction (acceptable minor inaccuracy for outline view — no Roslyn overhead)
- Handles: `class`, `struct`, `interface`, `enum`, `record`, `record struct`, methods, properties, fields, events, constructors

**XAML Parser:**
- Extracts `x:Class` attribute (code-behind class name)
- Extracts all `x:Name` attributes with element tag as type hint

**Caching:**
- `ConcurrentDictionary<(string path, DateTime writeTime), SourceOutlineModel>`
- `Invalidate(path)` removes entry; `GetOutlineAsync()` checks file stat before re-parsing
- Parse runs on ThreadPool via `Task.Run()`

---

## Dependencies

None — pure BCL (`System.Text.RegularExpressions`, `System.Collections.Concurrent`, `System.IO`).

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Service with cache** | `SourceOutlineEngine` (ConcurrentDictionary cache) |
| **Heuristic parser** | Regex-based outline extraction |
