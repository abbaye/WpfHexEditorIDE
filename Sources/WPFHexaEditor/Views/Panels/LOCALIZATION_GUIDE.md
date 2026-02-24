# Guide de localisation des panneaux Parsed Fields

## État actuel

### ✅ Complété
1. **Clés de ressources** (Properties/Resources.resx)
   - 140+ clés ajoutées pour les 5 panneaux
   - Organisation claire avec commentaires XML
   - Couverture complète de tous les textes UI

2. **Traduction française** (Properties/Resources.fr-FR.resx)
   - Traduction native complète de toutes les clés
   - Qualité professionnelle

3. **Script d'automatisation** (Tools/add_localizations.py)
   - Structure pour 17 langues restantes
   - Exemples Arabe et Allemand inclus

### 🔄 En cours / À faire

#### 1. Compléter les traductions (17 langues)

Langues restantes à traduire dans `add_localizations.py`:
- `es-419` (Espagnol - Amérique latine)
- `es-ES` (Espagnol - Espagne)
- `fr-CA` (Français - Canada)
- `hi-IN` (Hindi - Inde)
- `it-IT` (Italien - Italie)
- `ja-JP` (Japonais - Japon)
- `ko-KR` (Coréen - Corée)
- `nl-NL` (Néerlandais - Pays-Bas)
- `pl-PL` (Polonais - Pologne)
- `pt-BR` (Portugais - Brésil)
- `pt-PT` (Portugais - Portugal)
- `ru-RU` (Russe - Russie)
- `sv-SE` (Suédois - Suède)
- `tr-TR` (Turc - Turquie)
- `zh-CN` (Chinois - Chine)

**Comment ajouter une langue:**
1. Ouvrir `Tools/add_localizations.py`
2. Ajouter une entrée dans le dictionnaire `LANGUAGES`
3. Copier la structure depuis `'ar-SA'` ou `'de-DE'`
4. Traduire toutes les valeurs
5. Exécuter le script: `python Tools/add_localizations.py`

#### 2. Mettre à jour les fichiers XAML

Pour chaque panneau, remplacer les textes hardcodés par des bindings:

**Avant:**
```xml
<TextBlock Text="📊 Pattern Analysis" FontSize="14"/>
```

**Après:**
```xml
<TextBlock Text="{DynamicResource PatternAnalysis_Title}" FontSize="14"/>
```

**Fichiers à mettre à jour:**
- ✅ `PatternAnalysisPanel.xaml` - Pattern Analysis
- ✅ `FileStatisticsPanel.xaml` - File Statistics
- ✅ `ArchiveStructurePanel.xaml` - Archive Structure
- ✅ `FileComparisonPanel.xaml` - File Comparison
- ✅ `CustomParserTemplatePanel.xaml` - Custom Templates

**Pattern de remplacement:**
```xml
<!-- Headers avec émojis -->
<TextBlock Text="{DynamicResource PatternAnalysis_Title}"/>

<!-- Labels simples -->
<TextBlock Text="{DynamicResource PatternAnalysis_Entropy}"/>

<!-- Boutons -->
<Button Content="{DynamicResource ArchiveStructure_Extract}"/>

<!-- ToolTips -->
<ToolTip Content="{DynamicResource CustomTemplate_ExtensionsTooltip}"/>

<!-- Menu Items -->
<MenuItem Header="{DynamicResource ArchiveStructure_ViewDetails}"/>
```

#### 3. Mettre à jour les fichiers Code-Behind (C#)

Remplacer les MessageBox hardcodés par des ressources:

**Avant:**
```csharp
MessageBox.Show("Error loading file: " + ex.Message, "Error",
    MessageBoxButton.OK, MessageBoxImage.Error);
```

**Après:**
```csharp
using WpfHexaEditor.Properties;

MessageBox.Show(
    string.Format(Resources.FileComparison_ErrorLoading, ex.Message),
    Resources.FileComparison_Error,
    MessageBoxButton.OK,
    MessageBoxImage.Error
);
```

