# Système Multilingue - WPF Hex Editor Control

## 📋 Vue d'ensemble

Le WPF Hex Editor Control dispose d'un système de localisation complet supportant 6 langues via des fichiers de ressources .NET (.resx).

## 🌍 Langues supportées

| Langue | Code | Fichier | Statut |
|--------|------|---------|--------|
| 🇺🇸 English (US) | `en` | `Resources.resx` | ✅ 109 entrées |
| 🇨🇦 Français (CA) | `fr-CA` | `Resources.fr-CA.resx` | ✅ 105 entrées |
| 🇵🇱 Polski | `pl-PL` | `Resources.pl-PL.resx` | ✅ 109 entrées |
| 🇧🇷 Português (BR) | `pt-BR` | `Resources.pt-BR.resx` | ✅ 109 entrées |
| 🇷🇺 Русский | `ru-RU` | `Resources.ru-RU.resx` | ✅ 109 entrées |
| 🇨🇳 中文 (CN) | `zh-CN` | `Resources.zh-CN.resx` | ✅ 109 entrées |

## 📂 Architecture

```
WPFHexaEditor/
├── Properties/
│   ├── Resources.resx              # English (default)
│   ├── Resources.fr-CA.resx        # French (Canadian)
│   ├── Resources.pl-PL.resx        # Polish
│   ├── Resources.pt-BR.resx        # Portuguese (Brazilian)
│   ├── Resources.ru-RU.resx        # Russian
│   ├── Resources.zh-CN.resx        # Chinese (Simplified)
│   └── Resources.Designer.cs       # Auto-generated accessor class
```

## 🔧 Implémentation technique

### 1. Fichiers de ressources (.resx)

Chaque fichier .resx contient des paires clé-valeur pour les chaînes localisées :

```xml
<data name="UndoString" xml:space="preserve">
  <value>Undo</value>
</data>
```

### 2. Fichier Designer (Resources.Designer.cs)

Génère automatiquement des propriétés statiques pour accéder aux ressources :

```csharp
public static string UndoString {
    get {
        return ResourceManager.GetString("UndoString", resourceCulture);
    }
}
```

### 3. Utilisation dans XAML

Les ressources sont accessibles via `x:Static` avec le namespace `prop:` :

```xml
<UserControl xmlns:prop="clr-namespace:WpfHexaEditor.Properties">
    <MenuItem Header="{x:Static prop:Resources.UndoString}"/>
</UserControl>
```

### 4. Utilisation dans C#

```csharp
using WpfHexaEditor.Properties;

string message = Resources.UndoString;
```

## 📝 Catégories de ressources

### Menu contextuel principal (HexEditor)
- `UndoString` - Undo
- `BookmarksString` - Bookmarks
- `SetBookMarkString` - Set Bookmark
- `ClearBookMarkString` - Clear All Bookmarks
- `CopySelectionAsString` - Copy As...
- `CopyAsHexadecimalString` - Copy as Hexadecimal
- `CopyAsASCIIString` - Copy as ASCII
- `CopyAsCSharpCodeString` - Copy as C# Code ⭐ *nouveau*
- `CopyAsCCodeString` - Copy as C Code ⭐ *nouveau*
- `CopyAsTBLString` - Copy as TBL String
- `FindAllString` - Find All
- `PasteString` - Paste ⭐ *nouveau*
- `PasteNotInsertString` - Paste without inserting
- `DeleteString` - Delete
- `FillWithByteString` - Fill with Byte... ⭐ *nouveau*
- `ReplaceByteMenuString` - Replace Byte... ⭐ *nouveau*
- `ReverseSelectionString` - Reverse Selection ⭐ *nouveau*
- `SelectAllString` - Select all

### Menu contextuel StatusBar
- `StatusMessageString` - Status Message ⭐ *nouveau*
- `FileSizeString` - File Size ⭐ *nouveau*
- `SelectionInfoString` - Selection Info ⭐ *nouveau*
- `PositionString` - Position
- `EditModeString` - Edit Mode ⭐ *nouveau*
- `BytesPerLineString` - Bytes Per Line ⭐ *nouveau*
- `RefreshTimeString` - Refresh Time ⭐ *nouveau*

### Headers & Labels
- `OffsetString` - Offset ⭐ *nouveau*
- `ReadyString` - Ready ⭐ *nouveau*
- `NoSelectionString` - No selection ⭐ *nouveau*
- `ModeOverwriteString` - Mode: Overwrite ⭐ *nouveau*

### Recherche & Navigation
- `FindString` - Find
- `FindNextString` - Find Next
- `FindLastString` - Find Last
- `FindFirstString` - Find First
- `FindAndReplaceString` - Find and Replace
- `ReplaceString` - Replace
- `ReplaceAllString` - Replace All

### Autres catégories
- **Clipboard** : `CopyAsDecimalString`, `CopyAsTBLString`
- **Bookmarks** : `BookmarksString`, `SetBookMarkString`, `ClearBookMarkString`
- **Dialog** : `OkString`, `CancelString`, `CloseString`
- **File Operations** : `OpenAsReadOnlyString`, `FileDroppingConfirmationString`
- **Status** : `BytesTagString`, `KBTagString`, `MBTagString`
- **TBL** : `TBLString`, `DefaultTBLString`, `EndTagString`, `LineTagString`
- **Theme** : `ThemeLightString`, `ThemeDarkGlassString`, `ThemeCyberpunkString`
- **Zoom** : `ZoomString`, `ZoomResetString`

## 🔄 Sélection de la langue

La langue est automatiquement sélectionnée selon la culture du système Windows :

