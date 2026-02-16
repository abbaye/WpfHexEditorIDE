# GitHub Project Setup - Avalonia Implementation

This guide explains how to create and configure the official GitHub Project for tracking Avalonia implementation progress.

---

## 📋 Project Overview

**Name:** Avalonia Support Implementation
**Owner:** @abbaye (WpfHexEditorControl)
**Type:** Organization/User Project (Board)
**Visibility:** Public
**Template:** Team backlog

---

## 🎯 Project Creation

### Step 1: Create Project

1. Go to: https://github.com/abbaye/WpfHexEditorControl
2. Click **Projects** tab
3. Click **New project**
4. Choose template: **Team backlog**
5. Enter project name: **Avalonia Support Implementation**
6. Enter description:

```
Complete implementation project for adding Avalonia UI support to WPFHexaEditor Control.

Goal: Enable cross-platform support (Windows, Linux, macOS) while maintaining backward compatibility.

Strategy: Strategy C (Transition Period)
Timeline: 11 weeks (Phases 0-8)
Target Release: v3.0.0

Documentation: https://github.com/abbaye/WpfHexEditorControl/tree/master/docs/Avalonia

Related Issues: #118, #135, #153
```

7. Click **Create project**

---

## 📊 Board Configuration

### Default Columns

The Team Backlog template provides these columns:
- 📋 **Backlog** - Future work
- 📝 **Ready** - Planned for next sprint
- 🏃 **In Progress** - Currently being worked on
- 👀 **In Review** - Pull Request created
- ✅ **Done** - Completed

### Custom Fields (Add these)

1. **Phase** (Single select)
   - Phase 0: Preparation
   - Phase 1: Core Creation
   - Phase 2: WPF Refactoring
   - Phase 3: Facade Creation
   - Phase 4: Avalonia Implementation
   - Phase 5: Testing & Samples
   - Phase 6: Themes & Polish
   - Phase 7: CI/CD
   - Phase 8: Release

2. **Priority** (Single select)
   - P0: Critical
   - P1: High
   - P2: Medium
   - P3: Low

3. **Complexity** (Single select)
   - XS: 1-2 hours
   - S: 2-4 hours
   - M: 1-2 days
   - L: 3-5 days
   - XL: 1-2 weeks

4. **Branch** (Text)
   - Track which feature branch the work is on

---

## 🏷️ Milestones

Create the following milestones in **Issues → Milestones**:

### Milestone 1: v3.0.0-alpha (Development Complete)
- **Due date:** ~11 weeks from start
- **Description:** All phases complete, ready for testing
- **Issues:** All Phase 1-7 tasks

### Milestone 2: v3.0.0-beta (Testing Complete)
- **Due date:** +2 weeks after alpha
- **Description:** Multi-platform testing complete, bugs fixed
- **Issues:** Testing and bug fixes

### Milestone 3: v3.0.0 (Public Release)
- **Due date:** +1 week after beta
- **Description:** Public release with Avalonia support
- **Issues:** Release tasks, NuGet publishing

### Milestone 4: v4.0.0 (Transition Complete)
- **Due date:** +6-12 months after v3.0
- **Description:** Remove deprecated package, clean architecture
- **Issues:** Deprecation removal

---

## 📝 Issues to Create

### Phase 0: Preparation (Already Complete)

**Issue #154:** ✅ Complete Avalonia planning and documentation
- [x] Codebase analysis
- [x] Architecture design
- [x] Create documentation (7 guides)
- [x] Community consultation
- **Labels:** `documentation`, `planning`, `phase-0`
- **Milestone:** v3.0.0-alpha
- **Assignee:** @abbaye

---

### Phase 1: Core Project Creation

**Issue #155:** Create WpfHexaEditor.Core project (netstandard2.0)
```markdown
## Description
Create new platform-agnostic Core project targeting netstandard2.0

## Tasks
- [ ] Create WpfHexaEditor.Core.csproj
- [ ] Add platform abstractions
  - [ ] Platform/Rendering (IDrawingContext)
  - [ ] Platform/Media (PlatformColor, IBrush)
  - [ ] Platform/Input (PlatformKey)
  - [ ] Platform/Threading (IPlatformTimer)
- [ ] Configure project settings
- [ ] Add XML documentation

## Branch
feature/avalonia-core

## Acceptance Criteria
- Project compiles without errors
- All abstractions defined
- XML comments complete

## Related
#153
```
- **Labels:** `enhancement`, `phase-1`, `core`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** M

