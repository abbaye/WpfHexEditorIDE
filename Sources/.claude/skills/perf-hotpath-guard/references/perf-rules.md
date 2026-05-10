# Perf rules

Each rule fires only inside a hot-method body (see `hot-methods.md`).

| Rule                      | Pattern (regex)                                                | Why                                                                  |
|---------------------------|----------------------------------------------------------------|----------------------------------------------------------------------|
| `alloc-in-render`         | `\bnew\s+[A-Z]\w*\s*\(` (excluding `Span`, `StringBuilder`, common exceptions) | Each allocation is paid every frame. Pool or reuse.       |
| `linq-in-hot`             | `\.(ToList\|ToArray\|Where\|Select\|OrderBy\|GroupBy\|Aggregate\|Any\|All)\(` | Delegate creation + iterator + array allocations.        |
| `string-concat-loop`      | `+=` on string OR `"..." +` inside a `for/foreach/while/do`    | O(n^2) allocations. Use `StringBuilder` or `string.Create`.          |
| `boxing` (manual review)  | struct passed where `object` expected                          | Boxing allocates; flagged by reviewer not regex.                     |
| `dispatcher-cosmetic`     | `\bDispatcher\.BeginInvoke\b`                                  | Anti-pattern documented in `feedback_wordwrap_column`. Fix the layout root cause, not the symptom. |
| `formatted-text-recreate` | `\bnew\s+FormattedText\s*\(`                                   | Very expensive — pool per-style. See `bug_doc_editor_caret_hittest`. |
| `glyph-run-recreate`      | `\bnew\s+GlyphRun\s*\(`                                        | Each `GlyphRun` rebuilds shaping; cache by run-key.                  |
| `findvisualchild-render`  | `\bVisualTreeHelper\.(GetChild\|GetParent\|GetChildrenCount)\b` | Tree walk = O(n) per call; cache references at Loaded.              |
| `regex-in-render`         | `\bnew\s+Regex\s*\(\|Regex\.Match\s*\(\|Regex\.IsMatch\s*\(`   | Compile cost. Use a `static readonly Regex`.                         |

## Suppressions

- `// alloc-ok: <reason>` on the same line silences `alloc-in-render` only.
- `// COLD` above a method excludes the entire body.
- For other rules: if the firing is intentional, refactor or document. The
  skill does not provide a blanket-suppress comment to keep noise high
  enough to surface real regressions.

## Why these specifically

These are the patterns the project has paid for in past incidents
(`adr_006_zoom_rendering`, `bug_drag_scroll_fix`, `feedback_wordwrap_column`,
`bug_doc_editor_caret_hittest`). The list is intentionally short; broader
analysis stays with `CodeAnalysisRunner` (Halstead, LCOM, Roslyn diagnostics).

## Updating

- New incident teaches a new pattern → add a row here AND extend
  `hotpath-scan.ps1`'s `$rules` array.
- The `boxing` row is review-only because reliable detection requires Roslyn
  semantic analysis, which is out of scope for a regex skill.