**Fichiers à mettre à jour:**
- ✅ `PatternAnalysisPanel.xaml.cs`
- ✅ `FileStatisticsPanel.xaml.cs`
- ✅ `ArchiveStructurePanel.xaml.cs`
- ✅ `FileComparisonPanel.xaml.cs`
- ✅ `CustomParserTemplatePanel.xaml.cs`

**Pattern de remplacement:**
```csharp
// Import
using WpfHexaEditor.Properties;

// Messages simples
MessageBox.Show(Resources.CustomTemplate_NoTemplateSelected,
    Resources.CustomTemplate_SaveTemplate,
    MessageBoxButton.OK,
    MessageBoxImage.Warning);

// Messages avec format
MessageBox.Show(
    string.Format(Resources.CustomTemplate_SavedSuccessfully, _currentTemplate.Name),
    Resources.CustomTemplate_SaveTemplate,
    MessageBoxButton.OK,
    MessageBoxImage.Information
);

// Dialogues avec confirmation
var result = MessageBox.Show(
    string.Format(Resources.CustomTemplate_DeleteConfirm, template.Name),
    Resources.CustomTemplate_ConfirmDelete,
    MessageBoxButton.YesNo,
    MessageBoxImage.Question
);
```

## Clés de ressources disponibles

### Pattern Analysis Panel
- `PatternAnalysis_Title`
- `PatternAnalysis_Entropy`
- `PatternAnalysis_ByteDistribution`
- `PatternAnalysis_DetectedPatterns`
- `PatternAnalysis_NoPatternsDetected`
- `PatternAnalysis_Anomalies`
- `PatternAnalysis_NoAnomalies`
- `PatternAnalysis_BitsPerByte`
- `PatternAnalysis_HighEntropy`
- `PatternAnalysis_HighEntropyDesc`
- `PatternAnalysis_LowEntropy`
- `PatternAnalysis_LowEntropyDesc`
- `PatternAnalysis_SkewedDistribution`
- `PatternAnalysis_SkewedDesc`
- `PatternAnalysis_NullBytes`
- `PatternAnalysis_RepeatedSequence`
- `PatternAnalysis_AsciiText`

### File Statistics Panel
- `FileStats_Title`
- `FileStats_OverallHealth`
- `FileStats_HealthyStructure`
- `FileStats_Structure`
- `FileStats_Valid`
- `FileStats_Invalid`
- `FileStats_Checksums`
- `FileStats_AllPass`
- `FileStats_SomeFailed`
- `FileStats_Compression`
- `FileStats_Ratio`
- `FileStats_SubOptimalCompression`
- `FileStats_EntropyAnalysis`
- `FileStats_Level`
- `FileStats_CompressedOrEncrypted`
- `FileStats_DetectedIssues`
- `FileStats_NoIssues`
- `FileStats_FileInformation`
- `FileStats_Size`
- `FileStats_Fields`
- `FileStats_Format`
- `FileStats_Unknown`

### Archive Structure Panel
- `ArchiveStructure_Title`
- `ArchiveStructure_NoArchive`
- `ArchiveStructure_Extract`
- `ArchiveStructure_ViewDetails`
- `ArchiveStructure_ExpandAll`
- `ArchiveStructure_CollapseAll`
- `ArchiveStructure_Files`
- `ArchiveStructure_Folders`
- `ArchiveStructure_TotalSize`
- `ArchiveStructure_CompressionRatio`
- `ArchiveStructure_ExtractFunctionality`
- `ArchiveStructure_ExtractTitle`
- `ArchiveStructure_DetailsTitle`
- `ArchiveStructure_DetailName`
- `ArchiveStructure_DetailType`
- `ArchiveStructure_DetailFolder`
- `ArchiveStructure_DetailFile`
- `ArchiveStructure_DetailCompressed`
- `ArchiveStructure_DetailCRC`
- `ArchiveStructure_DetailMethod`

