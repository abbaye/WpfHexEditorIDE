# Git Workflow Strategy for Avalonia Implementation

## 🌿 Branch Strategy

### Overview

For a major feature like Avalonia support, we'll use a **feature branch workflow** with multiple development branches to isolate work and protect the `master` branch.

```
master (stable)
  ↓
  ├─ feature/avalonia-support (main development branch)
  │   ↓
  │   ├─ feature/avalonia-core (Phase 1)
  │   ├─ feature/avalonia-wpf-refactor (Phase 2)
  │   ├─ feature/avalonia-facade (Phase 3)
  │   ├─ feature/avalonia-platform (Phase 4)
  │   └─ feature/avalonia-samples (Phase 5)
  │
  └─ hotfix/* (urgent fixes on master)
```

---

## 📋 Branch Structure

### Main Branches

| Branch | Purpose | Protection |
|--------|---------|------------|
| `master` | Production-ready code (v2.x currently) | ✅ Protected |
| `feature/avalonia-support` | Main Avalonia development | ⚠️ Require PR review |

### Feature Branches (from `feature/avalonia-support`)

| Branch | Phase | Description |
|--------|-------|-------------|
| `feature/avalonia-core` | Phase 1 | Create Core project + abstractions |
| `feature/avalonia-wpf-refactor` | Phase 2 | Refactor WPF to use abstractions |
| `feature/avalonia-facade` | Phase 3 | Create backward-compatible facade |
| `feature/avalonia-platform` | Phase 4 | Implement Avalonia platform layer |
| `feature/avalonia-controls` | Phase 4 | Port controls to Avalonia |
| `feature/avalonia-samples` | Phase 5 | Create sample applications |
| `feature/avalonia-ci` | Phase 7 | CI/CD configuration |

---

## 🚀 Implementation Workflow

### Phase 0: Initialize Development Branch

```bash
# Ensure master is up to date
git checkout master
git pull origin master

# Create main development branch
git checkout -b feature/avalonia-support
git push -u origin feature/avalonia-support

# Protect branch on GitHub (Settings → Branches)
# ✅ Require pull request reviews before merging
# ✅ Require status checks to pass
# ⚠️ Do not allow force push
```

---

### Phase 1: Core Project (Week 1-2)

```bash
# Create feature branch from avalonia-support
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-core

# Work on Phase 1
# - Create WpfHexaEditor.Core project
# - Add platform abstractions
# - Move portable code

# Commit regularly
git add .
git commit -m "feat(core): Create Core project structure"
git commit -m "feat(core): Add Platform/Rendering abstractions"
git commit -m "feat(core): Move Core/Bytes to Core project"
git commit -m "feat(core): Move Services to Core project"

# Push to remote
git push -u origin feature/avalonia-core

# Create Pull Request: feature/avalonia-core → feature/avalonia-support
# Review, test, merge
```

**Merge Criteria:**
- ✅ All unit tests pass
- ✅ Code compiles without warnings
- ✅ Code review approved
- ✅ No breaking changes to existing code

---

### Phase 2: WPF Refactoring (Week 3-4)

```bash
# Create new feature branch
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-wpf-refactor

# Work on Phase 2
# - Rename WPFHexaEditor → WpfHexaEditor.Wpf
# - Implement WPF platform layer
# - Migrate controls to abstractions

git add .
git commit -m "refactor(wpf): Rename project to WpfHexaEditor.Wpf"
git commit -m "feat(wpf): Implement WPF platform implementations"
git commit -m "refactor(wpf): Migrate HexViewport to IDrawingContext"
git commit -m "refactor(wpf): Migrate remaining controls"

git push -u origin feature/avalonia-wpf-refactor

# PR: feature/avalonia-wpf-refactor → feature/avalonia-support
```

**Merge Criteria:**
- ✅ All WPF tests pass (visual + functional)
- ✅ No performance regression
- ✅ Sample app works correctly
- ✅ Zero breaking changes to public API

