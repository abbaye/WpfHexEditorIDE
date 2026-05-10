# Theme architecture

The repo defines 18 themes, all sharing the same key surface. The theming
contract is a pure ResourceDictionary swap — no per-theme code.

## File layout

```
Sources/Shell/WpfHexEditor.Shell/Themes/
    Dark/                       <- shared base templates
        Colors.xaml             palette tokens
        Menu.xaml               control templates (referenced by other themes)
        TabControl.xaml
        ContentControls.xaml
    Light/                      <- shared base templates (light palette)
    VS2022Dark/Colors.xaml      <- VS-accurate palette, reuses Dark/ templates
    Cyberpunk/Colors.xaml       <- color overrides only
    Dracula/Colors.xaml
    Forest/Colors.xaml
    ...

    DarkTheme.xaml              <- wrapper: merges Colors.xaml + templates
    CyberpunkTheme.xaml
    ...
```

## Reference theme

`Dark` is the **canonical reference**. Its `Colors.xaml` defines the full
key surface every other theme must satisfy. `data/reference-keys.txt` is
the extracted list, regenerated via `theme-parity.ps1 -RefreshReference`.

## Wrapper merge order

A `*Theme.xaml` wrapper looks like:

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <!-- 1. Colors first — defines tokens -->
        <ResourceDictionary Source="Cyberpunk/Colors.xaml" />
        <!-- 2. Templates second — consume tokens via DynamicResource -->
        <ResourceDictionary Source="pack://application:,,,/...;component/Themes/Dark/Menu.xaml" />
        <ResourceDictionary Source="pack://application:,,,/...;component/Themes/Dark/TabControl.xaml" />
        <ResourceDictionary Source="pack://application:,,,/...;component/Themes/Dark/ContentControls.xaml" />
        <ResourceDictionary Source="pack://application:,,,/...;component/Themes/PanelCommon.xaml" />
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

Order matters: templates reference tokens via `DynamicResource`, so
`Colors.xaml` must come **first**. The `theme-merge-order` rule enforces
this.

## Token surface (from Dark)

Prefixes (see `ui-design/references/design-tokens.md` for full taxonomy):

- `Dock*` / `DockMenu*` / `DockTab*` / `DockWindow*`
- `HexEditor_*`
- `Panel_*`
- `KSP_*`
- `ScrollBar*`

Every prefix in Dark must appear in every other theme. A token introduced in
one theme only is `theme-key-orphan` — either promote to all themes or
remove it.

## Adding a new theme

1. Create `Themes/<NewTheme>/Colors.xaml` by copying Dark and changing
   color literals.
2. Create `Themes/<NewTheme>Theme.xaml` wrapper merging Colors.xaml +
   shared templates (in that order).
3. Run `theme-parity.ps1 -Files Themes/<NewTheme>/Colors.xaml` to verify
   key parity.
4. Register the theme in the IDE's theme picker (out of scope of this
   skill).

## Adding a new token to all themes

1. Add `<Color>` + `<SolidColorBrush>` to Dark first.
2. Run `theme-parity.ps1 -RefreshReference` to update `reference-keys.txt`.
3. The skill will now report `theme-key-missing` on every other theme.
4. Propagate to all themes (manually or via `-SuggestPatch` to copy from
   Dark).
