# Loc coverage targets

The Phase-6 effort (`project_phase6_localization_complete`) reached 77.9% XAML
DynamicResource coverage on `Dev2026-04-27`. Remaining literals are proper
nouns, technical configs, code-behind-set, icon glyphs (intentionally not
translated).

## Targets

| Surface               | Target | Rationale                                |
|-----------------------|--------|------------------------------------------|
| New XAML files        | 95%+   | Greenfield — no excuse                   |
| Existing files edited | maintain or improve | Do not regress      |
| Solution-wide XAML    | 80%    | Phase-6 baseline + small uplift          |
| `*.resx` parity       | 100%   | Designer.cs must mirror keys             |

`xaml-check.ps1` reports `LocCoverage~<pct>%` on edited files only. The number
is approximate (counts `{DynamicResource` vs hardcoded user-visible attribute
values). It is a smoke signal, not a contract — the exact figure is the
Phase-6 infra's responsibility.

## When coverage drops

- Dropped on a single file edit → check if the new strings should be loc'd.
  Most should. Exceptions: glyphs, format placeholders, dev-only diagnostics.
- Dropped solution-wide → triggers a `BUG` investigation; usually means a
  recent batch added strings without resx entries.

## Adding new keys

- New user-visible string in a localizable surface (XAML, VM bound to UI):
  add the key to the appropriate `*Resources.resx` AND its 18+ satellites
  AND regenerate the Designer.cs.
- The 18 satellites approach is documented in
  `feedback_localization_agent_strategy` — split work via 3 sub-agents
  (resx+Designer / 18 satellites+infra / XAML+CS).
- `feedback_resx_satellite_corruption`: validate satellites with `ET.parse()`
  after sub-agent delegation.
