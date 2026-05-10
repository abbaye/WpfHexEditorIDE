# Forbidden patterns

Mechanically-detectable anti-patterns. The regex column is what
`post-edit-audit.ps1` uses; the rationale anchors each rule to project memory.

| Rule           | Regex                                                   | Why / Memory anchor                                |
|----------------|---------------------------------------------------------|----------------------------------------------------|
| `avalonedit`   | `\busing\s+ICSharpCode\b`                               | CodeEditor is 100% in-house; no AvalonEdit ever. `feedback_no_avalonedit` |
| `msgbox.show`  | `\bMessageBox\.Show\s*\(`                               | Use `IdeMessageBox` / `IDialogService`. `adr_009_themed_messagebox` |
| `hex-l10n`     | `(?<!L10n\s*=\s*)Resources\.[A-Z]\w+`  (only HexEditor) | `Resources.X` in HexEditor binds to UserControl, not the resx class. Alias as `L10n`. `feedback_resources_alias_hexeditor` |
| `static-mut`   | `\bpublic\s+static\s+(?!readonly)[A-Za-z_]\w*\s+[A-Za-z_]\w*\s*=` | New public static mutable state on hot paths is a perf/threading hazard |
| `md-in-sources`| n/a (path-based)                                        | `.md` files inside `Sources/` forbidden by global CLAUDE.md |

## Mental-only rules (no regex, but I must apply them)

- **Hardcoded user-visible string** in XAML or VM: must be a `DynamicResource`
  pulling from a `LocalizedResourceDictionary`.
  Anchor: `feedback_localization_new_strings`,
  `project_phase6_localization_complete`.
  Detection is hard via regex (false positives on technical strings) — I check
  manually for `Text="A..."`, `Header="A..."`, `Title="A..."`,
  `Content="A..."` patterns where the value starts with a capital and is not
  a code identifier.

- **Background "fix" without root cause**: any change that masks a symptom
  (try/catch swallow, retry-loop on a hang, defensive null check at the wrong
  layer) requires a `BUG` trigger and a memory entry. CLAUDE.md explicit.

- **Dispatcher.BeginInvoke as a fix for a layout race**: usually wrong.
  `feedback_wordwrap_column` and `adr_hexeditor_viewport_race` document the
  preferred fixes (column type swap, `Loaded` handler, property callback).

- **Python bulk-replace of curly quotes / mojibake**: forbidden.
  `feedback_python_encoding_fix` — restore via `git checkout` instead.

- **Sub-agent writing a `.resx` satellite directly**: validate the result with
  `ET.parse()`. `feedback_resx_satellite_corruption`.

- **XAML patcher `rep()` losing attribute name**: any DynamicResource patcher
  must preserve `Attr="..."` shape, not just the value.
  `adr_007_xaml_patcher_bug`.

## When a rule fires

1. Stop the current edit batch.
2. Print the rule + file + memory anchor.
3. Decide:
   - mechanical fix in same turn, OR
   - propose `PLAN` if the violation indicates a structural issue.

Never silence the rule. If a rule fires legitimately (rare exception), explain
why in the response and consider updating this file.
