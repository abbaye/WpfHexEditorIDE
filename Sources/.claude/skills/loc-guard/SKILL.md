---
name: loc-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  any *.resx (base or satellite) to verify the 28-language satellite parity:
  every key in *Resources.resx must exist in every *Resources.<lang>.resx,
  every satellite key must exist in the base (no orphans), placeholders
  ({0}/{1}) must match, and value-equals-base in non-en languages is flagged
  as "untranslated" (warn). Distinct from xaml-guard (which only checks
  Designer.cs parity for the base resx — loc-guard covers the satellite
  matrix that xaml-guard skips). Skip on: edits to non-resx files.
---

# loc-guard (internal)

The repo has 56 base `*Resources.resx` files. Each can have up to 28 satellite
files (`<base>.<lang>-<region>.resx`). `xaml-guard` already validates the
**base.resx ↔ Designer.cs** parity. `loc-guard` validates the **base.resx ↔
satellites** parity that no other skill covers.

## When I invoke

| Situation                                          | Run? |
|----------------------------------------------------|------|
| Edit a base `*Resources.resx`                      | yes (scan all its satellites) |
| Edit a single satellite `*.<lang>.resx`            | yes (scan against its base only) |
| Add a new key to base (Phase 6 loc workflow)       | yes  |
| Edit non-resx file                                 | no   |

## Pipeline

1. For each modified resx, locate its base + all satellites.
2. Run `scripts/loc-parity.ps1 -Files <paths>`.
3. Output: per-base summary `Loc <BaseName>: <N> satellites checked` then
   per-satellite stats.

## 5 rules

| Rule                  | Severity | Detected via                                    |
|-----------------------|----------|-------------------------------------------------|
| `satellite-missing-key` | error  | key in base but absent from satellite           |
| `satellite-orphan-key`  | error  | key in satellite but absent from base           |
| `placeholder-mismatch`  | error  | base has `{0} {1}`, satellite has `{0}` (or any other format-arg drift) |
| `untranslated`          | warn   | satellite value identical to base value (only flagged for non-`en-*` cultures) |
| `satellite-malformed`   | error  | XML parse fails or root element != `<root>` (extends xaml-guard's check across 28 langs) |

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

## Maintenance

- New language added (e.g. `nb-NO.resx`) → automatically picked up on next
  run.
- Renaming a base `*Resources.resx` → update all 28 satellites in the same
  commit (otherwise the old satellites become orphan files; the skill
  doesn't detect orphan files at the filesystem level — out of scope).
- For sub-agent delegation of large waves, see
  `references/agent-delegation.md`.
