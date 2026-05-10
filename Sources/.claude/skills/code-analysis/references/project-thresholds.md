# Project thresholds (WpfHexEditor)

Numeric limits enforced by `post-edit-audit.ps1` and used by `pre-edit-check.ps1`
to compute risk. Aligned with `CLAUDE.md` (global + project) and the existing
`WpfHexEditor.App/Analysis` collectors.

| Metric                       | Soft / Hard | Source                                      |
|------------------------------|-------------|---------------------------------------------|
| Lines per function           | 25          | global CLAUDE.md ("Functions <=25 lines")  |
| Lines per class              | 300         | project CLAUDE.md ("classes >300 lines" refactor trigger) |
| Cyclomatic complexity        | 10          | aligned with `ComplexityMetricsCollector`   |
| Nesting depth                | 4           | code-quality default                        |
| Halstead difficulty          | 30          | `HalsteadMetricsCollector` warning band     |
| Maintainability Index (MI)   | 65          | below = warn, below 50 = hot               |
| LCOM (cohesion)              | 0.7         | from `LcomCalculator`                       |
| Afferent + efferent coupling | 30          | `CouplingMetricsCollector` warning band     |
| Callers (P1 risk MED)        | 20          | scope-impact heuristic                      |

## Risk levels (P1)

- **HIGH**  : any class >300 lines OR >=3 functions >25 lines.
- **MED**   : >=20 callers OR >=1 function >25 lines.
- **LOW**   : everything else.

HIGH means: do not edit silently — propose a refactor PLAN first.

## Notes

- These thresholds are guards, not goals. A well-written 28-line method with
  3 calls is fine; a 24-line method with deep nesting is not. Treat the
  numeric verdict as a prompt to look closer, not as a verdict.
- For semantic quality (Halstead, MI, LCOM, coupling) defer to the in-app
  `CodeAnalysisRunner`. The skill checks only the cheap structural metrics
  that can be computed from a file in <50 ms.
