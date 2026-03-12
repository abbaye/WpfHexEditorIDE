# Options Manager - Dynamic Category Icons Guide

## 📚 Overview

Le système Options Manager supporte maintenant des **icônes et catégories dynamiques** pour chaque page d'options. Chaque plugin ou éditeur peut spécifier sa propre catégorie et son icône, éliminant le besoin de hardcoder ces valeurs.

---

## ✨ Features

### ✅ **Pour les Plugins**
Les plugins implémentant `IPluginWithOptions` peuvent maintenant:
- Spécifier une **catégorie personnalisée** (au lieu de "Plugins" par défaut)
- Définir une **icône emoji** pour leur catégorie
- Grouper plusieurs plugins dans la même catégorie avec la même icône

### ✅ **Pour les Éditeurs Built-In**
Les pages d'options intégrées peuvent spécifier leur icône directement dans `OptionsPageRegistry`:
```csharp
new("Environment", "General", () => new EnvironmentGeneralPage(), "🌍")
```

---

## 🔌 Plugin Implementation

### Basic Implementation (Default Values)

Si votre plugin n'implémente **pas** les nouvelles méthodes, il utilisera les valeurs par défaut:

```csharp
public sealed class MyPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public FrameworkElement CreateOptionsPage() => new MyOptionsPage();
    public void SaveOptions() { /* ... */ }
    public void LoadOptions() { /* ... */ }

    // Defaults:
    // - Category: "Plugins"
    // - Icon: "🔌"
}
```

---

### Custom Category & Icon

Pour spécifier une catégorie et icône personnalisées, **implémentez** les méthodes optionnelles:

```csharp
public sealed class DataInspectorPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public FrameworkElement CreateOptionsPage() => new DataInspectorOptionsPage();
    public void SaveOptions() { /* ... */ }
    public void LoadOptions() { /* ... */ }

    // Custom category and icon
    public string GetOptionsCategory() => "Data Analysis";
    public string GetOptionsCategoryIcon() => "📊";
}
```

**Result:**
```
Options Manager TreeView:
├─ 📊  Data Analysis
│   └─ Data Inspector
│   └─ Pattern Analysis    (si autre plugin dans même catégorie)
├─ 🔌  Plugins
│   └─ Other Plugin        (plugins sans catégorie custom)
```

---

## 🎨 Recommended Icons by Category

| Category Type         | Emoji | Unicode | Example Plugins          |
|-----------------------|-------|---------|--------------------------|
| **Data Analysis**     | 📊    | U+1F4CA | Data Inspector, Statistics|
| **File Operations**   | 📁    | U+1F4C1 | File Comparison, Archive |
| **Debugging Tools**   | 🐛    | U+1F41B | Debugger, Memory Monitor |
| **Editors**           | ✏️    | U+270F  | Custom Editors           |
| **Visualization**     | 📈    | U+1F4C8 | Charts, Graphs           |
| **Security**          | 🔒    | U+1F512 | Encryption, Hashing      |
| **Performance**       | ⚡    | U+26A1  | Profilers, Benchmarks    |
| **Network**           | 🌐    | U+1F310 | Network Analysis         |
| **Parsers**           | 📝    | U+1F4DD | Format Parsers           |
| **Generic Plugins**   | 🔌    | U+1F50C | Miscellaneous            |

---

## 🏗️ Architecture

### OptionsPageDescriptor (Updated)

```csharp
public sealed record OptionsPageDescriptor(
    string Category,           // "Data Analysis"
    string PageName,           // "Data Inspector"
    Func<UserControl> Factory, // () => new MyOptionsPage()
    string? CategoryIcon       // "📊" (optional, default: "📂")
);
```

### Interface Changes

```csharp
public interface IPluginWithOptions
{
    FrameworkElement CreateOptionsPage();
    void SaveOptions();
    void LoadOptions();

    // NEW: Optional methods with default implementations
    string GetOptionsCategory() => "Plugins";
    string GetOptionsCategoryIcon() => "🔌";
}
```

---

## 📦 Examples

### Example 1: Data Analysis Plugin

```csharp
public sealed class StatisticsPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string GetOptionsCategory() => "Data Analysis";
    public string GetOptionsCategoryIcon() => "📊";

    public FrameworkElement CreateOptionsPage() 
        => new StatisticsOptionsPage();

    // ... other methods
}
```

