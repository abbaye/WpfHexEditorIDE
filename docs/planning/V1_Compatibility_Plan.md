# Plan de Compatibilité V1 → V2 - État Actuel (Mise à Jour 2026-02-13)

---
> **✅ PLAN COMPLETED - V1 REMOVED**
>
> V1 compatibility achieved 100% during migration period (v2.5-v2.6).
> **Final outcome:** V1 Legacy removed in v2.6.0 (Feb 22, 2026) - 17,093 LOC deleted
>
> This document shows the compatibility work that enabled smooth V1→V2 transition.
---

## 🆕 Dernières Mises à Jour (2026-02-13)

### ✅ Fonctionnalités Récemment Ajoutées

#### Auto-Highlight Byte Feature (V1 Compatibility)
- **Implémenté:** Feature qui surligne tous les bytes correspondant à la valeur du byte sélectionné
- **Problème initial:** Highlighting ne se mettait pas à jour lors du clic sur différents bytes
- **Solution:** Ajout d'appel à `UpdateAutoHighlightByte()` dans le handler `SelectionStart` changed
- **Résultat:** Highlighting jaune dynamique qui suit la sélection
- **Commit:** `dbd0522` - "Fix and optimize auto-highlight and double-click select features"

#### Double-Click Select Feature (V1 Compatibility)
- **Implémenté:** Double-clic sur un byte sélectionne tous les bytes de même valeur
- **Approche v1:** Surlignage visuel avec `HighlightedPositions`
- **Approche v2:** Sélection réelle avec `SelectionStart`/`SelectionStop` (firstMatch → lastMatch)
- **Optimisation:** Recherche seulement dans la région visible (cache des lignes)
- **Commit:** `bbec374` - "Fix double-click select to use real selection"
- **État actuel:** 🔧 EN DÉBOGAGE (feature ne fonctionne pas correctement)

#### Debug Logging Ajouté
- **Ajouté:** Logs complets dans `HexViewport_ByteDoubleClicked()` et `SelectAllBytesWith()`
- **But:** Identifier pourquoi le double-clic ne déclenche pas la sélection
- **Debug actif:** Affichage dans StatusText + System.Diagnostics.Debug.WriteLine()

### 🚀 Restructuration Proposée: V2→Main / V1→Legacy

#### Exploration Complète de la Structure Actuelle
**Exploration effectuée:** Architecture complète analysée via agents Explore
- **V1 Files:** HexEditor.xaml/cs, BaseByte.cs, HexByte.cs, StringByte.cs (namespace: `WpfHexaEditor`)
- **V2 Files:** V2/HexEditorV2.xaml/cs, V2/ViewModels/, V2/Models/, V2/Controls/ (namespace: `WpfHexaEditor.V2`)
- **Shared:** Core/, Services/, ByteProvider (partagé par V1 et V2)

#### Plan de Restructuration
**Structure proposée:**
```
WPFHexaEditor/
├── Core/           (shared - unchanged)
├── Services/       (shared - unchanged)
├── Main/           (V2 files → renamed)
│   ├── HexEditor.xaml      (était HexEditorV2.xaml)
│   ├── HexEditor.xaml.cs   (était HexEditorV2.xaml.cs)
│   ├── ViewModels/
│   ├── Models/
│   └── Controls/
├── Legacy/         (V1 files → moved)
│   ├── HexEditor.xaml      (V1 original)
│   ├── HexEditor.xaml.cs   (V1 original)
│   ├── BaseByte.cs
│   ├── HexByte.cs
│   └── StringByte.cs
└── Properties/, Resources/
```

**Namespace Changes:**
- V2: `WpfHexaEditor.V2` → `WpfHexaEditor.Main` (ou simplement `WpfHexaEditor`)
- V1: `WpfHexaEditor` → `WpfHexaEditor.Legacy`

**Samples to Update:**
- 10 samples total trouvés
- HexEditorV2.Sample (V2) - namespace update
- WPFHexEditor.Sample.CSharp (dual-stack) - namespaces V1+V2 update
- 8 autres samples V1 - namespace update

**État:** ⏳ PRÊT À IMPLÉMENTER (en attente validation utilisateur)

### 📚 Documentation à Générer

#### Phase 10: Architecture et Documentation Avancée
**Objectifs:**
- Diagrammes UML MVVM architecture
- Diagrammes de séquence (Open, Edit, Save operations)
- Documentation des patterns (Services, VirtualPosition, DrawingContext)
- Migration guide complet V1→V2
- Quick Start guide
- API Reference (mise à jour)

