# CLAUDE.MEMORY.MD — WpfHexEditor Studio

## Architecture
- App (WpfHexEditor.App) → Shell + Docking
- Editors → independent modules
- Plugins → reloadable / isolated
- Services → shared logic
- Infrastructure → IO, persistence

## Key Decisions (ADR)
- ADR-001 — Plugin System: hybrid in-process + sandbox model (performance vs isolation)
- ADR-002 — Large File Handling: streaming + chunk-based, no full memory load, supports GB-scale
- ADR-003 — UI Architecture: strict MVVM, all panels dockable + themed

## Constraints
- UI must remain responsive. No blocking on UI thread.
- No memory leaks from plugins. Theme consistency required.
- SOLID mandatory. Composition > inheritance. 1 responsibility per file.

## Evolution Log (append-only)
Format: `Date | Change | Impact | Risk`
