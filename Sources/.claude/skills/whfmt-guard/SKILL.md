---
name: whfmt-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  any *.whfmt under Core/WpfHexEditor.Core.Definitions/FormatDefinitions/ or
  the whfmt.schema.json. Domain checks NOT covered by build gate
  EmbeddedWhfmt_Tests (parse-only) or nuget-guard (public API): JSONC parse
  with /* */ header tolerated (per feedback_whfmt_guard), version monotone
  vs git HEAD (per feedback_whfmt_version), required schema fields
  (formatName, formatId, extensions, category, description), formatId
  uniqueness across catalog, magic-signature collisions, detection.strength
  enum (catches typos before SignatureStrength converter crash —
  bug_signaturestrength_converter), and placeholder/variables drift.
  Skip on: files outside FormatDefinitions/, Tests/, Samples/, doc-only or
  comment-only edits.
---

# whfmt-guard (internal)

Guards the **content** of .whfmt format-definition files. Complements:
- `nuget-guard` — protects the public C# API of whfmt.* packages.
- `EmbeddedWhfmt_Tests` build gate — parses all 427+ embedded formats.

This skill catches what neither does: per-file schema/version/signature
regressions before they reach CI.

## When I invoke

| Situation                                                              | Run? |
|------------------------------------------------------------------------|------|
| Edit `**/FormatDefinitions/**/*.whfmt`                                 | yes  |
| Edit `**/whfmt.schema.json`                                            | yes  |
| Edit a `.whfmt` under `Tests/` or `Samples/`                           | no   |
| Doc-only / comment-only edit                                           | no   |
| Edit `.cs` (covered by nuget-guard)                                    | no   |

## Pipeline

1. Filter changed files to `.whfmt` under `Core/WpfHexEditor.Core.Definitions/FormatDefinitions/`. Empty → exit 0.
2. Load catalog index once (all `.whfmt` paths) for cross-file checks (R4, R5, R6).
3. Run R1→R7 per changed file.
4. Emit one `ERR|WARN <rule> <file> <detail>` per finding.
5. Exit code = number of ERR findings. WARN-only ⇒ exit 0.

## Rules

| Rule                       | Severity | Detects                                                                                                  |
|----------------------------|----------|----------------------------------------------------------------------------------------------------------|
| `whfmt-jsonc-parse`        | error    | File fails JSONC parse. Leading `/* ... */` header is tolerated then content must be strict JSON.        |
| `whfmt-version-monotone`   | error    | `version` field semver < version in `git show HEAD:<path>`. New files exempt. Bumps always allowed.      |
| `whfmt-schema-required`    | error    | Missing one of: `formatName`, `formatId`, `extensions` (non-empty array), `category`, `description`.     |
| `whfmt-id-uniqueness`      | error    | `formatId` collides with another `.whfmt` in catalog (case-insensitive).                                 |
| `whfmt-magic-collision`    | warn     | Same `detection.signature` + `detection.offset` + overlapping `extensions[]` as another file. Add a `detection.validation.note` to silence. |
| `whfmt-strength-enum`      | warn     | `detection.strength` not in {None, Weak, Medium, Strong, VeryStrong}. Prevents `SignatureStrength` converter surprises. |
| `whfmt-placeholder-drift`  | warn     | `{{var}}` token used in `description` or `blocks[].description` but `var` not declared in `variables{}`. |

## What this skill does NOT do

- Does not validate `blocks[]` field offsets (that's `whfmt.Validate`).
- Does not run signature accuracy fuzzing (that's `whfmt.Fuzz`).
- Does not check the schema JSON itself for well-formedness — assumes
  `whfmt.schema.json` is authoritative.

## Suppression

Append `// whfmt-ignore: <reason>` as a JSONC line comment above the
offending field. R1 still parses (comment stripped before strict JSON).

## Maintenance

- New required field in schema v3 → add to `whfmt-schema-required` list.
- New `SignatureStrength` enum value → update `whfmt-strength-enum` allow-list
  and the `SignatureStrength` C# enum in lockstep.