### Example 2: Security Plugin

```csharp
public sealed class EncryptionPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string GetOptionsCategory() => "Security";
    public string GetOptionsCategoryIcon() => "🔒";

    public FrameworkElement CreateOptionsPage() 
        => new EncryptionOptionsPage();

    // ... other methods
}
```

### Example 3: Multiple Plugins in Same Category

```csharp
// Plugin 1
public class HashCalculatorPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string GetOptionsCategory() => "Security";
    public string GetOptionsCategoryIcon() => "🔒";
    // ...
}

// Plugin 2
public class EncryptionPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string GetOptionsCategory() => "Security";
    public string GetOptionsCategoryIcon() => "🔒";
    // ...
}
```

**Result:**
```
Options TreeView:
├─ 🔒  Security
│   ├─ Hash Calculator
│   └─ Encryption
```

---

## 🔄 Dynamic Registration Flow

```
1. Plugin Loaded
   ↓
2. WpfPluginHost detects IPluginWithOptions
   ↓
3. PluginOptionsRegistry.RegisterPluginPage() called
   ↓
4. GetOptionsCategory() & GetOptionsCategoryIcon() invoked
   ↓
5. OptionsPageRegistry.RegisterDynamic(category, pageName, factory, icon)
   ↓
6. PageRegistered event fired
   ↓
7. OptionsEditorControl.RebuildTree() auto-triggered
   ↓
8. TreeView updated with new category/page
```

---

## 🧪 Testing

### Test Case 1: Default Values
```csharp
// Plugin without custom methods
public class SimplePlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public FrameworkElement CreateOptionsPage() => new SimpleOptionsPage();
    public void SaveOptions() { }
    public void LoadOptions() { }
}

// Expected:
// - Category: "Plugins"
// - Icon: "🔌"
```

### Test Case 2: Custom Category
```csharp
// Plugin with custom category
public class CustomPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public string GetOptionsCategory() => "My Tools";
    public string GetOptionsCategoryIcon() => "🛠️";
    // ...
}

// Expected:
// - Category: "My Tools"
// - Icon: "🛠️"
```

---

## 🚀 Migration Guide

### Before (v1.0 - Hardcoded)

```csharp
// Dans OptionsEditorControl.xaml.cs
var categoryIcons = new Dictionary<string, string>
{
    { "Plugins", "🔌" },  // ❌ Hardcoded
    { "Data Tools", "📊" } // ❌ Hardcoded
};
```

### After (v2.0 - Dynamic)

```csharp
// Dans le plugin
public string GetOptionsCategory() => "Data Tools";
public string GetOptionsCategoryIcon() => "📊";  // ✅ Dynamic
```

**No changes needed in OptionsEditorControl!** 🎉

---

## 📝 Best Practices

1. **✅ Use descriptive category names**
   - Good: "Data Analysis", "Security Tools"
   - Bad: "Stuff", "Tools", "Misc"

2. **✅ Choose relevant emojis**
   - Match the emoji to the plugin purpose
   - Use standard emojis for cross-platform compatibility

3. **✅ Group related plugins**
   - Multiple plugins can share the same category
   - Use consistent category names across related plugins

4. **✅ Provide defaults**
   - If unsure, use default "Plugins" category
   - Default icon "🔌" is always appropriate

5. **❌ Avoid hardcoding**
   - Never hardcode category icons in UI code
   - Always retrieve icons from descriptors

---

## 🐛 Troubleshooting

### Issue: Icon not showing

**Cause:** Emoji not supported on the platform  
**Solution:** Use widely-supported emojis (see recommended table)

### Issue: Category not grouping properly

**Cause:** Category name mismatch (case-sensitive)  
**Solution:** Ensure exact same `GetOptionsCategory()` return value

### Issue: Page not refreshing

**Cause:** Event not firing  
**Solution:** Verify `OptionsPageRegistry.RegisterDynamic()` is called

---

## 👥 Contributors

- **Derek Tremblay** - Original architecture
- **Claude Sonnet 4.6** - Dynamic icon system implementation

---

**Last Updated:** 2025-01-XX  
**Version:** 2.0.0
