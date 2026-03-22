\# 📘 CLAUDE.md — WpfHexEditor Prompt OS (v5)



---



Claude must infer flags implicitly without explicit declaration.

Claude must keep analysis lightweight unless task is LEVEL 3 or 4.



---



\## 1. Context Boot



Load:



\* Global rules

\* Project CLAUDE.md

\* Project memory



❌ No execution before full context load



---



\## 2. Execution Model



\* Default: READ-ONLY

\* `PLAN` mandatory before execution

\* Wait for explicit `GO`



Rules:



\* No out-of-plan changes

\* No architecture drift



---



\## 3. Workflow Engine



1\. Analyze request

2\. Detect context (see section 4)

3\. Load required modules

4\. Build PLAN

5\. WAIT for GO

6\. Execute

7\. Validate

8\. Update memory



❌ Cannot skip steps



---



\## 4. Smart Context Detection (IDE-AWARE)



Claude must auto-detect:



\### UI Layer



\* `.xaml`, `UserControl`, `Window`

&nbsp; → Load `claude.wpf.md`



\### Editor / Plugin System



\* keywords: Editor, Tool, Plugin, Module

&nbsp; → Load `claude.plugins.md`



\### Core Engine / Binary / Hex



\* HexEditor, buffer, stream, memory, binary parsing

&nbsp; → Load `claude.perf.md`



\### Documentation request



\* `DOC`, `README`, `.md`

&nbsp; → Load `claude.doc.md`



---



\## 5. Core Engineering Rules



\* 1 file = 1 responsibility



\* Strict separation:



&nbsp; \* UI (WPF)

&nbsp; \* Application (ViewModels / Services)

&nbsp; \* Domain (logic)

&nbsp; \* Infrastructure



\* SOLID mandatory



\* Composition > inheritance



Code:



\* Small, explicit, readable

\* No hidden side-effects



---



\## 6. Root Cause Rule



Always fix root cause

Temp fix → must include resolution plan



---



\## 7. Project Architecture Awareness (CRITICAL)



Claude must respect IDE structure:



\* WpfHexEditor.App → UI shell

\* Editors → independent modules

\* Services → shared logic

\* Plugins → isolated + reloadable

\* Docking system → central UI layout



❌ Never break modularity



---



\## 8. Memory \& Documentation



After execution:



\* Update project memory

\* Document:



&nbsp; \* decisions

&nbsp; \* architecture changes

&nbsp; \* performance impacts



---



\## 9. Triggers



\* `PLAN` → full plan required

\* `BUG` → root cause mandatory

\* `COR` → quick fix

\* `DOC` → full documentation scan

\* `DOCR` → recent docs only



---



\## 10. Quality Standard



Production-grade only



Question:

👉 Would this fit inside Visual Studio-level IDE?



---



\## 🧠 11. INTELLIGENT ANALYSIS ENGINE



Claude must evaluate and simulate before execution.



---



\### 1. Scoring



\* Architecture Score

\* Integration Score

\* Performance Score



---



\### 2. Risk Detection



\* Breaking changes

\* Plugin incompatibility

\* Performance regression

\* UI inconsistency



---



\### 3. Impact Simulation



Before execution:



\* Affected modules

\* Plugin impact

\* Memory/performance impact



---



\### 4. Output (for complex tasks)



\* Risk Level: LOW / MEDIUM / HIGH

\* Impact Scope: SMALL / MEDIUM / LARGE

\* Recommendation: PROCEED / ADJUST / STOP



---



\### 5. Guardrails



Claude must prevent:



\* Architecture drift

\* Full memory load

\* UI / logic mixing



---



\### 6. Refactor Trigger



If score < 90%:

→ MUST propose refactor PLAN



---



\## 12. Architecture Decision Tracking (ADR)



For each significant change:



Claude must generate:



\* Decision ID (ADR-XXX)

\* Context

\* Decision

\* Alternatives considered

\* Impact



---



\## Storage



Append to:

→ `CLAUDE.memory.md`



---



\## Trigger



Auto when:



\* New module

\* Architecture change

\* Performance decision



---



\## Goal



\* Full traceability

\* Long-term consistency

\* Knowledge persistence



---



\## ⚙️ 13. FEATURE FLAGS SYSTEM



Claude must dynamically enable features:



---



\### Flags



ENABLE\_WPF

ENABLE\_PLUGINS

ENABLE\_PERF

ENABLE\_DOC

ENABLE\_GUARDIAN



---



\### Auto-Activation Rules



\* `.xaml` detected → ENABLE\_WPF

\* Plugin/module keywords → ENABLE\_PLUGINS

\* Large data / hex → ENABLE\_PERF

\* DOC/README → ENABLE\_DOC

\* Structural change → ENABLE\_GUARDIAN



---



\### Behavior



\* Only active flags influence decisions

\* Disabled features MUST NOT add constraints



---



\### Goal



Minimize token usage + maximize relevance



---



\## 🧠 14. REFACTOR AI ENGINE



Claude must proactively improve the system.



---



\### 1. Refactor Triggers



\* Code duplication

\* Large classes (>300 lines)

\* Tight coupling

\* Performance bottlenecks



---



\### 2. Refactor Strategy



Claude must propose:



\* Minimal impact refactor

\* Step-by-step PLAN

\* Backward compatibility



---



\### 3. Safe Refactor Mode



Refactors must:



\* Not break plugins

\* Not break UI

\* Preserve behavior



---



\### 4. Refactor Types



\* Extract service

\* Split module

\* Introduce interface

\* Optimize data flow



---



\### 5. Priority



\* Performance > Cleanliness

\* Stability > Refactor



---



\## 🧠 15. MULTI-AGENT SYSTEM (MODULAR)



Claude must load agents dynamically.



---



\### Agents (external files)



\* ARCHITECT → claude.agent.architect.md

\* PERFORMANCE → claude.agent.performance.md

\* REVIEWER → claude.agent.reviewer.md

\* PLUGIN GUARDIAN → claude.agent.plugin.md



---



\### Activation Rules



\* Architecture change → ARCHITECT

\* Large data / hex → PERFORMANCE

\* Code quality → REVIEWER

\* Plugin/module → PLUGIN GUARDIAN



---



\### Workflow



1\. Detect required agents

2\. Load only needed agents

3\. Aggregate analysis

4\. Produce unified PLAN



---



\### Priority



1\. Stability

2\. Performance

3\. Architecture



---



\## 🧠 16. AUTONOMOUS IMPROVEMENT MODE



Claude may propose improvements even if not requested:



\* Refactor suggestions

\* Performance optimizations

\* Architecture improvements



---



Trigger:



\* Detected inefficiency

\* Repeated pattern issues

\* Growing complexity



---



Rule:



Must NOT modify without PLAN + GO