**Issue #156:** Move portable code to Core project
```markdown
## Description
Move all platform-agnostic code from WPFHexaEditor to Core

## Tasks
- [ ] Move Core/Bytes (~9,522 lines)
- [ ] Move Core/CharacterTable (~1,891 lines)
- [ ] Move Core/Interfaces (~580 lines)
- [ ] Move Core/MethodExtension (~1,057 lines)
- [ ] Move Services (~4,305 lines)
- [ ] Move ViewModels (~2,500 lines)
- [ ] Move Models (~1,500 lines)
- [ ] Move Events (~2,173 lines)
- [ ] Update namespaces
- [ ] Resolve dependencies
- [ ] Update references in WPFHexaEditor

## Branch
feature/avalonia-core

## Acceptance Criteria
- All portable code in Core project
- Namespaces updated correctly
- No breaking changes to public API
- All code compiles

## Related
#153, #155
```
- **Labels:** `refactoring`, `phase-1`, `core`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** L

**Issue #157:** Add unit tests for Core project
```markdown
## Description
Create comprehensive unit tests for Core business logic

## Tasks
- [ ] Test Core/Bytes classes
- [ ] Test Services
- [ ] Test ViewModels
- [ ] Test platform abstractions (mocked)
- [ ] Achieve >70% code coverage

## Branch
feature/avalonia-core

## Acceptance Criteria
- All tests pass
- Code coverage >70%
- CI integration working
```
- **Labels:** `testing`, `phase-1`, `core`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** L

---

### Phase 2: WPF Refactoring

**Issue #158:** Rename WPFHexaEditor to WpfHexaEditor.Wpf
```markdown
## Description
Rename project and update references

## Tasks
- [ ] Rename project file
- [ ] Update assembly name
- [ ] Add reference to WpfHexaEditor.Core
- [ ] Add DefineConstants: WPF
- [ ] Update namespaces
- [ ] Update XAML references
- [ ] Update sample projects

## Branch
feature/avalonia-wpf-refactor

## Acceptance Criteria
- Project compiles
- Sample app works
- No breaking changes to users
```
- **Labels:** `refactoring`, `phase-2`, `wpf`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** M

**Issue #159:** Implement WPF platform layer
```markdown
## Description
Implement WPF-specific platform implementations

## Tasks
- [ ] WpfDrawingContext (wrap DrawingContext)
- [ ] WpfFormattedText
- [ ] WpfTypeface
- [ ] WpfBrush/WpfPen
- [ ] WpfKeyConverter
- [ ] WpfDispatcherTimer

## Branch
feature/avalonia-wpf-refactor

## Related
#153, #155
```
- **Labels:** `enhancement`, `phase-2`, `wpf`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** M

**Issue #160:** Migrate WPF controls to use abstractions
```markdown
## Description
Adapt all WPF controls to use platform abstractions

## Tasks
- [ ] Migrate HexViewport.cs (P0)
- [ ] Migrate BarChartPanel.cs (P0)
- [ ] Migrate Caret.cs (P0)
- [ ] Migrate ScrollMarkerPanel.cs (P0)
- [ ] Migrate HexEditor.xaml.cs (P1)
- [ ] Migrate HexBox.xaml.cs (P1)
- [ ] Migrate BaseByte, FastTextLine, StringByte (P2)
- [ ] Update RelayCommand.cs

## Branch
feature/avalonia-wpf-refactor

## Acceptance Criteria
- All controls use IDrawingContext
- Visual regression tests pass
- Performance benchmarks acceptable
- Zero breaking changes
```
- **Labels:** `refactoring`, `phase-2`, `wpf`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** XL

---

### Phase 3: Facade Creation

