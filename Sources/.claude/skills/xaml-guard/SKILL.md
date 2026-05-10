---
name: xaml-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  any *.xaml, *.resx, or *Resources.Designer.cs file to detect regressions in
  the high-incident XAML/resx/loc domain (hardcoded user-strings, XAML patcher
  corruption, resx malformed, key drift, HexEditor Resources alias, MessageBox
  in code-behind, ContextMenu transparency, DockPanel-row alignment). Skip on:
  pure structural attribute edits (Grid.Row, Margin, Width).
---

# xaml-guard (internal)

Layered guard for the XAML / resx / localization surface — the area with the
highest incident density in project memory (`adr_007_xaml_patcher_bug`,
`feedback_resx_satellite_corruption`, `feedback_localization_new_strings`,
`feedback_resources_alias_hexeditor`, `bug_contextmenu_transparent`,
`feedback_overflow_menu_alignment`).

## When I invoke

| Situation                                                    | Run? |
|--------------------------------------------------------------|------|
| Edit/Write on `*.xaml`                                       | yes  |
| Edit/Write on `*.resx`                                       | yes  |
| Edit/Write on `*Resources.Designer.cs`                       | yes  |
| New UserControl / Window / Page                              | yes  |
| Pure structural attribute edit (Grid.Row, Margin, Padding)   | no   |
| Code-behind .xaml.cs only                                    | no (covered by code-analysis) |

## Pipeline

1. After the edit batch, gather modified `*.xaml` and `*.resx` files.
2. Run `scripts/xaml-check.ps1 -Files <paths>` for XAML rules.
3. Run `scripts/resx-validate.ps1 -File <path>` for each resx (parse + Designer parity).
4. Sortie: `XAML: <n> issues | LocCoverage~<pct>%` or `OK` + per-issue lines.

## Detected rules (10)

See `references/xaml-rules.md` for the full table with regex and exceptions.

| Rule                  | Anchor memory                              |
|-----------------------|--------------------------------------------|
| `hardcoded-text`      | `feedback_localization_new_strings`        |
| `missing-dynamic`     | `project_phase6_localization_complete`     |
| `patcher-corruption`  | `adr_007_xaml_patcher_bug`                 |
| `xmlns-mangle`        | `adr_007_xaml_patcher_bug`                 |
| `resx-malformed`      | `feedback_resx_satellite_corruption`       |
| `designer-drift`      | (implicit; detected by resx-validate)      |
| `hexeditor-resources` | `feedback_resources_alias_hexeditor`       |
| `messagebox-show`     | `adr_009_themed_messagebox`                |
| `static-color-no-alpha` | `bug_contextmenu_transparent`            |
| `dockpanel-row-grid`  | `feedback_overflow_menu_alignment`         |

## Output format

```
XAML: 2 hardcoded-text, 1 missing-dynamic | LocCoverage=78%
  MainWindow.xaml:42  hardcoded-text  Header="Settings"
  MainWindow.xaml:67  hardcoded-text  Title="Open File"
  Toolbar.xaml:15     missing-dynamic Tooltip="Save"
```

`LocCoverage` is approximate (count of `{DynamicResource` vs count of attribute
strings starting with a capital). The exact figure is owned by the Phase-6
infra; this is a quick sanity check.

## What this skill does NOT do

- Does **not** rewrite XAML automatically.
- Does **not** translate strings or generate resx keys (see optional future
  `loc-key-helper` skill).
- Does **not** parse XAML semantically — regex + light XML parsing only.
- Does **not** verify XAML compiles (build is the user's call).

## Failure modes

- Whitelist false positives: technical attribute values like
  `Style="{StaticResource ...}"`, `x:Key=...`, `FontFamily="Segoe UI"`,
  brush names — see `references/xaml-rules.md` whitelist section.
- If a rule fires legitimately (rare), I document the exception in the same
  file and proceed.
