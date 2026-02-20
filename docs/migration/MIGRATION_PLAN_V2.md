# Migration Plan: V2 → Main Control, V1 → Legacy

**Date**: 2026-02-14
**Status**: 📋 DRAFT - Ready for Implementation
**Goal**: Make V2 the default control, move V1 to legacy support

---

## 🎯 Executive Summary

**Current State:**
- V1 (HexEditor) = Main control in WPF namespace
- V2 (HexEditorV2) = New control with "V2" suffix
- Users must explicitly choose V2 to get benefits

**Target State:**
- V2 becomes **HexEditor** (main control)
- V1 becomes **HexEditorLegacy** (legacy support)
- 100% backward compatibility maintained
- Clear migration path for existing users

**Benefits:**
- ✅ New users get V2 by default (99% faster, bug-free)
- ✅ Existing users have time to migrate
- ✅ V1 bugs stay fixed (legacy = read-only maintenance)
- ✅ Clear deprecation timeline

---

## 📊 Impact Analysis

### Files Affected

| Component | Current Name | New Name | Action |
|-----------|-------------|----------|--------|
| **Main Control** | HexEditor.xaml.cs | HexEditorLegacy.xaml.cs | RENAME |
| **V2 Control** | HexEditorV2.xaml.cs | HexEditor.xaml.cs | RENAME |
| **ByteProvider** | ByteProviderLegacy.cs | (keep) | NO CHANGE |
| **V2 ByteProvider** | ByteProvider.cs | (keep) | NO CHANGE |
| **Namespace** | WpfHexaEditor | WpfHexaEditor | NO CHANGE |

### Backward Compatibility Strategy

**Option A: Alias (Recommended)** ⭐
```csharp
// Old V1 control becomes alias for compatibility
namespace WpfHexaEditor
{
    /// <summary>
    /// LEGACY: Use HexEditor (V2) instead. This is V1 renamed for backward compatibility.
    /// Will be removed in v3.0.
    /// </summary>
    [Obsolete("Use HexEditor instead. HexEditorV1 is deprecated and will be removed in v3.0.", false)]
    public class HexEditorV1 : HexEditorLegacy
    {
        // Empty class - just an alias
    }
}
```

**Option B: Breaking Change** ⚠️
- Rename directly without alias
- Users must update code
- Faster, cleaner, but disrupts existing projects

**Recommendation**: Use **Option A** for v2.6, transition to **Option B** in v3.0

---

## 🗺️ Migration Phases

### **Phase 1: Preparation (v2.5 - Current)**
**Status**: ✅ COMPLETE