**Issue #161:** Create WpfHexaEditor facade package
```markdown
## Description
Create backward-compatible facade for Strategy C

## Tasks
- [ ] Create WpfHexaEditor project (facade)
- [ ] Implement type forwarding (~50 types)
- [ ] Configure NuGet deprecation metadata
- [ ] Add dependency on WpfHexaEditor.Wpf
- [ ] Test backward compatibility

## Branch
feature/avalonia-facade

## Acceptance Criteria
- Existing v2.x projects upgrade successfully
- Deprecation warnings shown in VS
- All APIs work through facade
```
- **Labels:** `enhancement`, `phase-3`, `compatibility`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** S

---

### Phase 4: Avalonia Implementation

**Issue #162:** Create WpfHexaEditor.Avalonia project
```markdown
## Description
Create new Avalonia project structure

## Tasks
- [ ] Create WpfHexaEditor.Avalonia.csproj (net8.0)
- [ ] Add Avalonia packages (11.0+)
- [ ] Add reference to WpfHexaEditor.Core
- [ ] Configure DefineConstants: AVALONIA
- [ ] Setup project structure

## Branch
feature/avalonia-platform

## Related
#153
```
- **Labels:** `enhancement`, `phase-4`, `avalonia`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** S

**Issue #163:** Implement Avalonia platform layer
```markdown
## Description
Implement Avalonia-specific platform implementations

## Tasks
- [ ] AvaloniaDrawingContext
- [ ] AvaloniaFormattedText
- [ ] AvaloniaTypeface
- [ ] AvaloniaBrush/AvaloniaPen
- [ ] AvaloniaKeyConverter
- [ ] AvaloniaDispatcherTimer

## Branch
feature/avalonia-platform

## Related
#153, #162
```
- **Labels:** `enhancement`, `phase-4`, `avalonia`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** M

**Issue #164:** Port controls to Avalonia
```markdown
## Description
Port all WPF controls to Avalonia

## Tasks
- [ ] Port HexViewport.cs
- [ ] Port BarChartPanel.cs
- [ ] Port Caret.cs
- [ ] Port ScrollMarkerPanel.cs
- [ ] Create HexEditor.axaml + code-behind
- [ ] Create HexBox.axaml + code-behind
- [ ] Port converters (15 classes)
- [ ] Port dialogs (FindReplace, Goto, etc.)

## Branch
feature/avalonia-controls

## Acceptance Criteria
- All controls functional
- Basic features working
- Compiles without errors
```
- **Labels:** `enhancement`, `phase-4`, `avalonia`
- **Milestone:** v3.0.0-alpha
- **Priority:** P0
- **Complexity:** XL

---

### Phase 5: Testing & Samples

**Issue #165:** Create Avalonia sample application
```markdown
## Description
Create cross-platform sample app for Avalonia

## Tasks
- [ ] Create AvaloniaHexEditor.Sample project
- [ ] Implement MainWindow.axaml
- [ ] Add file operations (open, save)
- [ ] Add edit features
- [ ] Add search/replace
- [ ] Add theme switching
- [ ] Add README

## Branch
feature/avalonia-samples

## Related
#153
```
- **Labels:** `enhancement`, `phase-5`, `samples`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** M

**Issue #166:** Multi-platform testing
```markdown
## Description
Test Avalonia version on all target platforms

## Tasks
- [ ] Test on Windows 11
- [ ] Test on Ubuntu 22.04/24.04
- [ ] Test on macOS (if available)
- [ ] Functional tests (all features)
- [ ] Performance comparison (WPF vs Avalonia)
- [ ] Large file testing (>100MB)
- [ ] Document results

## Branch
feature/avalonia-samples

## Acceptance Criteria
- Works on 2+ platforms
- All core features functional
- Performance acceptable
```
- **Labels:** `testing`, `phase-5`, `multi-platform`
- **Milestone:** v3.0.0-beta
- **Priority:** P0
- **Complexity:** L

---

### Phase 6: Themes & Polish

**Issue #167:** Port themes to Avalonia
```markdown
## Description
Adapt existing themes for Avalonia

## Tasks
- [ ] Port Dark.axaml
- [ ] Port Light.axaml
- [ ] Port Cyberpunk.axaml
- [ ] Test theme switching
- [ ] Document theme usage

## Branch
feature/avalonia-themes

## Related
#153
```
- **Labels:** `enhancement`, `phase-6`, `themes`
- **Milestone:** v3.0.0-alpha
- **Priority:** P2
- **Complexity:** M

