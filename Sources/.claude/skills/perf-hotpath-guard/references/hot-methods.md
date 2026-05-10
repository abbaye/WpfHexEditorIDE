# Hot methods catalog

Methods that run every frame, every layout pass, or every input event.
`hotpath-scan.ps1` scans only the bodies of methods matching this list.

## Core WPF lifecycle (always hot)

- `OnRender(DrawingContext)` — runs every frame the visual is invalidated.
- `MeasureOverride(Size)` — runs on every layout pass.
- `ArrangeOverride(Size)` — runs on every layout pass.
- `OnPreviewMouseMove` — runs on every mouse move event.
- `OnScrollChanged` — runs on every scroll delta.

## Project-specific renderers (hot)

- `RenderGlyphLine` — CodeEditor per-line glyph emission.
- `DrawByte` / `RenderByte` — HexEditor cell rendering.
- `PaintViewport` / `RenderViewport` — viewport composition.
- `RenderTextRun` — text shaping.
- Any method named `Render*`, `Draw*`, `Paint*`, `Layout*`, `Compose*`,
  `Build*`, `Emit*` inside a `*Renderer.cs`, `*Viewport.cs`, or `*GlyphRun.cs`
  file.

## Convention annotations

- `// HOT` immediately above a method body forces hot scanning.
- `// COLD` immediately above a method body excludes it.
- `// alloc-ok: <reason>` inline silences `alloc-in-render` for that line.

## NOT hot (never scanned)

- Anything in `*Test*`, `*Mock*`, `*Designer*`, `*.g.cs`, `*.g.i.cs`.
- Constructors (`.ctor`).
- Property accessors that just return a backing field.
- Async one-shot methods (`async Task LoadAsync` etc.) unless explicitly
  annotated `// HOT`.

## Why these methods specifically

Memory anchors:
- `adr_006_zoom_rendering` — CodeEditor render path is snap-to-pixel in
  `GlyphRunRenderer`; allocations there cost double under transform.
- `adr_hexeditor_viewport_race` — viewport methods may run before layout is
  complete; defensive allocations multiply.
- `bug_drag_scroll_fix` — auto-scroll runs in `OnPreviewMouseMove`; any LINQ
  or allocation there is paid every pixel of drag.
- `feedback_wordwrap_column` — `MeasureOverride` is the wrong place for
  Dispatcher gymnastics.
- `bug_doc_editor_caret_hittest` — `FormattedText` and `GlyphRun` differ in
  wrapping; recreating either per-frame is the cost trap.
