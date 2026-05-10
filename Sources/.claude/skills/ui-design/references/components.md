# Reusable components

Before writing a new `<Style>` or `ControlTemplate`, check whether one of
these already covers the case. The `reinvented-style` rule warns when a
local style with `Setter Property="Template"` appears without `BasedOn=`.

## Implicit styles (no x:Key — apply automatically)

Defined in `WpfHexEditor.App/Themes/DialogStyles.xaml`. Merge that
ResourceDictionary into a Window's `<Window.Resources>` to inherit them.

| TargetType            | What it provides                                |
|-----------------------|-------------------------------------------------|
| `TextBlock`           | Foreground = DockMenuForegroundBrush            |
| `TextBox`             | Theme bg/fg + `Padding="4,2"`                   |
| `RadioButton`         | Foreground + `Margin="0,2"`                     |
| `CheckBox`            | VS2026-style 14×14 square indicator             |
| `ComboBox` + Item     | Themed dropdown with arrow                      |
| `ListBox` + Item      | Themed selection bar                            |
| `TreeView`            | Themed background + transparent border          |
| `DataGrid` + Row + ColumnHeader | Themed grid with horizontal lines     |

## Keyed styles for icon toolbars

Defined in `Sources/Docking/WpfHexEditor.Docking.Wpf/Themes/PanelCommon.xaml`.
Reference via `Style="{StaticResource PanelIconButtonStyle}"`.

| Key                        | Use                                              |
|----------------------------|--------------------------------------------------|
| `PanelToolbarStyle`        | Border for the panel toolbar row (26px high)     |
| `PanelIconButtonStyle`     | 22×22 transparent icon button (Segoe MDL2 Assets)|
| `PanelIconToggleStyle`     | 22×22 icon ToggleButton with check state         |
| `PanelToolbarSeparator`    | 1px vertical separator inside toolbar            |

## Title-bar buttons

Defined in each theme's `Colors.xaml`.

| Key                   | Use                                          |
|-----------------------|----------------------------------------------|
| `DockTitleButtonStyle`| Tab strip button (Hand cursor, hover/press) |
| `TitleBarButtonStyle` | Window chrome button (min/max/close)         |

## Localized resources alias for HexEditor

Inside HexEditor C# partials, `Resources.X` binds to `UserControl.Resources`,
NOT the resx class. Use:

```cs
using L10n = WPFHexaEditor.Properties.Resources;
```

Anchor: memory `feedback_resources_alias_hexeditor`.

## When a new style is justified

You need a brand new style if:
- The control type isn't in the implicit-styles table.
- You need a **visually distinct variant** of an existing styled control
  (e.g. flat borderless `TextBox` for a search bar).

In that case, declare:

```xml
<Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
    <!-- only the deltas -->
</Style>
```

`BasedOn={x:Type ...}` inherits the implicit style so theme switching keeps
working. Skipping `BasedOn=` and overriding `Template` is what the
`reinvented-style` rule catches.

## When NOT to write a new style

- "I just want different padding" → set `Padding=` directly on the instance.
- "I want it green" → set `Foreground={DynamicResource ...}` on the instance.
- "I want a tooltip" → set `ToolTip=` on the instance.

Local instance properties are always cheaper than a new style.