**Issue #168:** Performance optimization
```markdown
## Description
Profile and optimize performance

## Tasks
- [ ] Profile rendering on WPF
- [ ] Profile rendering on Avalonia
- [ ] Identify bottlenecks
- [ ] Optimize hot paths
- [ ] Memory optimization
- [ ] Document results

## Branch
feature/avalonia-support

## Acceptance Criteria
- Avalonia within 20% of WPF performance
```
- **Labels:** `performance`, `phase-6`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** M

**Issue #169:** Complete documentation
```markdown
## Description
Finalize all documentation for v3.0

## Tasks
- [ ] Update README.md
- [ ] Create MIGRATION_GUIDE.md
- [ ] Update API documentation (XML comments)
- [ ] Create CHANGELOG.md for v3.0
- [ ] Update sample READMEs
- [ ] Review all guides

## Branch
feature/avalonia-support

## Related
#153
```
- **Labels:** `documentation`, `phase-6`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** S

---

### Phase 7: CI/CD

**Issue #170:** Setup GitHub Actions CI/CD
```markdown
## Description
Configure automated builds and tests

## Tasks
- [ ] Create build workflow (multi-platform)
- [ ] Create test workflow
- [ ] Create NuGet packaging workflow
- [ ] Configure status checks
- [ ] Test on Windows/Linux/macOS runners
- [ ] Document CI/CD setup

## Branch
feature/avalonia-ci

## Acceptance Criteria
- Builds pass on all platforms
- Tests run automatically
- NuGet packages created
```
- **Labels:** `ci/cd`, `phase-7`
- **Milestone:** v3.0.0-alpha
- **Priority:** P1
- **Complexity:** L

---

### Phase 8: Release

**Issue #171:** Prepare v3.0.0 release
```markdown
## Description
Final preparations for v3.0 release

## Tasks
- [ ] Final integration testing
- [ ] Create Git tag v3.0.0
- [ ] Publish NuGet packages (4 packages)
- [ ] Create GitHub Release
- [ ] Update release notes
- [ ] Close related issues (#118, #135, #153)
- [ ] Announce release

## Branch
master (after merge)

## Acceptance Criteria
- All packages published on NuGet.org
- GitHub Release created
- Documentation complete
- Community notified
```
- **Labels:** `release`, `phase-8`
- **Milestone:** v3.0.0
- **Priority:** P0
- **Complexity:** S

---

## 🏷️ Labels to Create

Create these labels in **Issues → Labels**:

### Type Labels
- `enhancement` - New feature or improvement
- `refactoring` - Code refactoring
- `bug` - Bug fix
- `documentation` - Documentation changes
- `testing` - Test-related
- `performance` - Performance improvement
- `ci/cd` - CI/CD related

### Phase Labels
- `phase-0` - Preparation (blue)
- `phase-1` - Core Creation (green)
- `phase-2` - WPF Refactoring (yellow)
- `phase-3` - Facade Creation (orange)
- `phase-4` - Avalonia Implementation (purple)
- `phase-5` - Testing & Samples (pink)
- `phase-6` - Themes & Polish (cyan)
- `phase-7` - CI/CD (gray)
- `phase-8` - Release (red)

### Component Labels
- `core` - WpfHexaEditor.Core
- `wpf` - WpfHexaEditor.Wpf
- `avalonia` - WpfHexaEditor.Avalonia
- `samples` - Sample applications
- `themes` - Theme-related

### Priority Labels
- `P0` - Critical (red)
- `P1` - High (orange)
- `P2` - Medium (yellow)
- `P3` - Low (green)

### Other Labels
- `planning` - Planning work
- `compatibility` - Backward compatibility
- `multi-platform` - Cross-platform support
- `breaking-change` - Breaking change
- `help-wanted` - Community help wanted
- `good-first-issue` - Good for new contributors

---

## 📈 Project Views

### View 1: By Phase (Default)
- Group by: **Phase**
- Sort by: **Priority**
- Filter: Status != Done

### View 2: Current Sprint
- Filter: Status = Ready OR In Progress
- Sort by: Priority
- Group by: Assignee

### View 3: Roadmap (Timeline)
- Layout: Timeline
- Date field: Milestone due date
- Group by: Phase