**État:** ⏳ PLANIFIÉ (prêt après restructuration)

#### Phase 11: Tests et Benchmarks Complets
**Objectifs:**
- Tests unitaires compatibilité V1
- Tests d'intégration avec samples
- Benchmarks V1 vs V2 performance
- Tests de stabilité et memory leaks

**État:** ⏳ PLANIFIÉ

---

## Résumé de l'État Actuel

### ✅ Phases Complétées (Phases 1-6)

- **Phase 1: Compatibilité Types (Brush/Color)** ✅ 100%
  - Toutes les propriétés Color exposées comme Brush pour V1
  - Conversion automatique Brush ↔ Color

- **Phase 2: Propriétés de Visibilité** ✅ 100%
  - `HexDataVisibility`, `StringDataVisibility`, `HeaderVisibility`
  - `StatusBarVisibility`, `LineInfoVisibility`, `BarChartPanelVisibility`
  - Mapping Visibility ↔ bool

- **Phase 3: Recherche String** ✅ 100%
  - `FindFirst(string)`, `FindNext(string)`, `FindLast(string)`
  - `ReplaceFirst(string, string)`, `ReplaceNext(...)`, `ReplaceAll(...)`
  - Support encoding UTF8/ASCII/Custom

- **Phase 4: Événements Granulaires** ✅ 100%
  - Tous les 15+ événements V1 implémentés
  - `SelectionStartChanged`, `SelectionStopChanged`, `DataCopied`, etc.

- **Phase 5: Propriétés de Configuration** ✅ 100%
  - `AllowContextMenu`, `AllowZoom`, `MouseWheelSpeed`
  - `DataStringVisual`, `OffSetStringVisual`, `ByteOrder`, `ByteSize`
  - `AllowFileDrop`, `ShowByteToolTip`, `CustomEncoding`

- **Phase 6: Méthodes V1** ✅ 100%
  - Toutes les méthodes publiques V1 implémentées
  - `SetPosition(...)`, `SubmitChanges()`, `ModifyByte(...)`
  - `CopyToStream(...)`, `GetLineNumber(...)`, etc.

### ⏳ Phases Partiellement Complétées

#### Phase 7: Fonctionnalités Avancées (90% complété)

**✅ Déjà Implémenté:**
- 7.1: Custom Background Blocks ✅
  - `CustomBackgroundBlockItems` property
  - `AddCustomBackgroundBlock()`, `ClearCustomBackgroundBlock()`
  - Rendu dans HexViewport

- 7.4: Bar Chart Panel ✅
  - `BarChartPanel` control complet
  - `BarChartPanelVisibility` property
  - Analyse de fréquence des bytes

- 7.5: TBL Avancé ✅
  - Support MTE/DTE coloring
  - `TblShowMte`, `TbldteColor`, `TblmteColor`
  - Rendu dans HexViewport

- 7.2: File Comparison ✅
  - `Compare(HexEditorV2 other)` method
  - `Compare(ByteProvider provider)` method
  - Return `IEnumerable<ByteDifference>`

- 7.3: State Persistence ✅
  - `SaveCurrentState(string filename)` method
  - `LoadCurrentState(string filename)` method

