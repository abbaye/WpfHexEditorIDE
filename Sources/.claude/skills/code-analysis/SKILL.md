---
name: code-analysis
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes this skill BEFORE
  any non-trivial edit AND AFTER multi-file changes, to enforce CLAUDE.md rules
  (functions <=25 lines, classes <=300, 1-file-1-responsibility, module
  boundaries, forbidden patterns). Auto-trigger on: edits >50 lines, new files,
  refactors, module-crossing changes, plugin/SDK touches, performance-sensitive
  paths (HexEditor buffer, CodeEditor render, viewport, drag/scroll), Roslyn
  diagnostics work. Skip on: doc-only, resx-only, comment fixes, single-line bug
  fixes, single-file <20-line edits.
---

# code-analysis (internal)

This is **my** dev assistant skill, not a user-facing command. I invoke it on
myself when working on WpfHexEditor to keep changes aligned with `CLAUDE.md`,
project ADRs, and the existing `WpfHexEditor.App/Analysis` engine.

## When I invoke

| Situation                                       | Phases   |
|-------------------------------------------------|----------|
| User asks "ajoute / refactor / corrige X"       | P1, P3   |
| Edit traverses 2+ modules                       | P1, P3   |
| New file created                                | P1, P3   |
| Touch HexEditor/ or CodeEditor/ render/buffer   | P1, P3 (mandatory) |
| Plugin or SDK surface change                    | P1, P3 (mandatory) |
| Multi-file completion                           | P3       |
| Single-file edit < 20 lines, no new symbol      | skip     |
| .resx / .md / comments only                     | skip     |

## Phases

### P1 — pre-edit-check (before first Edit/Write)

Run `scripts/pre-edit-check.ps1 -File <path>`.
Output: one line per file. `LOC=X | Funcs>25=N | Class>300=M | Callers=K | Module=<id> | Risk=LOW|MED|HIGH`.

Use to:
- decide if a refactor PLAN is needed (HIGH risk → propose PLAN, do not edit).
- size the change vs CLAUDE.md thresholds.
- spot module-boundary crossing before it happens.

### P2 — forbidden-pattern guard (during each Edit)

Mental checklist sourced from `references/forbidden-patterns.md`. Refuse to
write any of:
- `using ICSharpCode` / AvalonEdit references.
- `MessageBox.Show(` outside `IdeMessageBox` (use `IDialogService`).
- `.md` files inside `Sources/` tree.
- Hardcoded user-visible strings in XAML / VMs (use `DynamicResource`).
- `Resources.X` inside HexEditor partials (alias as `L10n`).
- Background "fix" without root cause (`BUG` trigger required).
- New mutable static state on hot paths (HexEditor, CodeEditor render).

If a pattern is unavoidable, surface it explicitly and ask before proceeding.

### P3 — post-edit-audit (after the edit batch, before reporting done)

Run `scripts/post-edit-audit.ps1 -Files <paths> -Baseline HEAD`.
Output: `OK` or `VIOLATIONS: <short list>`.

Then:
- `OK` → continue / report done.
- `VIOLATIONS` small (≤2, mechanical) → fix immediately in same turn.
- `VIOLATIONS` large or architectural → stop, propose follow-up `PLAN`, do not
  pretend the task is done.

## Scope-impact (on demand, not automatic)

When refactoring a public symbol or moving a file across modules:
`scripts/scope-impact.ps1 -Symbol <name>` to enumerate referencing files (cap
50, depth 2). Used to size blast radius before committing to the change.

## Performance budget (self-imposed)

- P1 output: <= 5 lines per file, <=150 tokens total.
- P3 output: <= 10 lines, <=200 tokens.
- Never read full JSON reports — scripts aggregate.
- Never scan full solution by default — scope is the changed files only.
- Cache: skip P1 re-run on a file if `.skill-cache/<hash>` is < 5 min old AND
  the file mtime is unchanged.

## What this skill does NOT do

- Does **not** replace `CodeAnalysisRunner`. For deep semantic analysis
  (Halstead, LCOM, Roslyn diagnostics) the user runs the in-app Code Analysis
  pane. This skill is the lightweight pre/post guard.
- Does **not** modify source code automatically.
- Does **not** run `dotnet build`. Build verification is the user's call.
- Does **not** invoke external services or upload code anywhere.

## References

- `references/project-thresholds.md` — numeric limits aligned with CLAUDE.md.
- `references/module-boundaries.md` — who may reference whom.
- `references/forbidden-patterns.md` — regex-detectable anti-patterns sourced
  from project memory (ADR-009, feedback_no_avalonedit,
  feedback_localization_new_strings, feedback_resources_alias_hexeditor, etc.).

## Failure modes I must avoid

- **Sur-déclenchement**: invoking on a 3-line typo fix wastes turns. Honor the
  skip rules above.
- **Cache staleness**: if I just edited a file, P3 must NOT use a P1 cache for
  that file.
- **PowerShell edition mismatch**: scripts assume PowerShell 7+. If
  `$PSVersionTable.PSEdition -ne 'Core'` they exit with a clear message; I
  fall back to manual `Grep`/`Read` checks.
