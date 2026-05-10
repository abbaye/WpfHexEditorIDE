# Typography

The repo uses a discrete FontSize ladder. The system is **descriptive** —
sizes in use across MainWindow, dialogs, panels were measured and turned
into the canonical set below.

## Canonical FontSize values (used by `ui-check.ps1`)

```
9  10  11  12  13  14  16  18  20  22  24  28  32  36  40  48
```

## Role assignment (observed)

| Size  | Role                                                        |
|-------|-------------------------------------------------------------|
| `9`   | Tiny badge text (notification count, tag pill counter)      |
| `10`  | Status-bar metadata                                         |
| `11`  | Caption / secondary label                                   |
| `12`  | Body small (tree items, list rows, panel content)           |
| `13`  | Body / icon font default (Segoe MDL2 Assets in toolbars)    |
| `14`  | Body large / Welcome panel paragraphs                       |
| `16`  | Section title in dialogs                                    |
| `18`  | Welcome panel sub-heading                                   |
| `20+` | Splash, hero text, ASCII-art emphasis                       |

## Font families

- Default UI: inherited from theme — typically Segoe UI (Light/Dark themes)
  or theme-specific (Cascadia Code, Fira Code) for code surfaces.
- Icon font: `Segoe MDL2 Assets` (NOT `Segoe Fluent Icons`). The repo
  standardized on MDL2 because it ships with Win10+ without extra deps.
- Code editor / Hex editor: `Cascadia Code`, `Consolas`, `Courier New`
  (settable via theme).

## Don't

- Don't introduce `FontSize="15"` or other off-ladder values without
  documenting why. The skill flags these as warnings; ignore one if
  intentional, but don't accumulate them.
- Don't mix `Segoe Fluent Icons` with `Segoe MDL2 Assets` — codepoints
  partially overlap but are not identical. Stick with MDL2.

## Adding a new size

If a new design genuinely needs a size outside the ladder (e.g. a custom
splash screen), add it to `$canonicalFontSizes` in `ui-check.ps1` AND
document the role here. Ladders that grow without rationale defeat the
purpose.