- 7.6: Column Coloring ✅ **[COMPLÉTÉ AUJOURD'HUI]**
  - Alternance Noir/Bleu tous les bytes (1 noir, 1 bleu)
  - `ForegroundSecondColor` property

**🔧 Améliorations Récentes:**
- ✅ Insert mode paste fix (crée ByteAction.Added au lieu de Modified)
- ✅ Inserted bytes visibility (fond vert clair + bordure verte)

#### Phase 8: DependencyProperty Conversion (60% complété)

**✅ Déjà DependencyProperty:**
- ✅ `FileName` (string)
- ✅ `ZoomScale` (double)
- ✅ Toutes les couleurs (`SelectionFirstColor`, `ByteModifiedColor`, etc.)
- ✅ `ShowByteToolTip` (bool)
- ✅ `BarChartPanelVisibility` (Visibility)

**⏳ À Convertir en DependencyProperty:**
- ❌ `IsFileOrStreamLoaded` (bool, read-only)
- ❌ `SelectionStart` (long)
- ❌ `SelectionStop` (long)
- ❌ `SelectionLine` (long)
- ❌ `BytePerLine` (int)
- ❌ `ReadOnlyMode` (bool)
- ❌ `EditMode` (EditMode enum)
- ❌ `TypeOfCharacterTable` (CharacterTableType enum)
- ❌ `AllowContextMenu` (bool)
- ❌ `AllowZoom` (bool)
- ❌ `MouseWheelSpeed` (MouseWheelSpeed enum)

**Priorité: HAUTE** - Requis pour binding XAML dans samples V1

### 🔴 Phases Non Commencées

#### Phase 9: Alias et Documentation (0% complété)

**Objectifs:**
- Créer extension methods pour simplifier migration
- Documenter toutes les différences V1/V2
- Ajouter attributs `[Obsolete]` pour propriétés dépréciées
- Créer guide de migration

**Fichiers à créer:**
- `V2/Compatibility/V1Aliases.cs`
- `V2/Compatibility/V1Events.cs`
- `docs/MigrationGuide.md`

**Priorité: MOYENNE**

#### Phase 10: Architecture et Documentation (0% complété)

**Objectifs:**
- Diagrammes UML de l'architecture MVVM
- Documentation des patterns utilisés
- API Reference complète (✅ déjà fait via docs/ApiReference.md)
- Quick Start Guide

**Priorité: BASSE** (documentation)

#### Phase 11: Tests et Benchmarks (0% complété)

**11.1 Tests Unitaires:**
- Tests de compatibilité V1 (types, événements, méthodes)
- Tests ViewModel (Insert, Delete, Modify, Undo/Redo)
- Tests ByteProvider (I/O, modifications, persistance)

**11.2 Tests d'Intégration:**
- Exécuter samples V1 avec V2 (WPFHexEditor.Sample.CSharp, etc.)
- Tests end-to-end (Open → Modify → Save)
- Tests de régression

**11.3 Benchmarks de Performance:**
- Rendu (V1 vs V2 DrawingContext)
- Opérations (Open, Find, Replace, Insert/Delete)
- Mémoire (Memory leaks, GC pressure)

**Priorité: HAUTE** (tests critiques pour valider 100% compatibilité)

---

## Plan d'Action Prioritaire

### 🔥 PRIORITÉ 1: Phase 8 - DependencyProperty (2-3h)

**Objectif:** Permettre le binding XAML dans les samples V1

#### Tâches:
1. **Convertir propriétés critiques en DependencyProperty:**
   ```csharp
   // V2/HexEditorV2.xaml.cs

   // SelectionStart
   public static readonly DependencyProperty SelectionStartProperty =
       DependencyProperty.Register(nameof(SelectionStart), typeof(long),
           typeof(HexEditorV2), new PropertyMetadata(0L, OnSelectionStartChanged));

   public long SelectionStart
   {
       get => (long)GetValue(SelectionStartProperty);
       set => SetValue(SelectionStartProperty, value);
   }

   private static void OnSelectionStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
   {
       if (d is HexEditorV2 editor && e.NewValue is long position)
           editor._viewModel.SelectionStart = new VirtualPosition(position);
   }
   ```

2. **Liste des propriétés à convertir (par ordre de priorité):**
   - `SelectionStart` (long) - Binding XAML
   - `SelectionStop` (long) - Binding XAML
   - `BytePerLine` (int) - Configuration UI
   - `ReadOnlyMode` (bool) - État du contrôle
   - `EditMode` (EditMode) - Insert/Overwrite
   - `IsFileOrStreamLoaded` (bool, read-only) - État du contrôle
   - `TypeOfCharacterTable` (CharacterTableType) - Configuration
   - `AllowContextMenu`, `AllowZoom`, `MouseWheelSpeed` - Configuration

3. **Pattern à suivre:**
   - Créer DependencyProperty avec `Register()`
   - Créer property wrapper `get/set`
   - Créer callback `OnPropertyChanged` qui propage vers ViewModel
   - Pour read-only properties, utiliser `RegisterReadOnly()`

**Fichiers à modifier:**
- `V2/HexEditorV2.xaml.cs` (ajouter ~10 DependencyProperties)

**Validation:**
- Compiler sans erreurs
- Tester binding XAML: `<v2:HexEditorV2 SelectionStart="{Binding MyPosition}"/>`
- Vérifier que les changements se propagent correctement au ViewModel

---

### 🔥 PRIORITÉ 2: Phase 11.1 - Tests Unitaires V1 Compatibility (3-4h)

**Objectif:** Valider que toutes les fonctionnalités V1 fonctionnent correctement

#### Tâches:
1. **Créer projet de tests:**
   ```bash
   # Dans Sources/
   dotnet new xunit -n WPFHexaEditor.V2.Tests
   cd WPFHexaEditor.V2.Tests
   dotnet add reference ../WPFHexaEditor/WPFHexaEditor.csproj
   dotnet add package FluentAssertions
   ```

2. **Tests de compatibilité de type (Brush ↔ Color):**
   ```csharp
   // Tests/CompatibilityTests/ColorBrushTests.cs
   [Fact]
   public void SelectionFirstColor_CanSetAsBrush()
   {
       var editor = new HexEditorV2();
       var brush = new SolidColorBrush(Colors.Red);

       editor.SelectionFirstColorBrush = brush;

       editor.SelectionFirstColor.Should().Be(Colors.Red);
   }
   ```

3. **Tests d'événements V1:**
   ```csharp
   // Tests/CompatibilityTests/EventTests.cs
   [Fact]
   public void SelectionStartChanged_ShouldFire_WhenSelectionStartChanges()
   {
       var editor = new HexEditorV2();
       bool eventFired = false;
       editor.SelectionStartChanged += (s, e) => eventFired = true;

       editor.SelectionStart = 100;

       eventFired.Should().BeTrue();
   }
   ```

4. **Tests de méthodes V1:**
   ```csharp
   // Tests/CompatibilityTests/MethodTests.cs
   [Fact]
   public void FindFirst_WithString_ShouldFindMatch()
   {
       var editor = new HexEditorV2();
       editor.OpenFile("test.bin");

       long position = editor.FindFirst("Hello");

       position.Should().BeGreaterThanOrEqualTo(0);
   }
   ```

**Fichiers à créer:**
- `Tests/WPFHexaEditor.V2.Tests/CompatibilityTests/ColorBrushTests.cs`
- `Tests/WPFHexaEditor.V2.Tests/CompatibilityTests/EventTests.cs`
- `Tests/WPFHexaEditor.V2.Tests/CompatibilityTests/MethodTests.cs`
- `Tests/WPFHexaEditor.V2.Tests/CompatibilityTests/VisibilityTests.cs`

**Validation:**
- Tous les tests passent
- Couverture de code > 80% pour propriétés/méthodes V1

---

### 🔥 PRIORITÉ 3: Phase 11.2 - Tests d'Intégration avec Samples V1 (2-3h)

**Objectif:** Vérifier que les samples V1 fonctionnent avec V2 sans modification

#### Tâches:
1. **Créer copies des samples V1 utilisant V2:**
   ```
   Samples/
   ├── WPFHexEditor.Sample.CSharp/         (V1 - existant)
   ├── WPFHexEditor.Sample.CSharp.V2/      (V2 - nouveau)
   ├── HexEditorV2.Sample/                 (V2 - existant)
   └── WpfHexEditor.Sample.InsertByteAnywhere.V2/ (V2 - nouveau)
   ```

2. **Remplacer références V1 par V2:**
   ```xml
   <!-- Dans .csproj -->
   <!-- <local:HexEditor x:Name="HexEdit" ... /> -->
   <v2:HexEditorV2 x:Name="HexEdit" ... />
   ```

3. **Exécuter et vérifier comportement:**
   - WPFHexEditor.Sample.CSharp → Ouvrir fichier, éditer, sauver
   - WpfHexEditor.Sample.BinaryFilesDifference → Comparer 2 fichiers
   - WpfHexEditor.Sample.InsertByteAnywhere → Mode Insert
   - WpfHexEditor.Sample.BarChart → Afficher bar chart

4. **Créer tests automatisés:**
   ```csharp
   // Tests/IntegrationTests/SampleCompatibilityTests.cs
   [Fact]
   public void Sample_OpenFile_ShouldLoadCorrectly()
   {
       var editor = new HexEditorV2();
       editor.OpenFile("TestFiles/sample.bin");

       editor.IsFileOrStreamLoaded.Should().BeTrue();
       editor.Length.Should().BeGreaterThan(0);
   }
   ```

**Validation:**
- Samples V1 compilent avec V2
- Samples V1 s'exécutent correctement avec V2
- Aucune régression de fonctionnalité

---

### 📋 PRIORITÉ 4: Phase 9 - Alias et Documentation (1-2h)

**Objectif:** Faciliter la migration V1→V2 pour les développeurs

#### Tâches:
1. **Créer classe d'alias:**
   ```csharp
   // V2/Compatibility/V1Aliases.cs
   namespace WpfHexaEditor.V2.Compatibility
   {
       public static class V1Compatibility
       {
           [Obsolete("Use ClearSelection() instead")]
           public static void UnSelectAll(this HexEditorV2 editor, bool cleanFocus = false)
           {
               editor.ClearSelection();
           }

           [Obsolete("Use SetBookmark() instead")]
           public static void SetBookMark(this HexEditorV2 editor)
           {
               editor.SetBookmark();
           }

           [Obsolete("Use Copy() instead")]
           public static void CopyToClipboard(this HexEditorV2 editor)
           {
               editor.Copy();
           }
       }
   }
   ```

2. **Créer guide de migration:**
   ```markdown
   # docs/MigrationGuide.md

   ## Migration V1 → V2

   ### Changements de Nommage
   | V1 Method | V2 Method | Notes |
   |-----------|-----------|-------|
   | `UnSelectAll()` | `ClearSelection()` | Renamed for clarity |
   | `SetBookMark()` | `SetBookmark()` | Fixed typo |
   | `CopyToClipboard()` | `Copy()` | Simplified |

   ### Changements de Type
   | V1 Type | V2 Type | Migration |
   |---------|---------|-----------|
   | `Brush` | `Color` | Use `SelectionFirstColorBrush` for V1 compat |
   | `Visibility` | `bool` | Use `HeaderVisibility` for V1 compat |
   ```

3. **Ajouter XML comments pour migration:**
   ```csharp
   /// <summary>
   /// V1 compatible property. Use <see cref="ShowHeader"/> in new code.
   /// </summary>
   [Obsolete("Use ShowHeader property instead", false)]
   public Visibility HeaderVisibility { get; set; }
   ```

**Fichiers à créer:**
- `V2/Compatibility/V1Aliases.cs`
- `docs/MigrationGuide.md`
- `docs/BreakingChanges.md`

---

### 📊 PRIORITÉ 5: Phase 11.3 - Benchmarks Performance (2-3h)

**Objectif:** Vérifier que V2 maintient le gain de performance (99% boost)

#### Tâches:
1. **Créer projet de benchmarks:**
   ```bash
   dotnet new console -n WPFHexaEditor.Benchmarks
   dotnet add package BenchmarkDotNet
   ```

2. **Benchmark de rendu:**
   ```csharp
   // Benchmarks/RenderingBenchmarks.cs
   [MemoryDiagnoser]
   public class RenderingBenchmarks
   {
       [Benchmark]
       public void V2_RenderViewport_1000Lines()
       {
           var viewport = new HexViewport();
           viewport.UpdateLines(GenerateLines(1000));
           viewport.InvalidateVisual();
       }
   }
   ```

3. **Benchmark d'opérations:**
   ```csharp
   [Benchmark]
   [Arguments("1MB.bin")]
   [Arguments("10MB.bin")]
   [Arguments("100MB.bin")]
   public void OpenFile(string filename)
   {
       var editor = new HexEditorV2();
       editor.OpenFile(filename);
   }
   ```

4. **Exécuter et documenter résultats:**
   ```bash
   dotnet run -c Release --project Benchmarks/
   # Résultats attendus: V2 >= 90% plus rapide que V1
   ```

**Validation:**
- Rendu V2 > 10x plus rapide que V1 (grâce à DrawingContext)
- Pas de memory leaks détectés
- GC pressure < V1

---

### 📚 PRIORITÉ 6: Phase 10 - Architecture et Documentation (6-8h)

**Objectif:** Documenter l'architecture pour maintenabilité future

#### Tâches:
1. **Créer diagrammes d'architecture:**
   ```mermaid
   graph TD
       A[HexEditorV2.xaml] --> B[HexEditorViewModel]
       B --> C[ByteProvider]
       B --> D[Services]
       D --> E[UndoRedoService]
       D --> F[ClipboardService]
       D --> G[SearchService]
   ```

2. **Documenter patterns:**
   - MVVM separation
   - VirtualPosition mapping
   - DrawingContext custom rendering
   - Line caching strategy

3. **Créer Quick Start Guide:**
   ```markdown
   # Getting Started with HexEditorV2

   ## Installation
   ```csharp
   <v2:HexEditorV2 x:Name="HexEdit"/>
   ```

   ## Open a file
   ```csharp
   HexEdit.OpenFile("myfile.bin");
   ```
   ```

**Fichiers à créer:**
- `docs/Architecture.md`
- `docs/QuickStart.md`
- `docs/Performance.md`
- `docs/diagrams/` (Mermaid diagrams)

---

## Timeline et Effort

| Phase | Description | Temps Estimé | Priorité |
|-------|-------------|--------------|----------|
| **Phase 8** | DependencyProperty conversion | 2-3h | 🔥 CRITIQUE |
| **Phase 11.1** | Tests unitaires V1 | 3-4h | 🔥 CRITIQUE |
| **Phase 11.2** | Tests intégration samples | 2-3h | 🔥 HAUTE |
| **Phase 9** | Alias et migration guide | 1-2h | 📋 MOYENNE |
| **Phase 11.3** | Benchmarks performance | 2-3h | 📊 MOYENNE |
| **Phase 10** | Architecture docs | 6-8h | 📚 BASSE |
| **TOTAL** | | **16-23h** | |

## Critères de Réussite (100% V1 Compatibilité)

### ✅ Déjà Atteints
- ✅ Toutes les propriétés publiques V1 exposées
- ✅ Tous les événements V1 déclenchés
- ✅ Toutes les méthodes V1 fonctionnent
- ✅ Architecture MVVM V2 préservée
- ✅ Performance V2 maintenue (DrawingContext custom)

### 🔲 Restants
- ⏳ Binding XAML V1 fonctionne (attente Phase 8)
- ⏳ Tous les samples V1 fonctionnent avec V2 (attente Phase 11.2)
- ⏳ Tests unitaires passent (attente Phase 11.1)
- ⏳ Benchmarks valident performance (attente Phase 11.3)

---

## Next Steps - Recommandation

**Je recommande de commencer par:**

1. **Phase 8 (2-3h)** - DependencyProperty conversion
   - Critique pour binding XAML
   - Bloque les tests d'intégration avec samples V1

2. **Phase 11.1 (3-4h)** - Tests unitaires
   - Valide que tout fonctionne correctement
   - Détecte les régressions

3. **Phase 11.2 (2-3h)** - Tests samples V1
   - Preuve finale de 100% compatibilité
   - Validation end-to-end

**Total effort minimum pour 100% compatibilité validée: 7-10h**

---

## Documentation de l'Issue #31

**GitHub Issue #31: "Ability to insert bytes everywhere"**

### Statut: ✅ RÉSOLU dans V2

V2 implémente nativement le mode Insert avec les fonctionnalités suivantes:

1. **EditMode.Insert natif:**
   - Insertion de bytes sans écraser (vs V1 qui émulait l'insertion)
   - VirtualPosition mapping automatique
   - ByteAction.Added pour bytes insérés (fond vert + bordure verte)

2. **Paste en mode Insert:**
   - ✅ FIXÉ: Le paste crée maintenant ByteAction.Added au lieu de Modified
   - Respect automatique du EditMode (Insert vs Overwrite)

3. **Visibilité des bytes insérés:**
   - ✅ AMÉLIORÉ: Fond vert clair + bordure verte (plus visible)
   - Visibles dans hex ET ASCII panels

4. **VirtualLength mis à jour:**
   - ✅ FIXÉ: La longueur affichée reflète VirtualLength (avec insertions)
   - Status bar montre la vraie taille avec insertions

### Différences V1 vs V2:

| Aspect | V1 | V2 |
|--------|----|----|
| Insert mode | Émulé (difficile) | Natif (VirtualPosition) |
| Performance | Lente | Rapide (DrawingContext) |
| Visibility | Bordure verte seulement | Fond vert + bordure |
| Paste | Bugs avec insertions | Fonctionne correctement |

### Code Modifié (Aujourd'hui):
- `V2/ViewModels/HexEditorViewModel.cs:513-570` - Paste respecte EditMode
- `V2/Controls/HexViewport.cs:61-67` - Fond vert pour bytes insérés
- `V2/HexEditorV2.xaml.cs` - UpdateFileSizeDisplay() pour VirtualLength

---

*Dernière mise à jour: 2026-02-12*
*Phases 1-6: ✅ 100% complétées*
*Phase 7: ✅ 90% complétée*
*Phases 8-11: ⏳ En attente*
