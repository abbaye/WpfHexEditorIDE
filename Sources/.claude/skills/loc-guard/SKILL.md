---
name: loc-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes on two scopes:
  (1) PARITY — after editing any *.resx (base or satellite), verifies the
  28-language satellite parity (missing/orphan keys, placeholder drift,
  untranslated). (2) SOURCE — after editing *.xaml or *.cs under App/,
  Editors/, Plugins/, Core/, Controls/, advisory checks for: DynamicResource
  on loc keys (must be StaticResource), hardcoded user-visible string
  literals, IdeMessageBox.Show literals, legacy MessageBox.Show usage
  (ADR-009), and missing LocalizedResourceDictionary wiring (ADR-005).
  All SOURCE findings are warn-only. Distinct from xaml-guard (which only
  checks Designer.cs parity for the base resx). Skip on: Themes/, ColorPicker,
  Designer.cs/g.cs, Tests/, Samples/, pure structural XAML edits.
---

# loc-guard (internal)

The repo has 56 base `*Resources.resx` files. Each can have up to 28 satellite
files (`<base>.<lang>-<region>.resx`). `xaml-guard` already validates the
**base.resx ↔ Designer.cs** parity. `loc-guard` validates the **base.resx ↔
satellites** parity that no other skill covers.

## When I invoke

### Scope 1 — PARITY (resx only)

| Situation                                          | Run? |
|----------------------------------------------------|------|
| Edit a base `*Resources.resx`                      | yes (scan all its satellites) |
| Edit a single satellite `*.<lang>.resx`            | yes (scan against its base only) |
| Add a new key to base (Phase 6 loc workflow)       | yes  |

### Scope 2 — SOURCE (xaml + cs, advisory)

| Situation                                                                    | Run? |
|------------------------------------------------------------------------------|------|
| Edit `*.xaml` under `App/`, `Editors/`, `Plugins/`, `Core/`, `Controls/`     | yes  |
| Edit `*.cs` (UI / view-model / service) outside excluded paths               | yes  |
| Edit base `*Resources.resx` (also triggers R4 wiring check, once/assembly)   | yes  |
| Edit `Themes/*.xaml`, `ColorPicker/*`, `*.Designer.cs`, `*.g.cs`             | no   |
| Edit `Tests/`, `Samples/`, `obj/`, `bin/`                                    | no   |
| Pure structural edits (Grid.Row, Margin, Width on existing elements)         | no   |
| Single-line comment / whitespace                                             | no   |

## Pipeline

### Scope 1 — parity
1. For each modified resx, locate its base + all satellites.
2. Run `scripts/loc-parity.ps1 -Files <paths>`.
3. Output: per-base summary `Loc <BaseName>: <N> satellites checked` then
   per-satellite stats.

### Scope 2 — source
1. Collect modified `*.xaml` / `*.cs` / base `*.resx` paths.
2. Run `scripts/loc-source-guard.ps1 -Files <paths>`.
3. Output: one `WARN <rule> <file>:<line> <detail>` per finding. Always
   exits 0 (advisory). To silence a specific line, append the marker
   `// loc-ignore: <reason>` on that line (CS) or as a same-line XML
   comment (XAML).

## Rules

### Scope 1 — parity (5 rules, errors are blocking)

