# P0.6 — CodeEditor Standalone Gaps
Generated: 2026-05-11

When CodeEditor runs without Roslyn (standalone mode — non-.NET files, or any file where
MSBuild workspace is unavailable), the sole source of language intelligence is the
`.whfmt`'s `syntaxDefinition` block.

---

## Feature parity matrix: Roslyn vs standalone (.whfmt)

| Feature | Roslyn mode | Standalone .whfmt | Gap |
|---|---|---|---|
| **Syntax highlighting** | Full semantic tokens | Regex rule pipeline | Acceptable (regex covers 80%) |
| **IntelliSense / completions** | Full symbol completion | `snippets[]` trigger-based only | HIGH GAP — no keyword completions, no symbol-aware |
| **Hover documentation** | Roslyn XML docs | None | GAP |
| **Go-to-definition** | Full symbol navigation | `enableCtrlClickNavigation` flag set but no resolver | HIGH GAP |
| **Find all references** | Roslyn workspace | None | GAP |
| **Inline hints (CodeLens)** | Full CodeLens | `enableInlineHints` flag set but no data | HIGH GAP |
| **Live diagnostics** | Roslyn CS/VB errors | None — `diagnosticPrefix` declared but no engine | HIGH GAP |
| **Rename** | Roslyn | None | GAP |
| **Format document** | Roslyn Formatter | `formattingRules.*` — partial subset | PARTIAL (trimWhitespace, organizeImports implemented) |
| **Code folding** | Both | `foldingRules.*` fully mapped | OK |
| **Bracket matching** | Both | `bracketPairs[]` fully mapped | OK |
| **Breakpoints (visual)** | IDE debugger | `breakpointRules.*` mapped | OK (layout only) |
| **Color swatches** | Both | `colorLiteralPatterns[]` | OK |
| **Column rulers** | Both | `columnRulers[]` | OK |
| **Class diagram** | Roslyn only | `ideMetadata.supportsClassDiagram` flag | HIGH GAP |
| **Source outline** | Roslyn only | `ideMetadata.supportsSourceOutline` flag | HIGH GAP |
| **Snippets** | Roslyn snippet provider | `snippets[]` from whfmt | OK |
| **Diagnostic prefix** | Roslyn CS0001 etc. | `diagnosticPrefix` declared only | GAP |
| **Script globals** | Roslyn scripting | `scriptGlobals` mapped | Depends on host |

---

## What whfmt v3 standalone must add for parity

### P8.1 — Keyword completion engine
**Required whfmt fields** (to add/formalize):
```json
"syntaxDefinition": {
  "completions": [
    { "label": "abstract", "kind": "keyword", "detail": "C# modifier" },
    { "label": "Task<T>", "kind": "class", "insertText": "Task<${1:T}>" }
  ]
}
```
Currently `keywords[]` and `completions[]` exist in some .whfmt files but are NOT in the DTO.
Plan: add `completions[]` to LanguageDefinitionDto + LanguageDefinition + CodeSmartCompleteProvider.

### P8.2 — Source outline / symbol list from patterns
**Required whfmt fields** (to add):
```json
"syntaxDefinition": {
  "outlineRules": [
    { "type": "class", "pattern": "^\\s*(?:public|private|internal)?\\s*class\\s+(\\w+)", "group": 1 },
    { "type": "method", "pattern": "^\\s*(?:public|private)?\\s*\\w+\\s+(\\w+)\\s*\\(", "group": 1 }
  ]
}
```

### P8.3 — Inline hints from patterns (lightweight CodeLens)
**Required whfmt fields** (to add):
```json
"syntaxDefinition": {
  "inlineHintRules": [
    { "trigger": "class\\s+(\\w+)", "hintTemplate": "({0} members)", "group": 1 }
  ]
}
```

### P8.4 — Simple diagnostics from patterns
**Required whfmt fields** (to add):
```json
"syntaxDefinition": {
  "diagnosticRules": [
    { "id": "WH0001", "pattern": "TODO\\b", "severity": "info", "message": "TODO comment" }
  ]
}
```

### P8.5 — Ctrl+Click navigation from patterns
**Required whfmt fields** (to add):
```json
"syntaxDefinition": {
  "navigationRules": [
    { "targetType": "definition", "pattern": "\\b(\\w+)\\s*(?=\\()", "group": 1 }
  ]
}
```

---

## Immediate gaps in current syntax field mapping

### Gap: `rules[].colorKey` not projected
- In DTO as `ColorKey` string
- NOT mapped to LanguageDefinition domain model
- CodeEditor uses only `SyntaxRule.Kind` (enum) for coloring via theme lookup
- **Consequence**: cannot specify custom per-rule colors beyond the 10 theme slots

### Gap: `formattingRules.*` partial implementation
- `supportedRules[]` array gates which rules are active
- Current support: `trimTrailingWhitespace`, `insertFinalNewline`, `spaceAfterKeywords`, `spaceAroundBinaryOperators`, `spaceAfterComma`, `indentCaseLabels`, `organizeImports`
- Not implemented: `braceStyle`, `blankLineBeforeMethod`, `spaceInsideParens`, `maxConsecutiveBlankLines`

---

## Priority order for standalone parity (P8 work items)

1. **P8.1** — `completions[]` in DTO + completion provider (HIGH impact, many whfmt files need updating)
2. **P8.2** — `outlineRules[]` + source outline panel (HIGH — `ideMetadata.supportsSourceOutline` already flagged)
3. **P8.3** — `inlineHintRules[]` + inline hints panel (MED)
4. **P8.4** — `diagnosticRules[]` + diagnostic engine (MED)
5. **P8.5** — `navigationRules[]` + Ctrl+Click resolver (MED)
6. **P8.6** — Fix `rules[].colorKey` projection (LOW — aesthetic only)
7. **P8.7** — Complete `formattingRules` implementation (LOW)