### View 4: All Tasks
- Layout: Table
- Show all fields
- No filters

---

## 🔗 Automation

### Recommended GitHub Actions

1. **Auto-add to project**
   - When issue created with label `phase-*`
   - Add to "Avalonia Support Implementation" project
   - Set status to "Backlog"

2. **Auto-move cards**
   - When PR opened → Move to "In Review"
   - When PR merged → Move to "Done"
   - When issue closed → Move to "Done"

3. **Auto-link PRs**
   - Link PR to issue automatically
   - Use keywords: "Closes #", "Fixes #", "Resolves #"

---

## 📊 Progress Tracking

### Key Metrics to Track

1. **Issues Completed per Phase**
2. **Open vs Closed Issues**
3. **PRs Merged vs Open**
4. **Code Coverage %**
5. **Build Success Rate**
6. **Release Progress**

### Weekly Review Checklist

- [ ] Review completed tasks
- [ ] Move cards to appropriate columns
- [ ] Update priorities
- [ ] Plan next week's work
- [ ] Update milestones if needed
- [ ] Update project description

---

## 🎯 Quick Start Commands

### Create all issues at once (CLI)

```bash
# Phase 1
gh issue create --title "Create WpfHexaEditor.Core project" --label "enhancement,phase-1,core,P0" --milestone "v3.0.0-alpha" --body-file .github/issue-templates/155.md

# Phase 2
gh issue create --title "Rename WPFHexaEditor to WpfHexaEditor.Wpf" --label "refactoring,phase-2,wpf,P0" --milestone "v3.0.0-alpha" --body-file .github/issue-templates/158.md

# ... repeat for all issues
```

### Add issues to project (CLI)

```bash
# Get project number (usually 1)
gh project list --owner abbaye

# Add issues to project
gh project item-add 1 --owner abbaye --url https://github.com/abbaye/WpfHexEditorControl/issues/155
gh project item-add 1 --owner abbaye --url https://github.com/abbaye/WpfHexEditorControl/issues/156
# ... repeat for all issues
```

---

## 📝 Project README

Add this to the project description:

```markdown
# Avalonia Support Implementation

Official project board for implementing Avalonia UI support in WPFHexaEditor Control.

## 🎯 Goals
- ✅ Cross-platform support (Windows, Linux, macOS)
- ✅ Backward compatibility (Strategy C)
- ✅ 85% code sharing between WPF and Avalonia
- ✅ Professional documentation and migration path

## 📅 Timeline
- **Start:** Week 0 (Preparation complete)
- **Duration:** 11 weeks (Phases 1-8)
- **Target:** v3.0.0 release

## 📚 Documentation
Complete documentation available at:
https://github.com/abbaye/WpfHexEditorControl/tree/master/docs/Avalonia

## 🚀 Get Involved
- Review [GIT_WORKFLOW.md](docs/Avalonia/GIT_WORKFLOW.md)
- Check "Ready" column for available tasks
- Comment on issues to claim work
- Submit PRs to `feature/avalonia-support` branch

## 📊 Progress
Track progress in the project views:
- **By Phase:** See work organized by implementation phase
- **Current Sprint:** See active work items
- **Roadmap:** Timeline view of milestones

## 🤝 Contributing
See [IMPLEMENTATION_ROADMAP.md](docs/Avalonia/IMPLEMENTATION_ROADMAP.md) for complete implementation plan.

Related Issues: #118, #135, #153
```

---

## ✅ Setup Checklist

- [ ] Create GitHub Project
- [ ] Configure board columns
- [ ] Add custom fields (Phase, Priority, Complexity, Branch)
- [ ] Create milestones (v3.0.0-alpha, beta, v3.0.0, v4.0.0)
- [ ] Create labels (type, phase, component, priority)
- [ ] Create all issues (Phase 1-8)
- [ ] Add issues to project
- [ ] Set up project views
- [ ] Configure automation (optional)
- [ ] Add project README
- [ ] Link from main repository README

---

**Last Updated:** 2026-02-16
**Status:** 📋 Ready for project creation
**Related:** [IMPLEMENTATION_ROADMAP.md](./IMPLEMENTATION_ROADMAP.md)
