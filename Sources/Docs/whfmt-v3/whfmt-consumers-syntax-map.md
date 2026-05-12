# P0.3 — whfmt Consumers: syntaxDefinition Map
Generated: 2026-05-11

---

## Consumer: LanguageDefinitionSerializer
**File**: `Sources/Core/WpfHexEditor.Core.ProjectSystem/Languages/LanguageDefinitionSerializer.cs`
**Entry point**: `ParseSyntaxDefinitionBlock(syntaxJson, formatName, extensions, preferredEditor)`
**Called by**: `EmbeddedFormatCatalog.GetSyntaxDefinitionJson()` → `MainWindow` / `LanguageRegistry`

### syntaxDefinition fields consumed by LanguageDefinitionSerializer

| Field | Mapped to LanguageDefinition | Notes |
|---|---|---|
| `id` | `LanguageDefinition.Id` | Falls back to formatName |
| `name` | `LanguageDefinition.Name` | Falls back to formatName |
| `extensions[]` | `LanguageDefinition.Extensions` | Overrides parent .whfmt extensions |
| `diagnosticPrefix` | `LanguageDefinition.DiagnosticPrefix` | "CS", "VB" etc. |
| `lineCommentPrefix` | `LanguageDefinition.LineCommentPrefix` | |
| `blockCommentStart` | `LanguageDefinition.BlockCommentStart` | |
| `blockCommentEnd` | `LanguageDefinition.BlockCommentEnd` | |
| `enableInlineHints` | `LanguageDefinition.EnableInlineHints` | bool |
| `enableCtrlClickNavigation` | `LanguageDefinition.EnableCtrlClickNavigation` | bool |
| `rules[].type` | → MapKind() → `SyntaxRule.Kind` | 60+ recognized type strings |
| `rules[].pattern` | `SyntaxRule.Pattern` | Regex |
| `rules[].colorKey` | **NOT mapped** — colorKey is in DTO but not projected to model | GAP |
| `snippets[].trigger` | `SnippetDefinition.Trigger` | |
| `snippets[].body` | `SnippetDefinition.Body` | |
| `snippets[].description` | `SnippetDefinition.Description` | |
| `foldingRules.startPatterns[]` | `FoldingRules.StartPatterns` | |
| `foldingRules.endPatterns[]` | `FoldingRules.EndPatterns` | |
| `foldingRules.namedRegionStart` | `FoldingRules.NamedRegionStartPattern` | |
| `foldingRules.namedRegionEnd` | `FoldingRules.NamedRegionEndPattern` | |
| `foldingRules.indentBased` | `FoldingRules.IndentBased` | |
| `foldingRules.blockStartPattern` | `FoldingRules.BlockStartPattern` | |
| `foldingRules.tagBased` | `FoldingRules.TagBased` | |
| `foldingRules.selfClosingTags[]` | `FoldingRules.SelfClosingTags` | |
| `foldingRules.multilineTagSupport` | `FoldingRules.MultilineTagSupport` | |
| `foldingRules.headingBased` | `FoldingRules.HeadingBased` | |
| `foldingRules.minHeadingLevel` | `FoldingRules.MinHeadingLevel` | |
| `foldingRules.endOfBlockHint.*` | `FoldingRules.EndOfBlockHint` | |
| `breakpointRules.nonExecutablePatterns[]` | `BreakpointRules.NonExecutablePatterns` | |
| `breakpointRules.statementContinuationPatterns[]` | `BreakpointRules.StatementContinuationPatterns` | |
| `bracketPairs[].open/close` | `BracketPair[]` | |
| `columnRulers[]` | `LanguageDefinition.ColumnRulers` | int[] |
| `formattingRules.indentSize` | `FormattingRules.IndentSize` | |
| `formattingRules.useTabs` | `FormattingRules.UseTabs` | |
| `formattingRules.trimTrailingWhitespace` | `FormattingRules.TrimTrailingWhitespace` | |
| `formattingRules.insertFinalNewline` | `FormattingRules.InsertFinalNewline` | |
| `formattingRules.lineEnding` | `FormattingRules.LineEnding` | |
| `formattingRules.braceStyle` | `FormattingRules.BraceStyle` | |
| `formattingRules.spaceAfterKeywords` | `FormattingRules.*` | |
| `formattingRules.organizeImports` | `FormattingRules.OrganizeImports` | |
| `formattingRules.separateSystemImports` | `FormattingRules.SeparateSystemImports` | |
| `formattingRules.maxLineLength` | `FormattingRules.MaxLineLength` | |
| `formattingRules.supportedRules[]` | `FormattingRules.SupportedRules` | |
| `colorLiteralPatterns[]` | `LanguageDefinition.ColorLiteralPatterns` | Regex[] |
| `previewSnippet` | `LanguageDefinition.PreviewSnippet` | |
| `previewSamples.*` | `LanguageDefinition.PreviewSamples` | Dict |
| `ideMetadata.isSourceFile` | `LanguageDefinition.IsSourceFile` | |
| `ideMetadata.isStructuredDataFile` | `LanguageDefinition.IsStructuredDataFile` | |
| `ideMetadata.isSolutionFile` | `LanguageDefinition.IsSolutionFile` | |
| `ideMetadata.isProjectFile` | `LanguageDefinition.IsProjectFile` | |
| `ideMetadata.supportsClassDiagram` | `LanguageDefinition.SupportsClassDiagram` | |
| `ideMetadata.supportsSourceOutline` | `LanguageDefinition.SupportsSourceOutline` | |
| `ideMetadata.isProjectLanguage` | `LanguageDefinition.IsProjectLanguage` | |
| `ideMetadata.languageColor` | `LanguageDefinition.LanguageColor` | |
| `ideMetadata.aliases[]` | `LanguageDefinition.Aliases` | |
| `ideMetadata.iconGlyph` | `LanguageDefinition.IconGlyph` | |
| `ideMetadata.diffMode` | `LanguageDefinition.IdeDiffMode` | |
| `scriptGlobals` | `LanguageDefinition.ScriptGlobals` | |
| `includes[]` | `LanguageDefinition.Includes` | |