### File Comparison Panel
- `FileComparison_Title`
- `FileComparison_SelectFiles`
- `FileComparison_LoadFile1`
- `FileComparison_LoadFile2`
- `FileComparison_File1`
- `FileComparison_File2`
- `FileComparison_Matching`
- `FileComparison_Added`
- `FileComparison_Modified`
- `FileComparison_Removed`
- `FileComparison_SelectFile1`
- `FileComparison_SelectFile2`
- `FileComparison_AllFiles`
- `FileComparison_ErrorLoading`
- `FileComparison_Error`
- `FileComparison_SelectBothFiles`
- `FileComparison_Comparing`

### Custom Parser Template Panel
- `CustomTemplate_Title`
- `CustomTemplate_NewTemplate`
- `CustomTemplate_DeleteTemplate`
- `CustomTemplate_EditorTitle`
- `CustomTemplate_Name`
- `CustomTemplate_Description`
- `CustomTemplate_Extensions`
- `CustomTemplate_ExtensionsTooltip`
- `CustomTemplate_SaveTemplate`
- `CustomTemplate_FormatBlocks`
- `CustomTemplate_AddBlock`
- `CustomTemplate_RemoveBlock`
- `CustomTemplate_ApplyToFile`
- `CustomTemplate_ApplyDescription`
- `CustomTemplate_ApplyButton`
- `CustomTemplate_ExportImport`
- `CustomTemplate_ExportJSON`
- `CustomTemplate_ImportJSON`
- `CustomTemplate_NoTemplateSelected`
- `CustomTemplate_SavedSuccessfully`
- `CustomTemplate_ErrorSaving`
- `CustomTemplate_SelectOrCreate`
- `CustomTemplate_AddBlockTitle`
- `CustomTemplate_DeleteConfirm`
- `CustomTemplate_ConfirmDelete`
- `CustomTemplate_ErrorDeleting`
- `CustomTemplate_DeleteError`
- `CustomTemplate_ApplyingTemplate`
- `CustomTemplate_ApplyTitle`
- `CustomTemplate_NoTemplateToApply`
- `CustomTemplate_ExportTitle`
- `CustomTemplate_JSONFiles`
- `CustomTemplate_ExportedTo`
- `CustomTemplate_ExportSuccess`
- `CustomTemplate_ErrorExporting`
- `CustomTemplate_ExportError`
- `CustomTemplate_ImportTitle`
- `CustomTemplate_ImportedSuccessfully`
- `CustomTemplate_ImportSuccess`
- `CustomTemplate_ErrorImporting`
- `CustomTemplate_ImportError`
- `CustomTemplate_ColumnName`
- `CustomTemplate_ColumnOffset`
- `CustomTemplate_ColumnLength`
- `CustomTemplate_ColumnType`
- `CustomTemplate_ColumnColor`
- `CustomTemplate_ColumnDescription`

## Test de la localisation

1. **Changer la langue de Windows** ou utiliser:
```csharp
System.Threading.Thread.CurrentThread.CurrentUICulture =
    new System.Globalization.CultureInfo("fr-FR");
```

2. **Vérifier que tous les textes changent** dynamiquement

3. **Vérifier les formats de chaînes** avec paramètres `{0}`

## Notes importantes

- Utiliser `DynamicResource` pour les TextBlocks/Labels (permet changement à chaud)
- Utiliser `Resources.ResourceManager.GetString()` en C# pour du texte dynamique
- Toujours tester avec des langues RTL (arabe) pour vérifier la mise en page
- Les émojis dans les clés sont OK pour tous les systèmes modernes

## Estimation du travail restant

- ⏱️ Traductions (15 langues): ~10-15 heures (avec services de traduction)
- ⏱️ Mise à jour XAML (5 fichiers): ~3-4 heures
- ⏱️ Mise à jour code-behind (5 fichiers): ~2-3 heures
- ⏱️ Tests et corrections: ~2-3 heures

**Total: ~17-25 heures de travail**
