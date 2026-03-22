\# 🧠 CLAUDE.memory.md — WpfHexEditor Studio



---



\## 1. Architecture Overview



\### Core Structure



\* App (WpfHexEditor.App) → Shell + Docking

\* Editors → Independent modules

\* Plugins → Reloadable / isolated

\* Services → Shared logic

\* Infrastructure → IO, persistence



---



\## 2. Key Decisions (ADR)



\### ADR-001 — Plugin System



\* Type: Modular / Reloadable

\* Modes:



&nbsp; \* In-process (performance)

&nbsp; \* Sandbox (isolation)



Decision:

→ Hybrid model to balance safety vs performance



---



\### ADR-002 — Large File Handling



Decision:

→ Streaming + chunk-based processing

→ No full memory load



Impact:

→ Supports GB-scale files



---



\### ADR-003 — UI Architecture



\* Pattern: MVVM strict

\* Docking system central



Decision:

→ All panels must be dockable + themed



---



\## 3. Performance Constraints



\* Must support very large files

\* UI must remain responsive

\* No blocking operations on UI thread



---



\## 4. Known Risks



\* Plugin memory leaks

\* UI freeze on large updates

\* Theme inconsistency



---



\## 5. Standards



\* SOLID mandatory

\* Composition > inheritance

\* 1 responsibility per file



---



\## 6. Evolution Notes



(append-only)



Format:



\* Date

\* Change

\* Impact

\* Risk



---



