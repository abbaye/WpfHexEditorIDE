# Quick Start - Avalonia Implementation

## 🎯 Getting Started Checklist

This guide provides the exact steps to start implementing Avalonia support.

---

## ✅ Step 1: Create GitHub Project (5 minutes)

### Via GitHub Web UI (Recommended)

1. Go to: https://github.com/abbaye/WpfHexEditorControl
2. Click **Projects** tab → **New project**
3. Select template: **Team backlog**
4. Enter details:
   - **Title:** Avalonia Support Implementation
   - **Description:** (copy from GITHUB_PROJECT_SETUP.md)
5. Click **Create project**

### Add Custom Fields

In the project settings, add these fields:
- **Phase** (Single select): Phase 0-8
- **Priority** (Single select): P0, P1, P2, P3
- **Complexity** (Single select): XS, S, M, L, XL
- **Branch** (Text)

### Create Milestones

1. Go to: https://github.com/abbaye/WpfHexEditorControl/milestones
2. Click **New milestone**
3. Create 4 milestones:
   - **v3.0.0-alpha** - Development complete (~11 weeks)
   - **v3.0.0-beta** - Testing complete (+2 weeks)
   - **v3.0.0** - Public release (+1 week)
   - **v4.0.0** - Transition complete (+6-12 months)

**✅ Project created!** See [GITHUB_PROJECT_SETUP.md](./GITHUB_PROJECT_SETUP.md) for complete setup.

---

## ✅ Step 2: Create Feature Branch (1 minute)

### Create Main Development Branch

```bash
cd /c/Users/khens/source/repos/WpfHexEditorControl

# Ensure master is up to date
git checkout master
git pull origin master

# Create main development branch
git checkout -b feature/avalonia-support

# Push to remote
git push -u origin feature/avalonia-support

# Verify
git branch
```

**✅ Branch created:** `feature/avalonia-support`

---

## ✅ Step 3: Protect Branches on GitHub (2 minutes)

### Protect `master` Branch

1. Go to: https://github.com/abbaye/WpfHexEditorControl/settings/branches
2. Click **Add rule**
3. Branch name pattern: `master`
4. Enable:
   - ✅ Require pull request reviews before merging (1 approval)
   - ✅ Require status checks to pass
   - ✅ Require conversation resolution
   - ❌ Do not allow force pushes
   - ❌ Do not allow deletions
5. Click **Create**

### Protect `feature/avalonia-support` Branch

1. Add another rule
2. Branch name pattern: `feature/avalonia-support`
3. Enable:
   - ✅ Require pull request reviews before merging
   - ✅ Require status checks to pass
4. Click **Create**

**✅ Branches protected!**

---

## ✅ Step 4: Create Initial Issues (10 minutes)

### Create Phase 1 Issues

Use the templates from GITHUB_PROJECT_SETUP.md:

```bash
# Issue #155: Create WpfHexaEditor.Core project
gh issue create \
  --title "Create WpfHexaEditor.Core project" \
  --label "enhancement,phase-1,core,P0" \
  --milestone "v3.0.0-alpha" \
  --body "## Description
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
#153"

# Issue #156: Move portable code to Core
gh issue create \
  --title "Move portable code to Core project" \
  --label "refactoring,phase-1,core,P0" \
  --milestone "v3.0.0-alpha" \
  --body "## Description
Move all platform-agnostic code from WPFHexaEditor to Core

## Tasks
- [ ] Move Core/Bytes (~9,522 lines)
- [ ] Move Core/CharacterTable (~1,891 lines)
- [ ] Move Services (~4,305 lines)
- [ ] Move ViewModels (~2,500 lines)
- [ ] Update namespaces
- [ ] Resolve dependencies

## Branch
feature/avalonia-core

## Acceptance Criteria
- All portable code in Core project
- No breaking changes to public API
- All code compiles

## Related
#153"

# Issue #157: Add unit tests for Core
gh issue create \
  --title "Add unit tests for Core project" \
  --label "testing,phase-1,core,P1" \
  --milestone "v3.0.0-alpha" \
  --body "## Description
Create comprehensive unit tests for Core business logic

## Tasks
- [ ] Test Core/Bytes classes
- [ ] Test Services
- [ ] Test ViewModels
- [ ] Achieve >70% code coverage

## Branch
feature/avalonia-core

## Acceptance Criteria
- All tests pass
- Code coverage >70%

## Related
#153"
```

**✅ Issues created!** (Or create manually via GitHub UI)

---

## ✅ Step 5: Start Phase 1 - Create Core Project (NOW!)

### Create Feature Branch for Phase 1

```bash
# From feature/avalonia-support branch
git checkout feature/avalonia-support
git checkout -b feature/avalonia-core
```

### Create WpfHexaEditor.Core Project