---

### Phase 3: Facade Creation (Week 5)

```bash
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-facade

# Work on Phase 3
# - Create WpfHexaEditor facade project
# - Implement type forwarding
# - Configure deprecation warnings

git add .
git commit -m "feat(facade): Create WpfHexaEditor facade project"
git commit -m "feat(facade): Add type forwarding to Wpf package"
git commit -m "feat(facade): Configure deprecation metadata"

git push -u origin feature/avalonia-facade

# PR: feature/avalonia-facade → feature/avalonia-support
```

---

### Phase 4: Avalonia Implementation (Week 6-7)

```bash
# Platform layer
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-platform

git add .
git commit -m "feat(avalonia): Create Avalonia project structure"
git commit -m "feat(avalonia): Implement Avalonia platform layer"
git push -u origin feature/avalonia-platform

# Controls
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-controls

git commit -m "feat(avalonia): Port HexViewport to Avalonia"
git commit -m "feat(avalonia): Port HexEditor control"
git commit -m "feat(avalonia): Port remaining controls"
git push -u origin feature/avalonia-controls

# Merge both when ready
```

---

### Phase 5: Samples & Testing (Week 8)

```bash
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-samples

git commit -m "feat(samples): Create Avalonia sample app"
git commit -m "feat(samples): Add multi-platform testing"
git commit -m "docs(samples): Add README for samples"

git push -u origin feature/avalonia-samples
```

---

### Phase 6-7: Polish & CI/CD (Week 9-10)

```bash
# Themes
git checkout -b feature/avalonia-themes
git commit -m "feat(themes): Port Dark theme to Avalonia"
git commit -m "feat(themes): Port Light theme to Avalonia"
git commit -m "feat(themes): Port Cyberpunk theme to Avalonia"

# CI/CD
git checkout -b feature/avalonia-ci
git commit -m "ci: Add GitHub Actions workflow for multi-platform"
git commit -m "ci: Add NuGet packaging workflow"
git commit -m "ci: Add automated tests"
```

---

## 🔄 Merging Strategy

### Merge to `feature/avalonia-support`

**When:** Each phase is complete and tested

**Process:**
```bash
# Update main dev branch
git checkout feature/avalonia-support
git pull origin feature/avalonia-support

# Merge feature branch (use --no-ff for clear history)
git merge --no-ff feature/avalonia-core
git push origin feature/avalonia-support

# Delete merged feature branch
git branch -d feature/avalonia-core
git push origin --delete feature/avalonia-core
```

**Commit Message Template:**
```
Merge feature/avalonia-core into feature/avalonia-support

Phase 1 complete:
- Created WpfHexaEditor.Core project (netstandard2.0)
- Added platform abstractions (Rendering, Media, Input, Threading)
- Moved 20,000+ lines of portable code to Core
- 100% unit test coverage for Core

Related to #153
```

---

### Merge to `master` (Release v3.0)

**When:** All phases complete, fully tested, ready for release

**Process:**
```bash
# Ensure all feature branches merged to avalonia-support
git checkout feature/avalonia-support
git pull origin feature/avalonia-support

# Final testing
dotnet test
dotnet build -c Release

# Ensure master is up to date
git checkout master
git pull origin master

# Merge with --no-ff to preserve history
git merge --no-ff feature/avalonia-support

# Tag the release
git tag -a v3.0.0 -m "Release v3.0.0 - Avalonia Support

Major release adding cross-platform support via Avalonia UI.

Features:
- ✨ Avalonia UI support (Windows, Linux, macOS)
- 📦 Platform-agnostic architecture (85% shared code)
- 🔄 Backward-compatible transition (Strategy C)
- 📚 Comprehensive documentation

Breaking Changes:
- Package renamed: WpfHexaEditor → WpfHexaEditor.Wpf (recommended)
- Old package deprecated but still works

Migration:
See docs/Avalonia/COMPATIBILITY_STRATEGY.md

Related: #118, #135, #153
"

# Push to remote
git push origin master
git push origin v3.0.0

# Delete feature branch (optional, can keep for history)
git branch -d feature/avalonia-support
git push origin --delete feature/avalonia-support
```

