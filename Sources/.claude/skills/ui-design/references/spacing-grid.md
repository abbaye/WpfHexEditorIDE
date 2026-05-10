# Spacing grid

The repo uses a **2-px-base** scale, not a strict 4-px grid. Most spacings are
multiples of 2 with a few canonical asymmetric pairs (`4,2`, `6,3`, `6,4`).

## Canonical values (used by `ui-check.ps1`)

```
0  1  2  3  4  5  6  8  10  12  14  16  18  20  22  24  26  28  30  32  40  48  56  64
```

Any `Padding=` / `Margin=` whose components are all canonical is accepted.
Negative values are accepted only if their absolute value is canonical.

## Common pairs in the codebase

| Usage                              | Value            |
|------------------------------------|------------------|
| TextBox / ComboBox padding         | `4,2`            |
| ComboBoxItem padding               | `6,3`            |
| DataGridColumnHeader padding       | `6,4`            |
| CheckBox content margin            | `0,3` (vertical) |
| Icon-button margin                 | `1,0`            |
| Toolbar inner padding              | `4,0`            |
| Indeterminate / dropdown contents  | `4,2,22,2`       |

## Fixed sizes (NOT spacing, but enforced)

| Element                 | Size       | Source              |
|-------------------------|------------|---------------------|
| Toolbar height          | `26`       | `PanelToolbarStyle` |
| Icon button             | `22 × 22`  | `PanelIconButtonStyle` |
| Icon font-size          | `13`       | `PanelIconButtonStyle` |
| CheckBox indicator      | `14 × 14`  | `DialogStyles.xaml` |
| CheckBox column         | `16` wide  | `DialogStyles.xaml` |
| ToggleButton arrow box  | `18` wide  | `DialogStyles.xaml` |

These fixed sizes are intentional (VS-style density). Don't deviate without
a reason.

## When the rule fires

Most legitimate hits are intentional negative offsets for badge anchoring
(e.g. `Margin="9,-5,-5,0"` on a notification badge). When the warning is
intentional, add a same-line comment such as `<!-- intentional offset -->`
and move on; the rule stays as a sanity check, not a blocker.