### syntaxDefinition fields NOT consumed (gaps)

| Field | Status |
|---|---|
| `rules[].colorKey` | In DTO but NOT projected to domain model — coloring uses `kind` enum only |
| `keywords[]` (if present) | Not in DTO — completions only if in `snippets[]` |
| `completions[]` | Not in DTO |
| Any standalone `keywords` array | Not mapped |

---

## Consumer: CodeEditor.RegexSyntaxHighlighter
**File**: `Sources/Editors/WpfHexEditor.Editor.CodeEditor/Helpers/RegexSyntaxHighlighter.cs`
**Reads from**: `LanguageDefinition.SyntaxRules` (already deserialized — does NOT re-read JSON)
**Uses**: `SyntaxRule.Pattern` (regex) + `SyntaxRule.Kind` (for color lookup)

## Consumer: CodeEditor.BracketMatchingService
**Reads from**: `LanguageDefinition.BracketPairs` (already deserialized)

## Consumer: CodeEditor.ColorLiteralDetector
**Reads from**: `LanguageDefinition.ColorLiteralPatterns` (already deserialized)

## Consumer: LanguageDefinitionManager (Core.LSP)
**File**: `Sources/Core/WpfHexEditor.Core.LSP/LanguageDefinitionManager.cs`
**Purpose**: Registry + bridge between .whfmt catalog and LSP/Roslyn

## Consumer: SyntaxDefinitionCatalog (TextEditor)
**File**: `Sources/Editors/WpfHexEditor.Editor.TextEditor/Services/SyntaxDefinitionCatalog.cs`
**Purpose**: Catalog for text editor mode syntax definitions

---

## Standalone CodeEditor gaps (mode: no Roslyn, .whfmt only)

| Feature | Roslyn mode | Standalone (.whfmt) | Gap |
|---|---|---|---|
| Syntax highlighting | Full semantic | Regex rules only | Limited — no type inference |
| IntelliSense / completions | Full symbol completion | `snippets[]` only | No symbol-aware completion |
| Inline hints / CodeLens | Roslyn provides | Not implemented | GAP — `enableInlineHints` flag set but no data |
| Go-to-definition (Ctrl+Click) | Roslyn provides | Not implemented | GAP — `enableCtrlClickNavigation` set but no resolver |
| Diagnostics | Roslyn CS/VB errors | Not implemented | GAP — `diagnosticPrefix` declared but no engine |
| Formatting | EditorConfig rules | `formattingRules` mapped | Partial — only `supportedRules` subset active |
| Folding | Pattern-based | `foldingRules` fully mapped | OK |
| Bracket matching | Both | `bracketPairs` mapped | OK |
| Breakpoints | IDE debugger | `breakpointRules` mapped | OK (layout only, no execution) |
| Color swatches | Both | `colorLiteralPatterns` mapped | OK |
| Column rulers | Both | `columnRulers` mapped | OK |
| Class diagram | Roslyn only | `ideMetadata.supportsClassDiagram` declared | GAP — no whfmt-based diagram |
| Source outline | Roslyn only | `ideMetadata.supportsSourceOutline` declared | GAP — no whfmt-based outline |