---

## 🛡️ Branch Protection Rules

### `master` Branch

**Settings → Branches → Add rule for `master`:**

✅ **Require pull request reviews before merging**
- Required approving reviews: 1
- Dismiss stale pull request approvals when new commits are pushed

✅ **Require status checks to pass before merging**
- Require branches to be up to date before merging
- Status checks: `build`, `test`, `lint`

✅ **Require conversation resolution before merging**

✅ **Require signed commits** (optional but recommended)

❌ **Do not allow force pushes**

❌ **Do not allow deletions**

---

### `feature/avalonia-support` Branch

**Settings → Branches → Add rule for `feature/avalonia-support`:**

✅ **Require pull request reviews before merging**
- Required approving reviews: 1

✅ **Require status checks to pass before merging**
- Status checks: `build`, `test`

⚠️ **Allow force pushes** (for rebasing/squashing during development)

❌ **Do not allow deletions** (keep until merged to master)

---

## 📝 Commit Message Convention

Use **Conventional Commits** for clear history:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types:
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code refactoring (no functional change)
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `ci`: CI/CD changes
- `chore`: Maintenance tasks

### Scopes:
- `core`: WpfHexaEditor.Core project
- `wpf`: WpfHexaEditor.Wpf project
- `avalonia`: WpfHexaEditor.Avalonia project
- `facade`: WpfHexaEditor facade project
- `samples`: Sample applications
- `docs`: Documentation

### Examples:

```bash
# Good commits
git commit -m "feat(core): Add IDrawingContext abstraction"
git commit -m "refactor(wpf): Migrate HexViewport to use IDrawingContext"
git commit -m "feat(avalonia): Implement AvaloniaDrawingContext wrapper"
git commit -m "docs(readme): Update installation instructions for v3.0"
git commit -m "ci: Add GitHub Actions workflow for multi-platform builds"

# Bad commits (avoid)
git commit -m "WIP"
git commit -m "fix stuff"
git commit -m "update"
```

---

## 🔍 Pull Request Template

Create `.github/pull_request_template.md`:

```markdown
## Description
<!-- Brief description of changes -->

## Related Issues
Closes #
Related to #153

## Type of Change
- [ ] New feature (feat)
- [ ] Bug fix (fix)
- [ ] Refactoring (refactor)
- [ ] Documentation (docs)
- [ ] CI/CD (ci)

## Phase
- [ ] Phase 1: Core Creation
- [ ] Phase 2: WPF Refactoring
- [ ] Phase 3: Facade Creation
- [ ] Phase 4: Avalonia Implementation
- [ ] Phase 5: Testing & Samples
- [ ] Phase 6: Polish
- [ ] Phase 7: CI/CD

## Checklist
- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] Added/updated unit tests
- [ ] Updated documentation
- [ ] No breaking changes (or documented)
- [ ] Follows commit message convention
- [ ] Self-reviewed code

## Testing
<!-- How was this tested? -->
- [ ] Unit tests
- [ ] Integration tests
- [ ] Manual testing
- [ ] Tested on: Windows / Linux / macOS

## Screenshots (if applicable)
<!-- Add screenshots for UI changes -->

## Notes for Reviewers
<!-- Any additional context -->
```

---

## 📊 Workflow Diagram

