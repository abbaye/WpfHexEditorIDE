---
name: perf-hotpath-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  C# in render/layout/scroll hot paths (HexEditor/, CodeEditor/Rendering*,
  *Viewport*, *GlyphRun*, *Renderer*, *ScrollHandler*, OnRender,
  MeasureOverride, ArrangeOverride) to detect allocations and patterns that
  cause jank or GC pressure. Skip on: tests, mocks, designer files, non-render
  code in non-hot directories.
---

# perf-hotpath-guard (internal)

Catches the perf anti-patterns the project has historically paid for:
allocations in render loops, LINQ in measure, Dispatcher.BeginInvoke as a
cosmetic fix, FormattedText/GlyphRun rebuilt every frame, etc. Memory anchors:
`adr_006_zoom_rendering`, `adr_hexeditor_viewport_race`, `bug_drag_scroll_fix`,
`feedback_wordwrap_column`, `bug_doc_editor_caret_hittest`.

## When I invoke

Triggered by edits on C# files matching ANY of:
- Path under `HexEditor/`
- Path under `CodeEditor/Rendering` (or contains `Rendering`)
- Filename containing `Viewport`, `GlyphRun`, `Renderer`, `ScrollHandler`
- Files where the edit modifies `OnRender`, `MeasureOverride`, or
  `ArrangeOverride` (any class)

Skip if path contains: `Test`, `Mock`, `*.Designer.cs`, `*.g.cs`, `*.g.i.cs`.

## Pipeline

1. After the edit batch, gather modified `.cs` files matching trigger paths.
2. Run `scripts/hotpath-scan.ps1 -Files <paths>`.
3. The script identifies hot-method bodies (signature regex + path) and
   applies 9 rules inside those bodies only.
4. Sortie: `HotPath: <n> issues` or `OK` + per-issue lines with file:line.

## Detected rules (9)

See `references/perf-rules.md` for full table with regex and rationale.

| Rule                   | Anchor / reason                              |
|------------------------|----------------------------------------------|
| `alloc-in-render`      | GC pressure / jank                           |
| `linq-in-hot`          | allocations + delegate creation              |
| `string-concat-loop`   | O(n^2) allocations                           |
| `boxing`               | struct -> object passing                     |
| `dispatcher-cosmetic`  | `feedback_wordwrap_column` anti-pattern      |
| `formatted-text-recreate` | `bug_doc_editor_caret_hittest` cost       |
| `glyph-run-recreate`   | render cost without pool                     |
| `findvisualchild-render` | tree walk = O(n) every frame              |
| `regex-in-render`      | compile cost on hot path                     |

## Hot methods (white-list)

Maintained in `references/hot-methods.md`:
- `OnRender(DrawingContext)`
- `MeasureOverride`, `ArrangeOverride`
- `OnPreviewMouseMove`, `OnScrollChanged`
- `RenderGlyphLine`, `DrawByte`, `PaintViewport`, `RenderTextRun` (and any
  method matching `Render*`/`Draw*`/`Paint*` inside a renderer file)
- Methods preceded by a `// HOT` comment (convention)

A method preceded by `// COLD` is excluded even if it matches.

## Output format

```
HotPath: 2 issues
  HexLineRenderer.cs:142  alloc-in-render        OnRender -> new SolidColorBrush(...)
  TextViewport.cs:88      formatted-text-recreate OnRender -> new FormattedText(...)
```

## Local annotations to silence false positives

- `// alloc-ok: <reason>` on the same line silences `alloc-in-render`.
- `// COLD` above a method excludes it.
- Any other rule firing legitimately should be discussed and either fixed,
  exempted by annotation, or documented as a `// HOT-EXCEPTION:` note.

## What this skill does NOT do

- Does **not** profile actual runtime cost — it is a static guard.
- Does **not** rewrite code. Only reports.
- Does **not** check non-hot paths (covered by `code-analysis`).
