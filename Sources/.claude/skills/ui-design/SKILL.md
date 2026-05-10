---
name: ui-design
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  *.xaml under WpfHexEditor.App/, Editors/, Controls/, Plugins/, Core/ to
  enforce the design-system: no hardcoded colors (use {DynamicResource Dock*
  / HexEditor_* / Panel_*}), no unknown tokens, canonical spacing scale,
  canonical FontSize ladder, glyph-only interactive elements need ToolTip,
  no template reinvention. Skip on: Themes/*.xaml (themes ARE the palette),
  ColorPicker (legitimate color literals), generated *.g.cs.
---

# ui-design (internal)

Static guard for the WpfHexEditor design-system. The repo has 18 themes
sharing the same `Dock*` / `HexEditor_*` / `Panel_*` token surface — every UI
edit should respect those tokens and the implicit conventions documented in
`PanelCommon.xaml`, `DialogStyles.xaml`, and the per-theme `Colors.xaml`.

## When I invoke

| Situation                                                       | Run? |
|-----------------------------------------------------------------|------|
| Edit `*.xaml` under `WpfHexEditor.App/`                         | yes  |
| Edit `*.xaml` under `Editors/*/`, `Controls/*/`, `Plugins/*/`   | yes  |
| Edit `*.xaml` in `Themes/` (any folder)                         | no   |
| Edit `*.xaml` in `*ColorPicker*`                                | no   |
| Edit `*.cs.xaml` / `*.g.cs` / generated                         | no   |
| New theme file under `Themes/`                                  | re-run `token-catalog.ps1` instead |

## Pipeline

1. After the edit batch, gather modified `*.xaml` files matching the trigger.
2. If `data/known-tokens.json` is missing OR a new theme was added, run
   `scripts/token-catalog.ps1` first.
3. Run `scripts/ui-check.ps1 -Files <paths>`.
4. Output: `UI: <summary> | TokenCoverage=<pct>%` or `OK` + per-issue lines.

## 6 rules

| Rule                       | Severity | Anchor                                |
|----------------------------|----------|---------------------------------------|
| `hardcoded-color`          | error    | `references/design-tokens.md`         |
| `unknown-token`            | error    | tokens absent from `known-tokens.json`|
| `non-canonical-spacing`    | warn     | `references/spacing-grid.md`          |
| `non-canonical-fontsize`   | warn     | `references/typography.md`            |
| `glyph-no-tooltip`         | warn     | `references/glyphs-catalog.md`        |
| `reinvented-style`         | warn     | `references/components.md`            |

`hardcoded-color` and `unknown-token` are errors because they break theme
switching at runtime. The four warnings flag stylistic drift; resolve when
they cluster, not every isolated case.

## Whitelist (NOT flagged)

- Anything under `Themes/` — themes own the literal colors.
- `ColorPicker` — its job is to manipulate raw color values.
- `glyph-no-tooltip` skips:
  - glyphs in icon slots (`<MenuItem.Icon>`, `<Button.Icon>`, etc.) where the
    parent has its own `Header=` / `Content=`.
  - elements using `Style="{StaticResource PanelIconButtonStyle}"` /
    `PanelIconToggleStyle` (the style sets `AutomationProperties.Name`).
- `unknown-token` only fires when the unknown key ends in
  `Brush|Color|Background|Foreground|Border|Fill|Stroke|Highlight|Accent`
  to avoid false positives on local style keys.

## Output format

```
UI: 3 hardcoded-color, 2 unknown-token, 1 non-canonical-fontsize | TokenCoverage=92%
  MainWindow.xaml:122  hardcoded-color  Background="#E81123"
  MainWindow.xaml:1687 unknown-token    {DynamicResource DockAccentBrush}
  Welcome.xaml:330     non-canonical-fontsize  FontSize="15"
```

## Catalogues consultables

- `data/known-tokens.json` — list of every `<Color>` and `<SolidColorBrush>`
  x:Key across all themes. Regenerate via `token-catalog.ps1` when a theme is
  added or a new token introduced.
- `data/glyphs.tsv` — every Segoe MDL2 codepoint used in the repo with usage
  count + first context. Use to dedupe glyphs and reuse existing meanings.
- `references/design-tokens.md` — token taxonomy + naming conventions.
- `references/spacing-grid.md` — canonical Padding / Margin scale.
- `references/typography.md` — FontSize ladder.
- `references/components.md` — list of shared styles / templates so I don't
  reinvent them.
- `references/glyphs-catalog.md` — pointer to `data/glyphs.tsv` + dedupe rules.

## What this skill does NOT do

- Does **not** fix hardcoded colors (matching color → token is unstable).
- Does **not** auto-add ToolTips. It signals the missing one.
- Does **not** verify XAML compiles (build is the user's call).
- Does **not** check accessibility beyond glyph-no-tooltip.

## Maintenance

- New theme file under `Themes/` → run `token-catalog.ps1` immediately so
  `unknown-token` sees the new keys.
- Periodically run `glyph-catalog.ps1` to refresh `data/glyphs.tsv` and spot
  duplicate glyphs across the codebase.
- New canonical spacing / FontSize discovered → add it to the arrays in
  `ui-check.ps1` AND document it in the matching reference file.
