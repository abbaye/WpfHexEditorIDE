# Leak rules

| Rule                          | Severity | What it catches                                              | Fix recipe |
|-------------------------------|----------|--------------------------------------------------------------|-----------|
| `event-no-unsubscribe`        | warn     | `target.Event += handler;` with no matching `-=` in the file | Add `target.Event -= handler;` in `Dispose` / `Unloaded` / `Closed` / a `Detach` method |
| `idisposable-no-dispose`      | error    | Class declares `IDisposable` but no `Dispose()` body         | Implement `void Dispose()` (or `IDisposable.Dispose()`) |
| `dispose-no-suppress-finalize`| warn     | Finalizer present but `Dispose()` lacks `GC.SuppressFinalize(this)` | Add `GC.SuppressFinalize(this);` at the end of `Dispose()` |
| `filestream-no-using`         | warn     | `new FileStream(` / `File.Open(` not in `using` and not stored as a disposable field | Wrap in `using` or store in a field disposed by the owning class |
| `timer-no-stop`               | warn     | `DispatcherTimer` / `Timer` field with no `.Stop()` or `.Dispose()` reference | Stop in `Unloaded` / `Dispose`; for `System.Threading.Timer`, dispose it |
| `process-no-dispose`          | warn     | `new Process(` / `Process.Start(` without `using`            | Wrap in `using` or assign to a field and dispose with the owner |
| `static-event-collection`     | error    | `public static event` OR static mutable collection mutated by instance code | Convert to instance, or use `WeakEventManager` / `ConditionalWeakTable` |
| `weak-event-candidate`        | warn     | `+=` on a long-lived host (`Application.Current`, `Dispatcher.*`, `*.Instance.*`) from a control | Use `WeakEventManager` or guarantee `-=` on `Unloaded` |
| `secret-in-source`            | error    | `(api[_-]?key\|password\|secret\|token\|bearer)="<16+ chars>"` | Move to user secrets / env / config; never commit |

## Long-lived host list (used by `weak-event-candidate`)

`Application.Current.*`, `Dispatcher.*`, `EventBus.*`, `IDEEventBus.*`, any
`*.Instance.*` accessor.

If the project introduces a new long-lived global (e.g. a new singleton
manager), add it to `$longLivedSources` in `leak-scan.ps1` AND list it here.

## Suppression annotations

- Same-line `// leak-ok: <reason>` silences any rule on that line.
- Files under `Tests/`, `Samples/`, `*.Designer.cs`, `*.g.cs` are skipped.
- `// fixture` comment on a secret line excludes from `secret-in-source`.

## Why no LSP semantic analysis

Roslyn analyzers exist for some of this (CA2213, CA1063). The skill is a
**fast pre-flight** at edit time, not a replacement for compile-time
analyzers. False negatives are expected on cross-file patterns.
