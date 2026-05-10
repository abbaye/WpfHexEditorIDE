---
name: solid-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after creating
  new C# files OR adding >50 lines to existing classes (especially under
  Services/, Managers/, *Manager.cs, *Service.cs). Heuristics that
  CORRELATE with SOLID violations: SRP (mixed UI+IO concerns, classes
  >300 lines + >15 public methods), OCP (massive switch statements >10
  cases), DIP (newing services instead of injecting, static singleton
  mutation), ISP (fat interfaces, NotImplementedException stubs). LSP is
  out of scope (impossible to detect mechanically). All findings are
  ADVISORY (warn-only). Skip on: Tests/, Samples/, *.Designer.cs, *.g.cs.
---

# solid-guard (internal)

**ADVISORY skill.** Mechanical heuristics that *correlate* with SOLID
violations — not formal proofs. False positives are expected; the value is
in surfacing patterns worth a human look, not in blocking edits.

## When I invoke

| Situation                                                         | Run? |
|-------------------------------------------------------------------|------|
| New `.cs` file created                                            | yes  |
| Edit adds >50 lines to a class                                    | yes  |
| Edit under `**/Services/`, `**/Managers/`, `*Manager.cs`, `*Service.cs` | yes |
| Edit in `Tests/`, `Samples/`, `*.Designer.cs`, `*.g.cs`           | no   |
| Comment-only or rename-only edit                                  | no   |

## Pipeline

1. Gather modified `.cs` files matching the trigger.
2. Run `scripts/solid-scan.ps1 -Files <paths>`.
3. Output: `SOLID: <summary>` or `OK` + per-issue lines.

## 6 heuristics (all warn-only)

| Rule                  | Detected via                                                | SOLID letter |
|-----------------------|-------------------------------------------------------------|--------------|
| `srp-mixed-concerns`  | class touches both `System.IO`/`File.*` AND `System.Windows.*` (UI + IO) | S |
| `srp-class-too-broad` | class >300 lines AND >15 public methods                     | S |
| `ocp-massive-switch`  | a method contains a `switch` with >10 cases on a type/enum  | O |
| `dip-newing-services` | class instantiates `new XService(` / `new XManager(` outside a Factory | D |
| `dip-static-deps`     | `<Type>.Instance.<Member> = ...` (mutable singleton write)  | D |
| `isp-fat-interface`   | `interface I { ... }` with >10 members OR implementations throwing `NotImplementedException` | I |

LSP (Liskov) is **omitted** — reliable detection requires Roslyn semantic
analysis, out of scope for a regex skill.

## Suppression annotations

- `// solid-ok: <reason>` on the line of the offending construct silences
  that rule for that occurrence.
- A class file containing `Factory` in its name is exempted from
  `dip-newing-services` (factories legitimately new services).
- `srp-mixed-concerns` exempts file watchers (`*FileWatcher*`,
  `*FileMonitor*`) — these legitimately bridge IO events to the UI
  Dispatcher.

## Output format

```
SOLID: 1 srp-mixed-concerns, 2 dip-newing-services, 1 isp-fat-interface
  FileMonitorService.cs:1   srp-mixed-concerns       uses File.* AND System.Windows.Threading
  CodeAnalysisRunner.cs:42  dip-newing-services      new RoslynDiagnosticsCollector() — inject?
  CodeAnalysisRunner.cs:88  dip-newing-services      new HalsteadMetricsCollector()  — inject?
  IDocumentService.cs:14    isp-fat-interface        14 members (consider splitting)
```

## What this skill does NOT do

- Does **not** prove SOLID compliance. Heuristics ≠ formal analysis.
- Does **not** rewrite code. Refactor decisions stay human.
- Does **not** detect LSP violations.
- Does **not** check inheritance hierarchies (depth, fan-out) — that is a
  metric domain (Halstead, NOC) covered by `WpfHexEditor.App/Analysis/`.

## When a rule fires legitimately

Some patterns flagged by the skill are **correct by design**:

- `srp-mixed-concerns` on a `*Adapter.cs` that bridges IO events to the
  UI dispatcher → expected. Add `// solid-ok: bridge adapter`.
- `dip-newing-services` inside a composition root (App.xaml.cs,
  IDEHostContext, *Module.cs registrar) → expected. Annotate or rename
  the class to `*Factory`.
- `ocp-massive-switch` in a parser / interpreter / serializer →
  often correct (closed dispatch on an enum). Annotate.

Annotations document the decision; future readers (and re-scans) will not
re-flag.

## Maintenance

- New mechanically-detectable heuristic → add a row to
  `references/solid-heuristics.md` AND extend the rules array in
  `solid-scan.ps1`.
- Refine false-positive whitelist by editing the path filters and
  exemption regexes in the script (kept short and explicit).
