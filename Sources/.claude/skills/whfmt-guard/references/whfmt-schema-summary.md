# whfmt schema quick reference

Authoritative: `Core/WpfHexEditor.Core.Definitions/whfmt.schema.json`.

## Required top-level fields (enforced by whfmt-schema-required)

| Field         | Type      | Notes                                      |
|---------------|-----------|--------------------------------------------|
| `formatName`  | string    | Human-readable name.                       |
| `formatId`    | string    | Unique across catalog (whfmt-id-uniqueness). |
| `extensions`  | string[]  | At least one entry, leading dot required.  |
| `category`    | string    | Folder under FormatDefinitions/ matches.   |
| `description` | string    | Free-form, may use {{variables}}.          |

## Optional but tracked

- `version` — semver-ish. Checked monotone vs git HEAD.
- `detection.signature` — hex bytes string.
- `detection.offset` — int.
- `detection.strength` — one of: None, Weak, Medium, Strong, VeryStrong.
- `detection.validation.note` — free-form; suppresses magic-collision WARN
  when present (acknowledges intentional overlap, e.g. ZIP-based containers).
- `variables{}` — placeholder bag referenced by `{{var}}` in descriptions.

## JSONC tolerance

`ImportFromJson` (runtime) and this skill both accept a leading
`/* ... */` block-comment header. Strip it then parse strict JSON.
Inline `//` comments are tolerated by the skill on individual lines.