| Rule                  | Severity | Detected via                                    |
|-----------------------|----------|-------------------------------------------------|
| `satellite-missing-key` | error  | key in base but absent from satellite           |
| `satellite-orphan-key`  | error  | key in satellite but absent from base           |
| `placeholder-mismatch`  | error  | base has `{0} {1}`, satellite has `{0}` (or any other format-arg drift) |
| `untranslated`          | warn   | satellite value identical to base value (only flagged for non-`en-*` cultures) |
| `satellite-malformed`   | error  | XML parse fails or root element != `<root>` (extends xaml-guard's check across 28 langs) |

### Scope 2 — source (5 rules, all advisory / warn-only)

| Rule                          | Detected via                                                                                                          |
|-------------------------------|------------------------------------------------------------------------------------------------------------------------|
| `loc-static-required`         | `{DynamicResource <Key>}` where `<Key>` is a loc key (known assembly prefix or `_Title`/`_Label`/`_ToolTip`/… suffix). New loc strings MUST use `StaticResource` (or `{x:Static l10n:Resources.X}`). DynamicResource on loc keys is a regression — was historically allowed; current rule is StaticResource. |
| `loc-hardcoded-string`        | XAML `Text=`/`ToolTip=`/`Header=`/`Content=`/`Title=` with a literal string value (not a binding, not a resource ref, not numeric/whitespace/punctuation-only). Same check for CS UI property assignments (`.Text = "…"`, `.Title = "…"`, …). |
| `loc-idemessagebox-literal`   | `IdeMessageBox.Show("literal", …)` or `IDialogService.Show("literal", …)`. First positional argument must be a resource key.                                                                       |
| `loc-messagebox-legacy`       | `MessageBox.Show(…)` (standard WPF). Should use `IdeMessageBox` via `IDEHostContext.Dialogs` — ADR-009.                                                                                            |
| `loc-locdict-missing`         | Assembly contains `*Resources.resx` but no `App.xaml` / `Module.xaml` in the assembly merges a `LocalizedResourceDictionary` — ADR-005. Reported once per assembly.                                |

LSP-style false positives (technical strings, format placeholders) are
suppressed by `data/allowlist.json` and the per-line `// loc-ignore: <reason>`
marker.

## Output format

```
Loc AppResources (28 satellites):
  fr-FR  OK     keys=680
  fr-CA  OK     keys=680
  ja-JP  3 missing-key   APP_NewFile_Title, APP_Recent_Header, PA_FilterTitle
  ar-SA  1 placeholder-mismatch  APP_Status_Loading base="{0}/{1}" sat="{0}"
  ru-RU  124 untranslated  (warn — same value as base)
  el-GR  malformed  XML parse error: ...
```

## Auto-detection of language matrix

Each assembly has its own language coverage. AppResources has 28 satellites,
DocumentEditorResources has 18. The script detects the matrix dynamically by
scanning sibling files matching `<base>.<lang>(-<region>)?.resx` — no
hard-coded list.

## Whitelist

- A satellite value of `""` (empty string) is treated as "delete this key in
  this language" — not an error, but counted in coverage.
- A satellite that doesn't exist at all is treated as "language not yet
  added" — silent (use `theme-parity`-style explicit gap report if needed).
- The `untranslated` rule never fires for `en-*` cultures (English is often
  the source language; identical values are expected).

## Output catalog (data/satellites-snapshot.tsv)

After every successful run, the script can refresh
`data/satellites-snapshot.tsv` with one row per (base, language, key-count,
missing-count, placeholder-mismatch-count). Useful as a periodic snapshot of
loc completion across the 56 assemblies.

## What this skill does NOT do

- Does **not** translate strings.
- Does **not** generate satellite files (xaml-guard / Phase 6 infra).
- Does **not** validate `.Designer.cs` parity (xaml-guard's job).
- Does **not** coordinate sub-agent satellite creation (that pattern is in
  memory `feedback_localization_agent_strategy`).
- Does **not** block on Scope 2 findings (advisory only). Use in combination
  with `code-analysis` for hard quality gates.
- Does **not** detect LSP-aware semantic info (e.g. whether a `.Text` setter
  binds to a `TextBlock` or a non-UI POCO). Heuristic-only; tune via
  `data/allowlist.json` if noise rises.

## Maintenance

- New language added (e.g. `nb-NO.resx`) → automatically picked up on next
  run.
- Renaming a base `*Resources.resx` → update all 28 satellites in the same
  commit (otherwise the old satellites become orphan files; the skill
  doesn't detect orphan files at the filesystem level — out of scope).
- For sub-agent delegation of large waves, see
  `references/agent-delegation.md`.
