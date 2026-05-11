# NuGet protection conventions

## Why this skill exists

Several packages in this solution are published on nuget.org and have real
consumers downloading them every day (WPFHexaEditor: ~170k all-time;
WpfCodeEditor / WpfTerminal / WpfDocking: ~50/day fresh post-launch;
WpfHexEditor.Core.ByteProvider: ~50/day fresh post-launch). When an IDE
feature is added to `WpfHexEditor.App` or one of the `Editor.*` modules,
it is easy to accidentally pull a published package into the IDE shell's
dependency graph — and then quietly add a `using` or a `ProjectReference`
that breaks its standalone usability.

The skill enforces the **standalone-mode contract** of each protected
package: a downstream consumer who only references the package (no IDE,
no App) must still get a buildable, working artifact.

## The two contracts

### core-xplat
- Target: `net8.0` (no `-windows` suffix)
- `UseWPF` ⇒ false
- `UseWindowsForms` ⇒ false
- No `System.Windows.*`, no `System.Windows.Forms`, no `Microsoft.Web.WebView2.*`
- No `ProjectReference` into `WpfHexEditor.App*`, `WpfHexEditor.Editor.*` (except `.Core` subassemblies, which are contracts-only), `WpfHexEditor.Plugins.*`
- No reference to IDE-only types (`IDEHostContext`, `IdeMessageBox`, `IDialogService`, `DockManager`, …)

Packages: `WpfHexEditor.Core.ByteProvider`, `WpfHexEditor.Core.BinaryAnalysis`,
`whfmt.FileFormatCatalog`, `whfmt.Analysis`, `whfmt.Backfill`,
`whfmt.CodeGen`, `whfmt.Fuzz`, `whfmt.Validate`.

### wpf-control
- Target: `net8.0-windows`
- WPF allowed
- No `ProjectReference` to IDE shell (`App*`, `Editor.*` non-`.Core`, `Plugins.*`)
- No reference to IDE-only types
- Public API must remain stable (extensions OK, removals not)

Packages: `WPFHexaEditor`, `WpfCodeEditor`, `WpfDocking`, `WpfColorPicker`,
`WpfTerminal`.

## Public API rule (non-regression)

> You can ADD anything to a public surface. You cannot REMOVE or RENAME.

The skill compares the working tree against `git HEAD` for every edited
`.cs` in a protected package and reports:
- `nuget-api-removed` — signature gone (no near-match in same file)
- `nuget-api-renamed` — signature gone but a similar-named one appeared

Both block until reverted or until the change is explicitly justified.
Renames that **must** ship require a deprecation cycle: add the new API
alongside, mark the old `[Obsolete]`, and remove only at the next major
version bump (and only after the consumers documented in
`nuget_strategy.md` have been notified).

## Adding a new protected package

1. Verify the csproj has a `<PackageId>`, `<Version>`, `<Description>`,
   `<PackageLicenseExpression>`, README + icon packed.
2. Add an entry in `data/package-policy.json` with the appropriate
   category, TFM list, and `use_wpf`/`use_winforms` flags.
3. Smoke-test by running the script against an existing `.cs` of that
   package — confirm 0 false errors.
4. Update `MEMORY.md` (`project_nuget_guard.md`) with the addition and
   the category rationale.

## Suppression

`// nuget-ignore: <reason>` on the offending line. The `<reason>` is
mandatory and should reference an ADR or memory entry when the exception
is structural (e.g. WPF/WinForms allowed for an upcoming embedded preview).

## What this is NOT

- Not a SemVer enforcer (no AST diff). Use a dedicated tool like
  `dotnet-validate` or `Microsoft.DotNet.ApiCompat` at release time.
- Not a license / dependency-policy enforcer.
- Not a substitute for manual review on major versions.

## Memory anchors

- `nuget_strategy.md` — overall NuGet portfolio, download velocity, roadmap.
- `project_nuget_catalog_v11.md` — whfmt.FileFormatCatalog v1.1 utilities.
- `project_nuget_guard.md` — this skill's design rationale.