- [x] V2 architecture stabilized
- [x] Phase 6 optimizations completed (100-6000x faster)
- [x] All critical bugs fixed (#145, save data loss)
- [x] 80+ unit tests passing
- [x] Documentation complete

### **Phase 2: Soft Deprecation (v2.6 - Next Release)**
**Timeline**: 1-2 months
**Status**: 📋 PLANNED

**Changes:**

1. **Rename Files**
   ```bash
   # V1 → Legacy
   git mv HexEditor.xaml HexEditorLegacy.xaml
   git mv HexEditor.xaml.cs HexEditorLegacy.xaml.cs

   # V2 → Main
   git mv HexEditorV2.xaml HexEditor.xaml
   git mv HexEditorV2.xaml.cs HexEditor.xaml.cs
   ```

2. **Add Compatibility Aliases**
   ```csharp
   // For backward compatibility
   [Obsolete("Use HexEditor instead. HexEditorV1 is V1 legacy control.", false)]
   public class HexEditorV1 : HexEditorLegacy { }

   // V2 can still be referenced (but points to new HexEditor)
   [Obsolete("HexEditorV2 is now the main HexEditor control. Use HexEditor instead.", false)]
   public class HexEditorV2 : HexEditor { }
   ```

3. **Update Documentation**
   - README.md: V2 → Main control
   - Mark V1 features as "Legacy"
   - Add migration guide
   - Update all samples to use new HexEditor

4. **Add Compiler Warnings**
   ```csharp
   #warning "You are using HexEditorV1 (legacy). Please migrate to HexEditor (V2) for 99% faster performance and critical bug fixes. See MIGRATION_GUIDE.md"
   ```

### **Phase 3: Hard Deprecation (v2.8)**
**Timeline**: 6 months after v2.6
**Status**: 🔮 FUTURE

**Changes:**

1. **Mark for Removal**
   ```csharp
   [Obsolete("HexEditorV1 will be REMOVED in v3.0. Migrate to HexEditor now!", true)]
   public class HexEditorV1 : HexEditorLegacy { }
   ```

2. **Update NuGet Package Description**
   ```xml
   <PackageReleaseNotes>
   BREAKING CHANGE WARNING: HexEditorV1 (legacy) will be removed in v3.0 (6 months).
   Please migrate to HexEditor (V2) which is 99% faster with critical bug fixes.
   See MIGRATION_GUIDE.md for step-by-step instructions.
   </PackageReleaseNotes>
   ```

3. **Aggressive Warnings**
   - Build warnings on every compile
   - Runtime warnings in debug mode
   - Clear deprecation notices

### **Phase 4: Removal (v3.0)**
**Timeline**: 12 months after v2.6
**Status**: 🔮 FUTURE

**Changes:**

1. **Remove V1 Code**
   ```bash
   git rm HexEditorLegacy.xaml
   git rm HexEditorLegacy.xaml.cs
   git rm -r Legacy/
   ```

2. **Clean Up**
   - Remove all V1 compatibility aliases
   - Remove ByteProviderLegacy
   - Archive V1 in separate branch `v1-archive`

3. **Major Version Bump**
   - NuGet: 3.0.0
   - Clear breaking changes in changelog
   - Migration guide prominent in README

---

## 📝 Migration Guide for Users

### Quick Migration (30 seconds)

**Before (V1):**
```xaml
<control:HexEditor FileName="test.bin" />
```

**After (V2 - no change required!):**
```xaml
<control:HexEditor FileName="test.bin" />
<!-- V2 is now the default HexEditor! -->
```

**If you explicitly used HexEditorV2:**
```xaml
<!-- Old -->
<control:HexEditorV2 FileName="test.bin" />

<!-- New (both work, but HexEditor is preferred) -->
<control:HexEditor FileName="test.bin" />
```

### Code Migration

**Step 1: Replace Type References**
```csharp
// Old
using WpfHexaEditor;
var editor = new HexEditorV1();  // Legacy

// New
using WpfHexaEditor;
var editor = new HexEditor();  // V2 is now default!
```

**Step 2: Update Property Bindings** (mostly unchanged)
```csharp
// V1 and V2 have same public API - no changes needed!
editor.FileName = "test.bin";
editor.ReadOnlyMode = false;
// etc.
```

**Step 3: Handle V2-Specific Features** (optional)
```csharp
// NEW in V2: Service architecture
var findService = editor.FindReplaceService;
var results = findService.FindAllCached(pattern, 0);  // 10-100x faster!

// NEW in V2: SIMD comparisons
var comparison = editor.ComparisonService;
long diffs = comparison.CountDifferencesSIMD(other);  // 16-32x faster!
```

### Breaking Changes (Minimal)

**None for 99% of users!** V2 maintains V1 public API.

**Only if you used:**
1. **Internal/Private APIs** → Refactored in V2, use public services instead
2. **Custom ByteProvider subclasses** → Inherit from ByteProvider (V2) instead
3. **Reflection on internal fields** → Use public properties/services

---

## 🔧 Implementation Checklist

### Code Changes

- [ ] Rename HexEditor → HexEditorLegacy
- [ ] Rename HexEditorV2 → HexEditor
- [ ] Add compatibility aliases (HexEditorV1, HexEditorV2)
- [ ] Add [Obsolete] attributes with migration messages
- [ ] Update all internal references
- [ ] Update all sample projects
- [ ] Update unit tests

### Documentation Changes

- [ ] Update README.md (V2 → Main)
- [ ] Create MIGRATION_GUIDE.md
- [ ] Update wiki with migration steps
- [ ] Update NuGet package description
- [ ] Add deprecation timeline to CHANGELOG.md
- [ ] Update screenshots (remove V2 suffix)

### Testing

- [ ] All unit tests pass with new names
- [ ] Sample projects build with new HexEditor
- [ ] Backward compatibility tests (HexEditorV1 alias works)
- [ ] NuGet package builds correctly
- [ ] Documentation builds correctly

### Release

- [ ] Version bump to v2.6.0
- [ ] Clear release notes about migration
- [ ] Blog post / announcement explaining benefits
- [ ] Update GitHub releases with migration guide

---

## 📅 Timeline

| Phase | Version | Date | Status |
|-------|---------|------|--------|
| Phase 1: Preparation | v2.5 | 2026-02 | ✅ COMPLETE |
| Phase 2: Soft Deprecation | v2.6 | 2026-04 | 📋 PLANNED |
| Phase 3: Hard Deprecation | v2.8 | 2026-10 | 🔮 FUTURE |
| Phase 4: V1 Removal | v3.0 | 2027-04 | 🔮 FUTURE |

**Total migration window**: 12-14 months

---

## 🎯 Success Metrics

**After v2.6 Release:**
- [ ] 90%+ of new projects use HexEditor (V2)
- [ ] < 10 GitHub issues about migration confusion
- [ ] NuGet downloads shift from V1 to V2
- [ ] Documentation clarity score > 4.5/5

**After v3.0 Release:**
- [ ] 100% of code uses V2 architecture
- [ ] V1 code archived and documented
- [ ] Codebase reduced by 30% (V1 removal)
- [ ] Maintenance burden reduced

---

## 🚨 Risks & Mitigation

### Risk 1: Users Don't Migrate
**Mitigation:**
- Clear migration guide
- Gradual deprecation (12 months)
- Automated migration tools
- Strong incentive (99% faster, bugs fixed)

### Risk 2: Breaking Changes Discovered
**Mitigation:**
- Extensive testing before v2.6
- Beta period for early adopters
- Quick patch releases if issues found
- Keep V1 available via NuGet 2.x versions

### Risk 3: Enterprise Users Stuck on V1
**Mitigation:**
- Long deprecation timeline (12 months)
- LTS support for v2.5 (security fixes only)
- Clear communication to stakeholders
- Migration consulting offered

---

## 💡 Alternatives Considered

### Alternative 1: Keep Both Forever
**Pros:** No breaking changes
**Cons:**
- Confusing for new users (which one to use?)
- Double maintenance burden
- V1 bugs never truly fixed
**Verdict:** ❌ Not sustainable

### Alternative 2: Immediate V1 Removal
**Pros:** Clean, simple
**Cons:**
- Breaks all existing projects
- Angry users
- Bad reputation
**Verdict:** ❌ Too aggressive

### Alternative 3: Gradual Migration (CHOSEN)
**Pros:**
- ✅ Time for users to migrate
- ✅ Backward compatibility maintained
- ✅ Clear path forward
- ✅ Professional approach
**Verdict:** ✅ **RECOMMENDED**

---

## 📚 References

- [ARCHITECTURE_V2.md](ARCHITECTURE_V2.md) - V2 architecture details
- [OPTIMIZATIONS_PHASE6.md](Sources/WPFHexaEditor/OPTIMIZATIONS_PHASE6.md) - Performance improvements
- [CHANGELOG.md](CHANGELOG.md) - Version history
- [Semantic Versioning](https://semver.org/) - Versioning strategy

---

## ✅ Approval & Sign-Off

**Prepared By**: Claude Sonnet 4.5
**Review Status**: 📋 Draft - Pending Approval
**Target Implementation**: v2.6.0 (April 2026)

**Approval Required From:**
- [ ] Project Lead / Maintainer
- [ ] Community Feedback (GitHub Discussion)
- [ ] Beta Testers

**Next Steps:**
1. Create GitHub Discussion for community feedback
2. Implement Phase 2 in feature branch
3. Beta testing with early adopters
4. Release v2.6.0 with migration plan
