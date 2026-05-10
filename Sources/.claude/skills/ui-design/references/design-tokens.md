# Design tokens

The repo defines its UI palette as ResourceDictionary keys (`<Color>` /
`<SolidColorBrush>`) in 18 themes under:

- `Sources/Shell/WpfHexEditor.Shell/Themes/<ThemeName>/Colors.xaml` — full themes
- `Sources/Docking/WpfHexEditor.Docking.Wpf/Themes/Dark/Colors.xaml`,
  `.../Light/Colors.xaml` — base palettes for the docking layer

Every theme exposes the **same key surface** so theme switching is a pure
ResourceDictionary swap. `data/known-tokens.json` is the union of all keys.

## Naming conventions (prefixes)

| Prefix              | Domain                                              |
|---------------------|-----------------------------------------------------|
| `Dock*`             | Dockable surface — tab strips, splitters, overlays  |
| `DockMenu*`         | Menu bar / toolbar                                  |
| `DockTab*`          | Tab headers (active / inactive / hover / text)      |
| `DockWindow*`       | Window chrome / background                          |
| `HexEditor_*`       | Hex viewport — bytes, ASCII, selection, status      |
| `Panel_*`           | Tool-window panels (PanelToolbar, PanelIconButton)  |
| `KSP_*`             | Keyboard Shortcuts Page                             |
| `ScrollBar*`        | Custom scrollbar palette                            |

Always pair `<Color x:Key="XBrushColor">` with
`<SolidColorBrush x:Key="XBrush" Color="{StaticResource XBrushColor}"/>`.

## Color → Brush rule

UI components reference brushes (`{DynamicResource ...Brush}`), never raw
colors. `<Color>` resources exist only inside `Colors.xaml` files for theme
authoring. If a control needs a color (e.g. `LinearGradientBrush.GradientStop`),
still bind via `Color="{DynamicResource ...Color}"`.

## Live-switch requirement

Bindings MUST use `DynamicResource` (not `StaticResource`) for theme switching
to work at runtime. `StaticResource` is acceptable only for keys that are
truly immutable (geometry data, glyph codepoints).

## Where literal colors are allowed

- `Themes/**/*.xaml` — these define the palette.
- `*ColorPicker*` — its purpose is raw color manipulation.
- Inline gradient seeds inside a token-driven `LinearGradientBrush` (rare).
- Anywhere else a literal color appears, prefer adding a new token over
  hardcoding.

## Adding a new token

1. Pick a name following the prefix convention (e.g. `DockAccentBrush`).
2. Add `<Color>` + `<SolidColorBrush>` to **every** theme's `Colors.xaml`.
   Failing to add to all 18 themes leaves runtime gaps when the user
   switches.
3. Run `scripts/token-catalog.ps1` to regenerate `data/known-tokens.json`.
4. Replace literal usages with `{DynamicResource <NewToken>}`.

## Outstanding gaps detected (live state)

These tokens are referenced but absent from `known-tokens.json`. Treat as
debt to resolve:

- `DockAccentBrush` — used in 24 files, defined in zero theme. Either add
  to every `Colors.xaml` or replace usages with an existing token.

This list is regenerated implicitly each time `ui-check.ps1` reports
`unknown-token`.
