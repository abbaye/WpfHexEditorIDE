---
name: theme-parity
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  Themes/<Theme>/Colors.xaml or *Theme.xaml to verify the 18 themes share the
  same key surface. Detects: theme-key-missing (key in Dark but not in the
  edited theme), theme-key-orphan (key only in this theme), theme-color-no-brush
  (Color without paired SolidColorBrush), theme-merge-order (wrapper merges
  Colors.xaml after Menu.xaml/TabControl.xaml). Skip on: edits outside the
  Themes/ tree.
---

# theme-parity (internal)

Static guard for the 18-theme palette surface. The repo's design contract is
that **theme switching is a pure ResourceDictionary swap** — every theme
exposes the same key set, only the values change. A missing key in one theme
creates a runtime gap (control falls back to a hardcoded default or fails to
render).

`DarkTheme` is the **canonical reference**: it has the most complete key
set, and other themes are validated against it.

## When I invoke

| Situation                                                              | Run? |
|------------------------------------------------------------------------|------|
| Edit `*.xaml` under `Sources/Shell/WpfHexEditor.Shell/Themes/<X>/Colors.xaml` | yes  |
| Edit `*.xaml` under `Sources/Docking/WpfHexEditor.Docking.Wpf/Themes/<X>/Colors.xaml` | yes |
| Edit a `*Theme.xaml` wrapper (merge order)                             | yes  |
| Add a new theme folder                                                 | yes (regen reference) |
| Edit any non-theme XAML                                                | no (covered by `xaml-guard` / `ui-design`) |

## Pipeline

1. If `data/reference-keys.txt` is missing, run `scripts/theme-parity.ps1
   -RefreshReference` to extract the key surface from the reference theme
   (Dark).
2. Run `scripts/theme-parity.ps1 -Files <paths>`.
3. Output: per-theme summary `Theme: <name>  <missing>/<orphan>/<malformed>`
   then per-issue lines.

## 4 rules

| Rule                  | Severity | Detected via                                 |
|-----------------------|----------|----------------------------------------------|
| `theme-key-missing`   | error    | key in reference (Dark) but absent from edited theme |
| `theme-key-orphan`    | warn     | key present only in edited theme, no other theme defines it |
| `theme-color-no-brush`| warn     | `<Color x:Key="XColor">` without paired `<SolidColorBrush x:Key="XBrush">` (or vice versa) |
| `theme-merge-order`   | error    | `*Theme.xaml` wrapper merges `Colors.xaml` AFTER `Menu.xaml` / `TabControl.xaml` |

## Output format

```
Theme: Cyberpunk
  Missing: 4   DockMenuHighlightColor, DockMenuHighlightBrush, DockSplitterColor, DockSplitterBrush
  Orphan:  1   CyberNeonAccentColor (referenced by no other theme)
  Color/brush mismatch: 0
  Merge order: OK
```

## What this skill does NOT do

- Does **not** validate the *value* of a color (palette aesthetic stays a
  human decision).
- Does **not** rewrite the missing keys (only suggests via `--suggest-patch`).
- Does **not** check usage in non-theme XAML (that is `ui-design`'s
  `unknown-token` rule).

## Reference theme management

`data/reference-keys.txt` is the union of `<Color>` and `<SolidColorBrush>`
x:Key values from the reference theme (default: Dark). It is the contract
every other theme must satisfy.

To regenerate after intentionally adding new keys to Dark:

```pwsh
pwsh -File scripts/theme-parity.ps1 -RefreshReference
```

After regeneration, the missing-key check fires on every other theme that
hasn't caught up yet — that is the intended workflow to roll a new token to
all 18 themes.

## Suggesting patches

```pwsh
pwsh -File scripts/theme-parity.ps1 -Files <theme>/Colors.xaml -SuggestPatch
```

Emits a copy-from-reference snippet for each missing key, with the value
copied from Dark. The skill does NOT apply the patch — pasting and
adjusting the color literal is the human's call.

## Maintenance

- Adding a new theme folder under `Themes/` → run with `-RefreshReference`
  if needed, then `theme-parity.ps1 -Files <new theme>/Colors.xaml` to see
  the gap vs Dark.
- Promoting a token from one theme to all → add to Dark first, regen the
  reference, then propagate.
