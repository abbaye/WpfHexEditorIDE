# WpfHexEditor Prompt OS

Infer flags implicitly. Keep analysis lightweight unless LEVEL 3/4.

## Boot
Load: global rules → project CLAUDE.md → memory. ❌ No execution before full context load.

## Execution
Default: READ-ONLY. PLAN mandatory → wait for GO. No out-of-plan changes. No architecture drift.

## Workflow
1. Analyze → 2. Detect context → 3. Load modules → 4. Build PLAN → 5. WAIT GO → 6. Execute → 7. Validate → 8. Update memory. ❌ Cannot skip steps.

## Context Detection (auto)
- `.xaml`, UserControl, Window → ENABLE_WPF
- Editor/Tool/Plugin/Module keywords → ENABLE_PLUGINS
- HexEditor/buffer/stream/binary → ENABLE_PERF
- DOC/README/.md → ENABLE_DOC
- Structural change → ENABLE_GUARDIAN

Only active flags influence decisions.

## Engineering
- 1 file = 1 responsibility. UI(WPF) / App(VM+Services) / Domain / Infra separated. SOLID. Composition > inheritance.
- Small, explicit, readable. No hidden side-effects.
- Root cause always. Temp fix → resolution plan required.

## Architecture
- WpfHexEditor.App → UI shell
- Editors → independent modules
- Services → shared logic
- Plugins → isolated + reloadable
- Docking → central UI layout

❌ Never break modularity.

## Triggers
- `PLAN` → full plan, blocks until GO
- `BUG` → root cause mandatory
- `COR` → quick fix
- `DOC` → full doc scan
- `DOCR` → recent docs only

## Quality
Production-grade only. Question: "Would this fit inside Visual Studio-level IDE?"

## Analysis (complex tasks)
Before execution: Architecture/Integration/Performance scores. Detect: breaking changes, plugin incompatibility, perf regression, UI inconsistency. Output: Risk (LOW/MED/HIGH), Scope (SMALL/MED/LARGE), Recommendation (PROCEED/ADJUST/STOP). Score <90% → propose refactor PLAN.

## ADR
For each significant change generate: Decision ID (ADR-XXX), Context, Decision, Alternatives, Impact. Append to memory. Auto-trigger: new module, architecture change, performance decision.

## Refactor Engine
Triggers: duplication, classes >300 lines, tight coupling, perf bottlenecks. Strategy: minimal impact, step-by-step PLAN, backward compat. Must not break plugins/UI. Priority: Performance > Cleanliness, Stability > Refactor.

## Agents (activate as needed)
- Architecture change → ARCHITECT
- Large data/hex → PERFORMANCE
- Code quality → REVIEWER
- Plugin/module → PLUGIN GUARDIAN

## Autonomous Improvement
May propose refactor/perf/arch improvements when inefficiency detected. Must NOT modify without PLAN + GO.
