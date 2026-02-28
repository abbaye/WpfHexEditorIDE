# Converters - Convertisseurs de Valeurs WPF

Convertisseurs de valeurs pour data binding XAML.

## 📁 Contenu

- **ValueConverters.cs** - Plusieurs convertisseurs inline

## 🎯 Convertisseurs Disponibles

```csharp
// Bool → Selection
public class BoolToSelectionConverter : IValueConverter { }

// ByteAction → Brush
public class ActionToBrushConverter : IValueConverter { }

// Bool → Validation Color
public class BoolToValidationColorConverter : IValueConverter { }

// Long → Hex String
public class LongToHexStringConverter : IValueConverter { }

// Byte → Hex String
public class ByteToHexStringConverter : IValueConverter { }

// Bool → Visibility
public class BoolToVisibilityConverter : IValueConverter { }

// Position → String
public class PositionToStringConverter : IValueConverter { }
```

## 💡 Exemples

**Utilisation XAML:**
```xml
<Window.Resources>
    <converters:LongToHexStringConverter x:Key="HexConverter" />
    <converters:BoolToVisibilityConverter x:Key="VisConverter" />
</Window.Resources>

<!-- Convertir position en hex -->
<TextBlock Text="{Binding Position, Converter={StaticResource HexConverter}}" />
<!-- Affiche: "0x00001000" -->

<!-- Visibilité selon bool -->
<Button Visibility="{Binding IsModified, Converter={StaticResource VisConverter}}" />
```

**Exemple: ActionToBrushConverter**
```csharp
// ByteAction.Modified → Orange
// ByteAction.Inserted → Green
// ByteAction.Deleted → Red
<Ellipse Fill="{Binding Action, Converter={StaticResource ActionToBrushConverter}}" />
```

## 🔗 Ressources

- **[Core/Converters/README.md](../Core/Converters/README.md)** - 7 convertisseurs additionnels

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
