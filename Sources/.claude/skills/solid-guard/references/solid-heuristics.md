# SOLID heuristics

## Disclaimer

These heuristics **correlate** with SOLID violations — they do not prove
them. The skill is **advisory**:

- All findings are warn-only.
- False positives are expected — annotate with `// solid-ok: <reason>`.
- The skill aims to surface patterns worth a human review, not to gate
  edits.

## 6 heuristics

| Rule                  | Detection                                                   | What it correlates with |
|-----------------------|-------------------------------------------------------------|--------------------------|
| `srp-mixed-concerns`  | class touches both `System.IO`/`File.*` AND `System.Windows.*` | Class glues IO to UI without an Adapter — SRP violation |
| `srp-class-too-broad` | >300 lines AND >15 public methods                           | Likely doing too much — split candidates |
| `ocp-massive-switch`  | switch with >10 cases on a type/enum                        | Adding a case modifies the class — replace by polymorphism |
| `dip-newing-services` | `new XService(` outside a Factory / composition-root        | Class binds itself to a concrete impl — inject instead |
| `dip-static-deps`     | `<Type>.Instance.<Member> = ...`                           | Mutable singleton — hidden global state |
| `isp-fat-interface`   | interface >10 members OR `throw new NotImplementedException` | Implementations forced to provide unrelated members |

## What is NOT detected

- **LSP** (Liskov Substitution): requires semantic analysis of subclass
  contract conformance. Skipped.
- **Cross-file SRP**: a class might be SRP-compliant alone but split a
  responsibility with another class. Out of regex scope.
- **DI container misuse**: e.g., service-locator anti-pattern. Skipped.
- **Implicit static deps via static methods**: `MyHelper.Compute()` with
  side effects looks innocent. Skipped.

## Severity rationale

Everything is warn because:

1. SOLID is a **design heuristic**, not a hard rule.
2. False positives are common (factories legitimately new services,
   parsers legitimately do massive switch).
3. A blocker would prevent legitimate work; a warning prompts a review.

If a project consistently runs into the same false positive, add the path
to the exemption regex in `solid-scan.ps1`.

## Refactor hint per rule

See `references/refactor-recipes.md` for the standard fix pattern of each
rule. Quick summary:

- `srp-mixed-concerns` → introduce an Adapter or Service that owns the IO,
  inject it into the UI class.
- `srp-class-too-broad` → identify cohesion clusters (methods sharing
  fields) and split into separate classes.
- `ocp-massive-switch` → replace by polymorphic dispatch (interface +
  implementations + factory) OR Strategy pattern.
- `dip-newing-services` → introduce ctor parameter, register in IoC.
- `dip-static-deps` → convert singleton to instance + injection.
- `isp-fat-interface` → split into role interfaces (e.g., `IReader`,
  `IWriter` instead of `IReadWrite`).

## Memory anchors

The repo has prior incidents and decisions that inform what NOT to flag:

- ADR-009 (themed MessageBox / `IDialogService`) — example of DIP applied
  correctly: instances inject via `IDEHostContext.Dialogs`.
- ADR-010 / ADR-011 (debugger / assembly-explorer plugin → core App) —
  module integration via SDK extensibility, not direct news.

When a new ADR documents a SOLID-relevant decision (e.g., a new
composition root), update the exemption regex AND mention the ADR here.
