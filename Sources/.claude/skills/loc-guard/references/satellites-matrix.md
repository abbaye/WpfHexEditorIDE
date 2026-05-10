# Satellites matrix

Each base `*Resources.resx` has its own subset of language satellites. The
`loc-parity.ps1` script auto-detects which exist by scanning the directory.

`data/satellites-snapshot.tsv` is refreshed via:

```pwsh
pwsh -File scripts/loc-parity.ps1 -Files <some.resx> -WriteSnapshot
```

Format (one row per assembly × language):

```
base               lang     keys   missing   placeholder-mismatch
AppResources       fr-FR    680    0         0
AppResources       ja-JP    677    3         0
AppResources       ar-SA    679    0         1
DocumentEditor...  fr-FR    312    0         0
```

## Coverage tiers (typical)

- **Tier 1 (28 langs)**: AppResources, EditorCoreResources — the user-facing
  IDE shell. Full coverage required.
- **Tier 2 (18 langs)**: most editors and plugins — frequent translation
  partial.
- **Tier 3 (en + fr only)**: small plugins, dev-experimental modules.

Tiers are **descriptive**, not enforced. Add a satellite freely; the script
picks it up automatically.

## When a satellite is intentionally missing

If a language is not yet supported for a given assembly, simply do not
create the file. `loc-parity.ps1` reports the count of detected satellites —
a small count vs other assemblies signals the tier mismatch but does not
fail.

## Adding a new language across the project

1. Create the satellite for AppResources (Tier 1) and translate the keys.
2. Run `loc-parity.ps1 -Files AppResources.<newlang>.resx` to verify
   missing/orphan/placeholder integrity.
3. Optionally extend to other assemblies. The skill flags missing/orphan
   keys per file but does not enforce coverage cross-assembly.

## Generating new keys (Phase 6 workflow)

1. Add the key to the base `*Resources.resx`.
2. Use `xaml-guard`'s `resx-validate.ps1` to confirm Designer.cs parity.
3. Run `loc-parity.ps1 -Files <base>.resx` — it will report the new key
   as missing in every satellite.
4. Translate (sub-agent delegation if 100+ keys; see
   `feedback_localization_agent_strategy`).
5. Re-run `loc-parity.ps1` to verify all satellites are now in parity.
