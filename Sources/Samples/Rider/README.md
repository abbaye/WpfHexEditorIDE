# 🚀 WpfHexEditor - Rider Sample Projects

**Ready-to-use examples** for JetBrains Rider users.

---

## 📁 Projects

### SimpleExample
**Minimal WPF application** demonstrating basic HexEditor usage.

**Features:**
- Basic file opening
- Simple hex/ASCII viewing
- Read-only mode toggle
- Demonstrates IntelliSense usage

**How to open:**
```bash
# Option 1: Open in Rider directly
rider SimpleExample/RiderSimpleExample.csproj

# Option 2: From command line
cd SimpleExample
dotnet run
```

---

## 💡 For Rider Users

**No visual toolbox?** Don't worry! These examples show how to:
- ✅ Add controls via XAML code
- ✅ Use IntelliSense for properties
- ✅ Preview in XAML Preview window
- ✅ Use Live Templates for fast insertion

**📖 Complete Guide:** [../../../docs/IDE/RIDER_GUIDE.md](../../../docs/IDE/RIDER_GUIDE.md)

---

## 🎯 Quick Tips

### 1. IntelliSense is your friend
```xml
<!-- Start typing and IntelliSense shows all options -->
<hex:HexEditor |
               ^
               Ctrl+Space shows all properties!
```

### 2. Use Live Templates
Import [WpfHexEditor.DotSettings](../../../docs/IDE/WpfHexEditor.DotSettings), then:
- Type `hexed` + Tab → Instant HexEditor
- Type `hexns` + Tab → Instant namespace

### 3. XAML Preview
- View → Tool Windows → XAML Preview
- See changes in real-time!

---

## 📚 More Examples

**Looking for advanced features?**
- **[Main Sample](../WpfHexEditor.Sample.Main/)** - Full-featured application
- **[Legacy Samples](../Legacy/)** - V1 examples
- **[All Samples](../README.md)** - Complete list

---

**Questions?** → [Report issue](https://github.com/abbaye/WpfHexEditorIDE/issues) with `rider` tag
