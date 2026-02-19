# HexEditor - Features Roadmap

## ✅ Phase 1: Core Architecture (DONE)
- [x] MVVM architecture (ViewModel + View)
- [x] Virtual/Physical positions
- [x] Insert/Overwrite mode
- [x] ByteProvider integration
- [x] Services integration (Undo/Redo, Clipboard, Selection, FindReplace)
- [x] Basic UI (Toolbar, Status bar, Hex/ASCII display)
- [x] Scroll virtualization
- [x] Sample application

## 🔄 Phase 2: User Interaction (IN PROGRESS)
- [ ] **Mouse Selection**
  - Click to select single byte
  - Drag to select range
  - Shift+Click for range selection
- [ ] **Keyboard Navigation**
  - Arrow keys (Up/Down/Left/Right)
  - Page Up/Down
  - Home/End
  - Ctrl+Home/End (file start/end)
- [ ] **Keyboard Editing**
  - Hex input (0-9, A-F)
  - Insert key toggle
  - Delete/Backspace
  - Ctrl+Z/Y (Undo/Redo)
  - Ctrl+C/V/X (Copy/Paste/Cut)

## 📋 Phase 3: Advanced Editing
- [ ] **Copy/Paste**
  - Copy as hex
  - Copy as ASCII
  - Paste from clipboard
  - Cut operation
- [ ] **Search/Replace UI**
  - Find dialog
  - Replace dialog
  - Find All highlighting
  - Navigate results
- [ ] **Multi-Selection** (optional)
  - Ctrl+Click for multiple selections
  - Rectangular selection

## 🎨 Phase 4: Visual Features
- [ ] **Highlights**
  - Search results highlighting
  - Custom highlights
  - Color customization
- [ ] **Bookmarks**
  - Add/Remove bookmarks
  - Navigate bookmarks
  - Bookmark panel
  - Visual markers
- [ ] **Custom Backgrounds**
  - Define background color ranges
  - Show/Hide backgrounds
  - Background panel
- [ ] **Themes**
  - Light/Dark mode
  - Custom color schemes

## 📊 Phase 5: Data Visualization
- [ ] **Multiple Encodings**
  - ASCII
  - UTF-8
  - UTF-16
  - EBCDIC
  - Custom encodings
- [ ] **TBL Support**
  - Load TBL files
  - Custom character mappings
  - Switch encodings on-the-fly
- [ ] **Data Inspector**
  - Show byte as int8/16/32/64
  - Show as float/double
  - Show as date/time
  - Endianness selection

## ⚙️ Phase 6: Advanced Features
- [ ] **Diff Mode**
  - Compare two files side-by-side
  - Highlight differences
  - Navigation between diffs
- [ ] **Structure Overlay**
  - Define data structures
  - Overlay on hex view
  - Collapsible sections
- [ ] **Scripting** (optional)
  - Python/Lua scripting
  - Automation support
  - Custom operations

## 🔧 Phase 7: Settings & Configuration
- [ ] **Settings Dialog**
  - Bytes per line
  - Font size/family
  - Colors
  - Auto-backup
- [ ] **Settings Persistence**
  - Save/Load settings
  - Import/Export
- [ ] **Keyboard Shortcuts**
  - Customizable shortcuts
  - Shortcuts editor

## 📦 Phase 8: File Operations
- [ ] **File Operations**
  - Save As
  - Recent files
  - Auto-save
  - Backup on save
- [ ] **Large File Support**
  - Memory mapping
  - Progressive loading
  - Optimize for files > 1GB

## 🎯 Current Priority Order

### Immediate (Week 1):
1. Mouse selection (click & drag)
2. Keyboard navigation (arrows, page up/down)
3. Hex input editing
4. Copy/Paste basic

### Short-term (Week 2-3):
5. Search/Replace UI
6. Bookmarks
7. Highlights
8. Custom backgrounds

### Medium-term (Month 1):
9. Multiple encodings
10. TBL support
11. Data inspector
12. Settings dialog

### Long-term (Month 2+):
13. Diff mode
14. Structure overlay
15. Large file optimization

---

## 📝 Implementation Notes

### Dependencies on V1:
- Can reuse most Services (already integrated)
- Can reuse ByteProvider (already integrated)
- Can reuse TBL classes
- Need to adapt UI controls

### Breaking from V1:
- No UserControl per byte (performance)
- Virtualized rendering
- MVVM pattern (easier testing)
- Cleaner separation of concerns

---

**Last Updated:** 2026-02-11
**Version:** V2 Alpha
