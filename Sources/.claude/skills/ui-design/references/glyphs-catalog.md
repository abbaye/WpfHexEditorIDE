# Glyphs catalog

The repo uses `Segoe MDL2 Assets` for icons everywhere (toolbars, menus,
title-bar, badges). `data/glyphs.tsv` is the auto-generated index of every
codepoint actually used in the codebase.

Generate / refresh it with:

```pwsh
pwsh -File scripts/glyph-catalog.ps1
```

## Why a catalog

- Reuse existing glyphs instead of inventing new mappings (e.g. don't pick
  a new "save" glyph if one is already used elsewhere).
- Spot accidental duplicates: same intent, two different codepoints.
- Provide a sanity check when migrating to a different icon font.

## Format

`data/glyphs.tsv` columns:

```
codepoint    count    example-context
U+E700       38       Sources\WpfHexEditor.App\MainWindow.xaml  <TextBlock Text="..."  Font...
U+E703       12       ...
```

Sort by codepoint to dedupe across files. Sort by count to see the most-used
glyphs.

## Conventions

- Always use `&#xCODE;` notation, never raw glyphs in the XAML source.
- FontSize for icon glyphs: `13` (matches `PanelIconButtonStyle`).
- Always pair with `Foreground={DynamicResource DockMenuForegroundBrush}` (or
  the theme-appropriate token) so the glyph respects theme switching.

## Accessibility

A glyph alone is **not accessible**. Either:

- Wrap it in an interactive element with `ToolTip=` AND
  `AutomationProperties.Name=` set to the localized label.
- Or use `Style="{StaticResource PanelIconButtonStyle}"` which sets
  `AutomationProperties.Name` from `(ToolTip)` automatically.

The `glyph-no-tooltip` rule warns when neither condition is met.

## Do not

- Don't mix MDL2 with Fluent Icons. Codepoints overlap partially but
  semantics differ (e.g. U+E700 is "GlobalNavButton" in MDL2 but a
  different glyph in Fluent).
- Don't introduce custom font files for icons unless the design genuinely
  requires it. MDL2 ships with Win10+ at zero cost.

## Updating

When a new domain (e.g. a new editor / plugin) introduces icons:

1. Pick codepoints from MDL2 that already appear in `glyphs.tsv` if
   semantics match.
2. Otherwise add fresh codepoints and re-run `glyph-catalog.ps1`.
3. Document the choice in the panel/control's header comment so reviewers
   can audit.
