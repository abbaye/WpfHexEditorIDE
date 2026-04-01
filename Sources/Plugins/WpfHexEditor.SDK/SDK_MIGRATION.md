# WpfHexEditor.SDK Migration Guide

This document tracks breaking changes between SDK major versions and provides
step-by-step migration instructions for plugin authors.

---

## Versioning Policy

The SDK follows [Semantic Versioning 2.0.0](https://semver.org/):

| Change Type | Version Bump | Plugin Impact |
|-------------|-------------|---------------|
| New interface / new member with default impl | MINOR | None — existing plugins compile unchanged |
| New model / event / descriptor | MINOR | None |
| Removed or renamed member | **MAJOR** | **Must update** |
| Changed method signature | **MAJOR** | **Must update** |
| Bug fix / doc correction | PATCH | None |

### Compatibility Guarantee

- All interfaces listed as **Stable** in `CHANGELOG.md` will not receive breaking changes
  within the same major version (2.x).
- Interfaces marked `[Obsolete]` are **preview** — their API surface may change in the next
  major version. Plugin authors should avoid depending on them.
- New members added to existing interfaces will always have **default implementations**
  so that existing plugin code continues to compile.

### Plugin Manifest Compatibility

Plugins declare `sdkVersion`, `minSDKVersion`, and `maxSDKVersion` in `manifest.json`.
The PluginHost validates these at load time:

```json
{
  "sdkVersion": "2.0.0",
  "minSDKVersion": "2.0.0",
  "maxSDKVersion": "2.*"
}
```

- `"2.*"` — compatible with any SDK 2.x release.
- `">=2.0.0 <3.0.0"` — equivalent, explicit range.

---

## Migration: 1.x to 2.0.0

### What Changed

| Change | Impact | Action Required |
|--------|--------|-----------------|
| Version scheme formalized to SemVer | Low | Update `manifest.json` `sdkVersion` to `"2.0.0"` |
| `IMarketplaceService` marked `[Obsolete]` | Low | Avoid new dependencies on this interface |
| `MarketplaceListing` marked `[Obsolete]` | Low | Avoid new dependencies on this model |
| `GenerateDocumentationFile` enabled | None | No plugin impact |

### Steps

1. Update your plugin's `manifest.json`:
   ```json
   "sdkVersion": "2.0.0",
   "minSDKVersion": "2.0.0"
   ```

2. If you reference `IMarketplaceService` or `MarketplaceListing`, add
   `#pragma warning disable CS0618` around those usages, or migrate away
   from the marketplace API (it will be redesigned in SDK 3.0).

3. Rebuild and verify — no other changes required.

---

## Template: Future Major Version Migration

<!--
Copy this template when preparing a new MAJOR version migration section.

## Migration: X.x to Y.0.0

### What Changed

| Change | Impact | Action Required |
|--------|--------|-----------------|
| ... | ... | ... |

### Steps

1. ...
2. ...
3. Rebuild and verify.

### Breaking Change Details

#### [Change Title]
**Before (SDK X.x):**
```csharp
// old code
```
**After (SDK Y.0):**
```csharp
// new code
```
**Why:** [rationale]
-->