```bash
# Navigate to Sources directory
cd Sources

# Create new project
dotnet new classlib -n WpfHexaEditor.Core -f netstandard2.0

# Navigate into new project
cd WpfHexaEditor.Core

# Remove default Class1.cs
rm Class1.cs
```

### Create Project Structure

```bash
# Create platform abstraction folders
mkdir -p Platform/Rendering
mkdir -p Platform/Media
mkdir -p Platform/Input
mkdir -p Platform/Threading
mkdir -p Platform/Controls

# Create folders for portable code (will be moved from WPFHexaEditor)
mkdir -p Core/Bytes
mkdir -p Core/CharacterTable
mkdir -p Core/Interfaces
mkdir -p Core/MethodExtension
mkdir -p Services
mkdir -p ViewModels
mkdir -p Models
mkdir -p Events
```

### Configure Project File

Edit `WpfHexaEditor.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>

    <!-- Package Info -->
    <PackageId>WpfHexaEditor.Core</PackageId>
    <Version>3.0.0-alpha.1</Version>
    <Authors>Derek Tremblay, Contributors</Authors>
    <Company>WpfHexaEditor</Company>
    <Description>
      Platform-agnostic core library for WPF and Avalonia Hex Editor controls.
      Contains business logic, services, and platform abstractions.
    </Description>
    <PackageTags>hex;editor;core;portable;netstandard</PackageTags>
    <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/abbaye/WpfHexEditorControl</PackageProjectUrl>
    <RepositoryUrl>https://github.com/abbaye/WpfHexEditorControl</RepositoryUrl>
    <RepositoryType>git</RepositoryType>

    <!-- Documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile>bin\$(Configuration)\$(TargetFramework)\WpfHexaEditor.Core.xml</DocumentationFile>

    <!-- Symbols -->
    <DebugType>embedded</DebugType>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <!-- No dependencies - this is the base library -->
  </ItemGroup>
</Project>
```

### Build to Verify

```bash
# Build the project
dotnet build

# Should output:
# Build succeeded.
# 0 Warning(s)
# 0 Error(s)
```

### Add to Solution

```bash
# Navigate back to repo root
cd ../..

# Add project to solution
dotnet sln WpfHexEditorControl.sln add Sources/WpfHexaEditor.Core/WpfHexaEditor.Core.csproj

# Verify
dotnet build
```

### Commit Initial Structure

```bash
git add Sources/WpfHexaEditor.Core/
git add WpfHexEditorControl.sln

git commit -m "feat(core): Create WpfHexaEditor.Core project structure

Initialize platform-agnostic core library:
- Target: netstandard2.0 (portable to all platforms)
- Created folder structure for platform abstractions
- Created folders for portable business logic
- Configured package metadata for NuGet
- Enabled XML documentation generation
- Added to solution

Related to #153, #155

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"

git push -u origin feature/avalonia-core
```

**✅ Core project created!**

---

## 📋 What's Next?

### Current Status

```
✅ GitHub Project created
✅ Branches created and protected
✅ Issues created (#155-157)
✅ Phase 1 started
✅ WpfHexaEditor.Core project structure created
```

### Next Steps (in order)

1. **Create Platform Abstractions** (Issue #155)
   - [ ] IDrawingContext interface
   - [ ] PlatformColor struct
   - [ ] IBrush, IPen interfaces
   - [ ] PlatformKey enum
   - [ ] IPlatformTimer interface

2. **Move Portable Code** (Issue #156)
   - [ ] Copy Core/Bytes from WPFHexaEditor
   - [ ] Copy Services from WPFHexaEditor
   - [ ] Copy ViewModels from WPFHexaEditor
   - [ ] Update namespaces
   - [ ] Update references

3. **Add Unit Tests** (Issue #157)
   - [ ] Create WpfHexaEditor.Core.Tests project
   - [ ] Add tests for Core/Bytes
   - [ ] Add tests for Services
   - [ ] Configure CI

4. **Create Pull Request**
   - [ ] Push all changes
   - [ ] Create PR: feature/avalonia-core → feature/avalonia-support
   - [ ] Request code review
   - [ ] Merge when approved

---

## 🎯 Detailed Next Commands

### Create Platform Abstraction Files

Ready to create the abstraction interfaces? Say "yes" and I'll create:
- Platform/Rendering/IDrawingContext.cs
- Platform/Rendering/IFormattedText.cs
- Platform/Rendering/PlatformGeometry.cs
- Platform/Media/PlatformColor.cs
- Platform/Media/IBrush.cs
- Platform/Input/PlatformKey.cs
- Platform/Threading/IPlatformTimer.cs

---

**Status:** 🟢 Ready to continue Phase 1!
**Current Branch:** `feature/avalonia-core`
**Next:** Create platform abstraction interfaces
