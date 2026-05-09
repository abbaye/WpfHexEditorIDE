---
name: leak-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  C# files that touch IDisposable, events, FileStream, Process, Timer, or
  files under Services/, *Manager.cs, *Service.cs, *Watcher*, *Adapter.cs.
  Detects: event handlers added without unsubscribe, IDisposable without
  Dispose, missing GC.SuppressFinalize, FileStream without using, Timer not
  stopped, Process not disposed, static event collections, weak-event
  candidates, hardcoded secrets. Skip on: Tests/, Samples/, *.Designer.cs,
  *.g.cs, generated code.
---

# leak-guard (internal)

Static guard for memory / handle / event leaks and basic secret hygiene.
Targets the patterns the WpfHexEditor codebase actually uses (75+ IDisposable
implementations, lots of file watching, LSP integration with long-lived
handlers).

## When I invoke

| Situation                                                                 | Run? |
|---------------------------------------------------------------------------|------|
| Edit `.cs` containing `IDisposable`, `event`, `+=` on event               | yes  |
| Edit `.cs` using `FileStream`, `File.Open`, `Process`, `Timer`            | yes  |
| Edit under `Services/`, `*Manager.cs`, `*Service.cs`, `*Watcher*`, `*Adapter.cs` | yes |
| Edit in `Tests/`, `Samples/`, `*.Designer.cs`, `*.g.cs`                   | no   |
| Pure renaming, comment-only changes                                       | no   |

## Pipeline

1. Gather modified `.cs` files matching the trigger.
2. Run `scripts/leak-scan.ps1 -Files <paths>`.
3. Output: `Leaks: <summary>` or `OK` + per-issue lines with file:line.

## 9 rules

| Rule                          | Severity | Detected via                              |
|-------------------------------|----------|-------------------------------------------|
| `event-no-unsubscribe`        | warn     | `+=` on event without matching `-=` in `Dispose`/`Unloaded` of same class |
| `idisposable-no-dispose`      | error    | `: IDisposable` without `Dispose()` method body |
| `dispose-no-suppress-finalize`| warn     | `~Class()` finalizer present, but `Dispose()` lacks `GC.SuppressFinalize(this)` |
| `filestream-no-using`         | warn     | `new FileStream(` / `File.Open(` not preceded by `using` and not on a field |
| `timer-no-stop`               | warn     | `new (DispatcherTimer\|Timer\|System.Timers.Timer)(` field without `Stop()`/`Dispose()` reference |
| `process-no-dispose`          | warn     | `Process.Start` / `new Process(` without `using` and result assigned to local |
| `static-event-collection`     | error    | `public static event` OR `static (List\|Dictionary\|HashSet)<...>` mutated by instance methods |
| `weak-event-candidate`        | warn     | `+=` on `Application.Current.*`, `Dispatcher.*`, or any `*.Instance.*` event from a `UserControl`/`Window` |
| `secret-in-source`            | error    | regex on `(api[_-]?key\|password\|secret\|token)\s*=\s*"[A-Za-z0-9+/=]{16,}"` |

## Whitelist / suppressions

- Skip path patterns: `\\Tests\\`, `\\Samples\\`, `\.Designer\.cs$`,
  `\.g\.cs$`, `\.g\.i\.cs$`.
- Inline `// leak-ok: <reason>` on the same line silences any rule on that
  line.
- `secret-in-source` skips lines matching `// fixture` or files under
  `Tests/Fixtures/`.

## Output format

```
Leaks: 2 event-no-unsubscribe, 1 timer-no-stop | 0 secrets
  FileMonitorService.cs:42       event-no-unsubscribe   _watcher.Changed += OnFileChanged (no -= in Dispose)
  HighlightPipelineService.cs:128 timer-no-stop          DispatcherTimer field never .Stop()
```

## What this skill does NOT do

- Does **not** run static analyzers (Roslyn analyzers exist for some of this).
- Does **not** instrument runtime (no GC.Collect, no allocations profiling).
- Does **not** rewrite code to fix.
- Does **not** detect cross-file leaks (handler subscribed in class A, never
  removed in class B). It is single-file static analysis.

## Maintenance

- New leak pattern discovered → add a row to `references/leak-rules.md` AND
  extend `$rules` in `leak-scan.ps1`.
- When the project introduces a new long-lived host object (e.g. a singleton
  manager) → add it to the `weak-event-candidate` long-lived-source list in
  the script.
