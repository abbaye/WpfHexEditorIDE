# Options Manager - Amélioration du Système

## 📋 Résumé des Changements

Ce document décrit les améliorations apportées au système de gestion des options de l'IDE WpfHexEditor pour supporter le rafraîchissement automatique et une interface utilisateur améliorée style Visual Studio.

---

## ✨ Nouvelles Fonctionnalités

### 1. **Rafraîchissement Automatique** 🔄

Le système d'options se met maintenant à jour automatiquement quand un plugin charge ou décharge une page d'options.

#### Changements techniques:
- **`OptionsPageRegistry.cs`**: Ajout d'événements `PageRegistered` et `PageUnregistered`
- **`OptionsEditorControl.xaml.cs`**: Abonnement aux événements pour déclencher `RebuildTree()`
- **`PluginOptionsRegistry.cs`**: Intégration avec `OptionsPageRegistry` pour notifier automatiquement

#### Fonctionnement:
```
Plugin chargé → IPluginWithOptions détecté → RegisterDynamic() appelé 
→ PageRegistered event → OptionsEditorControl.RebuildTree() 
→ TreeView mis à jour automatiquement
```

---

### 2. **TreeView Hiérarchique Style Visual Studio** 🌳

Le menu de sélection des pages a été transformé en un TreeView riche avec:

#### Améliorations visuelles:
- ✅ **Icônes par catégorie** (🌍 Environment, 🔧 Hex Editor, 🔌 Plugins, etc.)
- ✅ **Chevrons d'expansion animés** (▶️ → ▼) avec rotation fluide (150ms)
- ✅ **Effets hover** avec fond semi-transparent
- ✅ **Sélection visuelle** avec couleur de thème dynamique
- ✅ **Indentation hiérarchique** pour distinguer catégories et pages

#### Template personnalisé:
```xml
<ToggleButton Style="{StaticResource ExpandCollapseToggleStyle}">
    <TextBlock Text="▶" RenderTransform="{RotateTransform}"/>
    <!-- Animation de rotation sur IsChecked -->
</ToggleButton>
```

---

### 3. **Préservation de l'État** 💾

Lors du rafraîchissement automatique:
- ✅ La page actuellement sélectionnée est **sauvegardée**
- ✅ Le TreeView est reconstruit avec les nouvelles pages
- ✅ La sélection précédente est **restaurée automatiquement**
- ✅ Si la page n'existe plus, retour à la première page

---

## 📁 Fichiers Modifiés

### Nouveaux Fichiers
1. **`ViewModels/OptionsTreeItemViewModel.cs`** (NOUVEAU)
   - ViewModel pour les items du TreeView
   - Support pour catégories et pages
   - Propriétés: `Name`, `Icon`, `Children`, `IsExpanded`, `IsSelected`

### Fichiers Modifiés
2. **`OptionsPageRegistry.cs`**
   - Ajout: `event PageRegistered`
   - Ajout: `event PageUnregistered`
   - Modification: `RegisterDynamic()` déclenche l'événement
   - Modification: `UnregisterDynamic()` déclenche l'événement

3. **`OptionsEditorControl.xaml.cs`**
   - Ajout: Abonnement aux événements `PageRegistered/Unregistered`
   - Ajout: `RebuildTree()` pour reconstruire le TreeView
   - Ajout: `SaveCurrentSelection()` et `RestoreSelection()`
   - Modification: `BuildTree()` avec icônes par catégorie
   - Ajout: `OnUnloaded()` pour désabonnement

4. **`OptionsEditorControl.xaml`**
   - Nouveau style: `ExpandCollapseToggleStyle` avec animation
   - Template TreeViewItem amélioré avec chevron et indentation
   - Ajout: Triggers pour hover et sélection
   - Animation: Rotation du chevron (0° → 90°)

5. **`WpfHexEditor.PluginHost\Services\PluginOptionsRegistry.cs`**
   - Modification: `RegisterPluginPage()` appelle `OptionsPageRegistry.RegisterDynamic()`
   - Modification: `UnregisterPluginPage()` appelle `OptionsPageRegistry.UnregisterDynamic()`
   - Ajout: Gestion des erreurs avec logging

6. **`WpfHexEditor.PluginHost\WpfHexEditor.PluginHost.csproj`**
   - Ajout: Référence projet vers `WpfHexEditor.Options`

---

## 🔌 Intégration Plugin

### Pour les développeurs de plugins

Aucun changement requis! Si votre plugin implémente déjà `IPluginWithOptions`, il bénéficie automatiquement du nouveau système:

```csharp
public sealed class MyPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    public FrameworkElement CreateOptionsPage()
    {
        return new MyOptionsPage();
    }

    public void SaveOptions() { /* ... */ }
    public void LoadOptions() { /* ... */ }
}
```

**Lors du chargement du plugin:**
1. `WpfPluginHost.LoadPluginAsync()` détecte `IPluginWithOptions`
2. `PluginOptionsRegistry.RegisterPluginPage()` est appelé
3. La page est ajoutée à la catégorie **"Plugins"** dans l'Options Manager
4. L'événement `OptionsPageRegistry.PageRegistered` est déclenché
5. Le TreeView se rafraîchit **automatiquement** sans action manuelle

---

## 🎨 Catégories et Icônes

| Catégorie           | Icône | Description                      |
|---------------------|-------|----------------------------------|
| Environment         | 🌍    | Paramètres généraux de l'IDE     |
| Hex Editor          | 🔧    | Options de l'éditeur hexadécimal |
| Solution Explorer   | 📁    | Gestion de fichiers et projets   |
| Code Editor         | 💻    | Éditeur de code                  |
| Text Editor         | 📝    | Éditeur de texte                 |
| Plugin System       | 🔌    | Système de plugins               |
| **Plugins**         | 🔌    | Pages ajoutées par les plugins   |

---

## 🧪 Tests Suggérés

### Test 1: Rafraîchissement automatique
1. Ouvrir Options Manager
2. Charger un plugin avec `IPluginWithOptions`
3. **Résultat attendu:** La page du plugin apparaît sous "Plugins" sans recharger

### Test 2: Déchargement de plugin
1. Ouvrir Options Manager avec un plugin chargé
2. Décharger le plugin
3. **Résultat attendu:** La page du plugin disparaît automatiquement

### Test 3: Préservation de sélection
1. Sélectionner une page spécifique
2. Charger un nouveau plugin
3. **Résultat attendu:** La page précédemment sélectionnée reste active

### Test 4: TreeView interactif
1. Cliquer sur les chevrons pour expand/collapse
2. **Résultat attendu:** Animation fluide de rotation
3. Hover sur les items
4. **Résultat attendu:** Fond de highlight visible

---

## 🚀 Améliorations Futures (Phase 5)

### Court terme:
- [ ] Badge compteur sur catégories: "Hex Editor (4)"
- [ ] Indicateur visuel pour pages récemment ajoutées (🆕)
- [ ] Animation fade-in lors du changement de page
- [ ] Sauvegarde de l'état d'expansion des catégories

### Moyen terme:
- [ ] Recherche avancée avec highlight des résultats
- [ ] Filtres par type de paramètre (UI, Performance, etc.)
- [ ] Export/Import de configurations
- [ ] Profiles de configuration (Dev, Production, etc.)

---

## 📝 Notes de Migration

**Version précédente:**
- Les pages devaient être manuellement ajoutées à `OptionsPageRegistry._pages`
- Aucun rafraîchissement automatique
- TreeView simple sans hiérarchie visuelle

**Version actuelle (v2.0):**
- ✅ Enregistrement dynamique via événements
- ✅ Rafraîchissement automatique en temps réel
- ✅ TreeView hiérarchique style VS avec animations
- ✅ Préservation de l'état utilisateur

---

## 👥 Contributeurs

- **Derek Tremblay** - Architecture initiale
- **Claude Sonnet 4.6** - Implémentation du système événementiel et du TreeView amélioré

---

## 📅 Historique des Versions

### v2.1.0 (2025-01-XX) - Dynamic Category Icons 🎨
- ✨ **BREAKING CHANGE**: Removed hardcoded category icons
- ✨ Added `CategoryIcon` parameter to `OptionsPageDescriptor`
- ✨ Added `GetOptionsCategory()` and `GetOptionsCategoryIcon()` to `IPluginWithOptions`
- ✨ Plugins can now specify custom categories and icons
- ✨ Default values provided for backward compatibility ("Plugins", "🔌")
- ✨ Updated all built-in pages to specify their icons explicitly
- 🐛 Fixed: Category icons now retrieved dynamically from descriptors
- 📚 Added comprehensive guide: `DYNAMIC_ICONS_GUIDE.md`

### v2.0.0 (2025-01-XX)
- ✨ Ajout du système événementiel pour rafraîchissement automatique
- ✨ TreeView hiérarchique style Visual Studio avec animations
- ✨ Préservation de l'état lors du rafraîchissement
- ✨ Intégration automatique avec PluginHost

### v1.0.0
- ✅ Système d'options de base
- ✅ Support de `IPluginWithOptions`
- ✅ Auto-save des paramètres

---

**Pour toute question ou suggestion, référez-vous au plan de développement principal ou créez une issue sur GitHub.**