```csharp
// Automatique (basé sur Thread.CurrentThread.CurrentUICulture)
string text = Resources.UndoString;

// Manuel (changer la culture)
System.Globalization.CultureInfo.CurrentUICulture =
    new System.Globalization.CultureInfo("fr-CA");
```

## 📖 Guide pour ajouter une nouvelle ressource

### 1. Ajouter dans tous les fichiers .resx

**Resources.resx (EN)**
```xml
<data name="NewFeatureString" xml:space="preserve">
  <value>New Feature</value>
</data>
```

**Resources.fr-CA.resx**
```xml
<data name="NewFeatureString" xml:space="preserve">
  <value>Nouvelle fonctionnalité</value>
</data>
```

*Répéter pour tous les 6 fichiers*

### 2. Ajouter dans Resources.Designer.cs

```csharp
/// <summary>
///   Looks up a localized string similar to New Feature.
/// </summary>
public static string NewFeatureString {
    get {
        return ResourceManager.GetString("NewFeatureString", resourceCulture);
    }
}
```

### 3. Utiliser dans le XAML

```xml
<MenuItem Header="{x:Static prop:Resources.NewFeatureString}"/>
```

## 🎨 Bonnes pratiques

### Conventions de nommage

- Toujours suffixer avec `String` : `UndoString`, `PasteString`
- Utiliser PascalCase : `CopyAsHexadecimalString`
- Être descriptif : `ReplaceByteMenuString` vs `ReplaceString`

### Traductions

- **Contexte** : Fournir du contexte aux traducteurs (commentaires dans .resx)
- **Cohérence** : Utiliser les mêmes termes pour les concepts similaires
- **Longueur** : Éviter les chaînes trop longues (problèmes d'UI)
- **Placeholders** : Utiliser `{0}`, `{1}` pour les valeurs dynamiques

### Performance

- Les ressources sont chargées **à la demande** (lazy loading)
- Utilisation de `ResourceManager` pour un cache efficace
- Pas de pénalité de performance pour le multilingue

## 🔍 Débogage

### Vérifier la langue active

```csharp
var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
Console.WriteLine($"Current UI Culture: {culture.Name}"); // ex: "fr-CA"
```

### Tester une langue spécifique

```csharp
// Dans App.xaml.cs ou au démarrage
System.Threading.Thread.CurrentThread.CurrentUICulture =
    new System.Globalization.CultureInfo("pl-PL");
```

### Ressource manquante

Si une ressource n'existe pas dans une langue, le système **fallback** automatiquement vers la ressource par défaut (EN).

## 📊 Statistiques

- **Total langues** : 6
- **Total ressources** : 109 par langue
- **Couverture actuelle** : 100% (tous les menus et labels traduits)
- **Historique V1** : 100% couverture maintenue pendant migration (V1 removed v2.6.0)

## 🚀 Nouveautés (Février 2026)

### Ajout de 16 nouvelles ressources (Février 2026)

Toutes les chaînes du HexEditor utilisent maintenant le système de ressources :

- ✅ Menu contextuel de l'éditeur entièrement localisé
- ✅ Menu contextuel de la StatusBar localisé
- ✅ Headers de colonnes localisés
- ✅ Messages de statut localisés
- ✅ Support du temps de rafraîchissement (Refresh Time)

### Migration V1 → V2

**Avant (V1)** : Utilisait `Properties.Resources.*` ✅
**Avant (V2)** : Chaînes en dur dans le XAML ❌
**Maintenant (V2)** : Utilise `{x:Static prop:Resources.*}` ✅

## 🔗 Intégration dans l'architecture globale

```mermaid
flowchart TD
    A["🖥️ HexEditor (View)<br/>WPF XAML UI"]
    B["⚙️ Properties.Resources<br/>Auto-generated accessor class"]
    C["📁 Fichiers .resx<br/>19 langues"]

    A -->|"{x:Static prop:Resources.*}"| B
    B -->|"ResourceManager<br/>+ Cache"| C

    style A fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
    style B fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    style C fill:#e8f5e9,stroke:#388e3c,stroke-width:2px

    subgraph "Binding automatique"
        A
    end

    subgraph "Propriétés statiques"
        B
    end

    subgraph "Ressources"
        C
        D["EN (default) | FR-CA, FR-FR<br/>DE-DE | ES-ES, ES-419<br/>IT-IT | JA-JP | KO-KR<br/>NL-NL | PL-PL<br/>PT-BR, PT-PT | RU-RU<br/>SV-SE | TR-TR | ZH-CN<br/>AR-SA | HI-IN"]
        C -.-> D
    end
```

## ✅ Checklist de migration pour nouveaux composants

Lorsque vous créez un nouveau composant WPF :

- [ ] Identifier toutes les chaînes visibles par l'utilisateur
- [ ] Créer des clés de ressources descriptives (`*String`)
- [ ] Ajouter dans tous les 6 fichiers .resx
- [ ] Ajouter les propriétés dans Resources.Designer.cs
- [ ] Remplacer les chaînes en dur par `{x:Static prop:Resources.*}`
- [ ] Tester avec différentes cultures
- [ ] Vérifier que les traductions sont cohérentes

## 📚 Ressources additionnelles

- [Documentation .NET sur la localisation](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization)
- [Guide WPF Localization](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview)
- [ResX File Format](https://learn.microsoft.com/en-us/dotnet/framework/resources/creating-resource-files-for-desktop-apps)

---

**Auteurs** : Derek Tremblay, Claude Sonnet 4.5
**Date de mise à jour** : 15 février 2026
**Version** : 2.6.0