```
┌─────────────────────────────────────────────────────────┐
│                        master                            │
│                   (stable v2.x)                          │
└──────────────────────┬──────────────────────────────────┘
                       │
                       │ branch
                       ▼
┌─────────────────────────────────────────────────────────┐
│              feature/avalonia-support                    │
│              (main development branch)                   │
└──┬────┬────┬────┬────┬────┬────────────────────────────┘
   │    │    │    │    │    │
   │    │    │    │    │    └─► feature/avalonia-ci (Phase 7)
   │    │    │    │    └──────► feature/avalonia-samples (Phase 5)
   │    │    │    └───────────► feature/avalonia-controls (Phase 4)
   │    │    └────────────────► feature/avalonia-platform (Phase 4)
   │    └─────────────────────► feature/avalonia-facade (Phase 3)
   └──────────────────────────► feature/avalonia-wpf-refactor (Phase 2)
   └──────────────────────────► feature/avalonia-core (Phase 1)

   Each merges back to feature/avalonia-support via PR

   When all phases complete:
   feature/avalonia-support ──► master (v3.0 release)
```

---

## 🎯 Git Commands Quick Reference

### Starting a new phase
```bash
git checkout feature/avalonia-support
git pull origin feature/avalonia-support
git checkout -b feature/avalonia-<phase-name>
```

### Regular commits during development
```bash
git add <files>
git commit -m "feat(scope): description"
git push origin feature/avalonia-<phase-name>
```

### Creating a Pull Request
```bash
# Push latest changes
git push origin feature/avalonia-<phase-name>

# Go to GitHub and create PR:
# feature/avalonia-<phase-name> → feature/avalonia-support
```

### Updating feature branch with latest changes
```bash
git checkout feature/avalonia-<phase-name>
git fetch origin
git rebase origin/feature/avalonia-support

# Or if you prefer merge
git merge origin/feature/avalonia-support
```

### Cleaning up after merge
```bash
# Delete local branch
git branch -d feature/avalonia-<phase-name>

# Delete remote branch
git push origin --delete feature/avalonia-<phase-name>
```

---

## ⚠️ Important Notes

### DO:
✅ Work in feature branches, not directly on `feature/avalonia-support`
✅ Create PRs for all merges (even to dev branch)
✅ Write descriptive commit messages
✅ Keep commits small and focused
✅ Test before creating PR
✅ Update documentation as you go
✅ Rebase on conflicts (not merge) during development

### DON'T:
❌ Push directly to `master`
❌ Force push to shared branches (except your own feature branch)
❌ Merge without PR review
❌ Commit commented-out code or debug statements
❌ Commit large binary files
❌ Use generic commit messages ("fix", "update", etc.)

---

## 📋 Phase-by-Phase Checklist

### Phase 1: Core Creation
- [ ] Create `feature/avalonia-core` branch
- [ ] Implement changes
- [ ] All tests pass
- [ ] Create PR to `feature/avalonia-support`
- [ ] Code review + approval
- [ ] Merge PR
- [ ] Delete feature branch

### Phase 2: WPF Refactoring
- [ ] Create `feature/avalonia-wpf-refactor` branch
- [ ] Implement changes
- [ ] Visual regression tests pass
- [ ] Performance benchmarks pass
- [ ] Create PR to `feature/avalonia-support`
- [ ] Code review + approval
- [ ] Merge PR
- [ ] Delete feature branch

### ... (repeat for all phases)

### Final Release
- [ ] All phases merged to `feature/avalonia-support`
- [ ] Full integration testing
- [ ] Documentation complete
- [ ] Create PR: `feature/avalonia-support` → `master`
- [ ] Final review + approval
- [ ] Merge to master
- [ ] Tag v3.0.0
- [ ] Publish NuGet packages
- [ ] Create GitHub release
- [ ] Close related issues

---

## 🎓 Learning Resources

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/)
- [GitHub Flow](https://guides.github.com/introduction/flow/)
- [Semantic Versioning](https://semver.org/)

---

**Last Updated:** 2026-02-16
**Status:** 📋 Ready for implementation
**Related:** [IMPLEMENTATION_ROADMAP.md](./IMPLEMENTATION_ROADMAP.md)
