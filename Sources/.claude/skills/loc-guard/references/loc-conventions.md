# Localization conventions

## Key naming

Each base `*Resources.resx` uses an assembly-specific prefix to avoid
collision in the merged dictionaries. Conventions captured during Phase 5/6:

| Prefix          | Assembly                                   |
|-----------------|--------------------------------------------|
| `APP_`          | WpfHexEditor.App                           |
| `HE_`           | WPFHexaEditor (HexEditor control)          |
| `CD_`           | WpfHexEditor.Plugins.ClassDiagram          |
| `DocEd_`        | WpfHexEditor.Editor.DocumentEditor         |
| `DBG_`          | WpfHexEditor.App/Debug                     |
| `DS_`           | WpfHexEditor.Plugins.DocumentStructure     |
| `Git_`          | WpfHexEditor.Plugins.Git                   |
| `AsmExplorer_`  | WpfHexEditor.App/AssemblyExplorer          |
| `XD_`           | XAML Designer                              |
| `PA_`           | PatternAnalysis                            |
| `PF_`           | ParsedFields                               |
| `RX_`           | ResxLocalization                           |
| `SE_`           | ScriptEditor                               |
| `SR_`           | ScriptRunner                               |
| `FS_`           | FileStatistics                             |
| `FI_`           | FormatInfo                                 |
| `AI_`           | AIAssistant                                |

The prefix is **not** validated by `loc-guard` (no enforcement) — it is a
convention. Following it is encouraged for new keys.

## File layout per assembly

```
WpfHexEditor.App/Properties/
    AppResources.resx              <- base (en, defines key set)
    AppResources.fr-FR.resx
    AppResources.fr-CA.resx
    AppResources.de-DE.resx
    ... (28 satellites total for AppResources)
    AppResources.Designer.cs       <- generated, validated by xaml-guard
```

## 28 supported languages (AppResources)

`ar-SA`, `cs-CZ`, `da-DK`, `de-DE`, `el-GR`, `es-419`, `es-ES`, `fi-FI`,
`fr-CA`, `fr-FR`, `hi-IN`, `hu-HU`, `id-ID`, `it-IT`, `ja-JP`, `ko-KR`,
`nl-NL`, `pl-PL`, `pt-BR`, `pt-PT`, `ro-RO`, `ru-RU`, `sv-SE`, `th-TH`,
`tr-TR`, `uk-UA`, `vi-VN`, `zh-CN`.

Smaller assemblies often have fewer satellites (18 is also common). The
script auto-detects what exists.

## Placeholders

Use indexed placeholders `{0}`, `{1}`, `{2:N0}` (with format spec). The same
**indices** must appear in every translation:

✅ Base: `"{0} files in {1}"`  Satellite: `"{1} fichiers dans {0}"`  (reorder OK)
❌ Base: `"{0} files in {1}"`  Satellite: `"{0} fichiers"`  (drops {1} — placeholder-mismatch)

## Untranslated detection

A satellite value identical to the base value is flagged as `untranslated`
(warn) for non-`en-*` cultures. This catches the "TODO: translate later"
pattern that ships placeholder-only satellites.

For values that are **legitimately the same** in another language (proper
nouns, technical terms, version strings), the warning is acceptable noise.
The rule remains warn-only for that reason.

## Memory anchors

- `feedback_localization_new_strings` — every new user-visible string must
  be a key, not a literal in XAML/code.
- `feedback_localization_agent_strategy` — when adding 100+ keys, split
  into 3 sub-agents (resx+Designer / 18 satellites+infra / XAML+CS).
- `feedback_resx_satellite_corruption` — sub-agents may overwrite satellites
  with `<data>` fragments only; validate with `ET.parse()` after delegation.
- `adr_005_phase5_localization` — overall localization architecture.
