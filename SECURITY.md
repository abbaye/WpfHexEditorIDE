# Security Policy

## Supported Versions

The following versions of WPF HexEditor are currently supported with security updates:

| Version | .NET Target | Support Status | Security Updates |
| ------- | ----------- | -------------- | ---------------- |
| 2.1.7+ | .NET Framework 4.8 | ✅ Supported | Active |
| 2.1.7+ | .NET 8.0-windows | ✅ Supported | Active |
| 2.1.x | .NET 7.0-windows | ❌ **Deprecated** | **No longer supported** |
| < 2.1.0 | Various | ❌ Not Supported | No updates |

### ⚠️ Important Security Notice

**As of February 2026, .NET 7.0-windows support has been removed** from all project files due to Microsoft ending security updates for this framework. Users on .NET 7.0 should migrate to:
- **.NET 8.0-windows** (recommended for modern applications)
- **.NET Framework 4.8** (for legacy applications)

## Supported Frameworks

WPF HexEditor officially supports and receives security updates on:

- ✅ **.NET Framework 4.8** - Long-term support until 2028+
- ✅ **.NET 8.0-windows** - Long-term support until November 2026

## Recent Security-Related Updates

### 2026-02-10 - Phase 3 Architecture Refactoring

**Security Improvements:**
- Enhanced code maintainability through service-based architecture
- Reduced attack surface by separating business logic from UI
- Improved input validation in all service methods
- Better isolation of data modification operations

**Services Added:**
- `HighlightService` - Manages search result highlighting with controlled state
- `ByteModificationService` - Centralized byte operations with validation

### 2026 - Critical Cache Bug Fix

**Security Impact:** Medium
**Issue:** Search cache was never invalidated after data modifications, potentially leading to data integrity issues and incorrect search results.

**Fix:** Cache clearing implemented at 11 critical modification points:
- ModifyByte, Paste, FillWithByte, ReplaceByte
- ReplaceFirst, ReplaceNext, ReplaceAll
- Undo handler, InsertByte, InsertBytes, DeleteSelection

**Result:** Users now receive accurate search results, preventing potential data corruption scenarios.

## Security Best Practices

### For Library Users

When integrating WPF HexEditor into your application:

1. **Input Validation:**
   - Always validate file paths before passing to `HexEditor.FileName`
   - Sanitize user input when searching/replacing bytes
   - Verify file sizes before loading large files

2. **Access Control:**
   - Use `ReadOnlyMode` property when editing should be restricted
   - Set `AllowDeleteByte` and `AllowInsertAnywhere` according to your security requirements
   - Control file access permissions at the OS level

3. **Data Integrity:**
   - Always backup important files before editing
   - Use the Undo/Redo functionality for reversible operations
   - Verify modifications with checksums when data integrity is critical

4. **Memory Safety:**
   - Be aware of memory usage when loading large files (streams are recommended for files > 100MB)
   - Dispose of the HexEditor control properly to free resources
   - Monitor memory usage in production applications

### Common Security Pitfalls

❌ **Don't:**
- Load untrusted files without size validation
- Allow unrestricted file access in public-facing applications
- Ignore file permissions and access control
- Edit system-critical files without proper safeguards

✅ **Do:**
- Validate file paths and sizes before loading
- Implement proper access control in your application
- Use read-only mode when editing is not required
- Backup important data before modifications

## Known Security Considerations

### 1. File System Access
WPF HexEditor directly accesses the file system when using the `FileName` property. Applications should:
- Validate file paths to prevent directory traversal attacks
- Implement proper access control
- Consider using streams instead of direct file access for sensitive scenarios

### 2. Memory Usage
Large file operations can consume significant memory. Applications should:
- Limit maximum file sizes based on available memory
- Use streaming operations for files > 100MB
- Monitor memory usage in production

### 3. Undo/Redo Stack
The undo/redo functionality stores byte changes in memory:
- Be aware that sensitive data may remain in memory after edits
- Clear undo/redo history when handling sensitive data
- Consider memory scrubbing for high-security applications

### 4. Clipboard Operations
Data copied to clipboard may be accessible by other applications:
- Be cautious when copying sensitive binary data
- Consider disabling clipboard operations in high-security contexts
- Use in-memory streams instead of clipboard for sensitive transfers

## Reporting a Vulnerability

If you discover a security vulnerability in WPF HexEditor, please report it responsibly:

### How to Report

**🔒 For security vulnerabilities:**
1. Open a GitHub issue at: https://github.com/abbaye/WpfHexEditorControl/issues
2. Use the label: **security**
3. Include in your report:
   - Description of the vulnerability
   - Steps to reproduce
   - Affected versions
   - Potential impact
   - Suggested fix (if available)

**📋 GitHub Issue Title Format:**
```
[SECURITY] Brief Description of the Vulnerability
```

Alternatively, you can contact the maintainer directly at: **derektremblay666@gmail.com**

### Response Timeline

- **Initial Response:** Within 48 hours
- **Status Update:** Within 1 week
- **Fix Timeline:** Depends on severity
  - **Critical:** 1-2 weeks
  - **High:** 2-4 weeks
  - **Medium:** 4-8 weeks
  - **Low:** Next regular release

### Vulnerability Handling

**If accepted:**
- We will work on a fix and keep you informed of progress
- You will be credited in the release notes (if desired)
- A security advisory will be published after the fix is released
- We will coordinate disclosure timing with you

**If declined:**
- We will provide a detailed explanation
- We may suggest alternative security practices
- The issue may be reclassified as a feature request or bug

## Security Contact

- **Report via GitHub Issues:** https://github.com/abbaye/WpfHexEditorControl/issues (Label: security)
- **Alternative Contact:** Derek Tremblay (derektremblay666@gmail.com)
- **Project Repository:** https://github.com/abbaye/WpfHexEditorControl
- **Security Advisories:** Check GitHub Security tab

## Additional Resources

- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/security/)
- [.NET Security Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/)

---

**Last Updated:** 2026-02-10
**Policy Version:** 1.1
