# Plan d'Amélioration Complet - ByteSize/ByteOrder Functionality

## État Actuel (Phase 1-6 Complétées)

### ✅ Fonctionnalités Implémentées
- **Phase 1**: ByteData model étendu (Values[], GetHexText(), CellWidth)
- **Phase 2**: ViewModel groupement bytes par stride
- **Phase 3**: HexViewport rendering adapté
- **Phase 4**: Column headers stride-based
- **Phase 5**: DependencyProperty callbacks TwoWay
- **Phase 6**: Hit testing et separator positioning

### ✅ Ce Qui Fonctionne
- Display Bit8/16/32 avec largeurs cellules dynamiques
- ByteOrder (LoHi/HiLo) avec inversion bytes
- Settings panel TwoWay binding
- Click detection avec cellules variables
- Column headers affichent offsets corrects (00, 02, 04...)

---

## 🐛 Bugs Identifiés à Corriger

### Bug 1: Positionnement Imprécis ⚠️ PRIORITÉ HAUTE
**Symptôme**: Clicks ou sélections pas exactement alignés avec cellules visuelles

**Causes Potentielles**:
1. **ByteSpacers**: Hit testing ne compte pas correctement les spacers en mode multi-byte
2. **Partial groups**: Fin de fichier avec groupes incomplets (ex: 15 bytes en Bit16 = 7 groupes complets + 1 byte isolé)
3. **DPI scaling**: CellWidth calculé peut différer du rendu réel si DPI ≠ 96

**Plan d'Investigation**:
```
1. Tester avec fichier aligné (16, 32, 64 bytes) → Si fonctionne, problème = partial groups
2. Tester sans ByteSpacers → Si fonctionne, problème = spacer calculation
3. Vérifier DPI avec VisualTreeHelper.GetDpi() → Si ≠ 96, ajuster CellWidth
```

**Solution Proposée**:
- Ajouter logging détaillé dans HitTestByteWithArea()
- Vérifier calcul hexX accumulation vs mouse position
- Ajuster CellWidth pour partial groups (dernier groupe < stride bytes)

---

### Bug 2: ASCII Area Alignment 🔧 PRIORITÉ MOYENNE
**Symptôme**: Separator peut être mal positionné si lignes partielles

**Cause**: Separator calculé avec `numCells = (_bytesPerLine + stride - 1) / stride` (ceiling)
Mais ligne partielle a moins de cells → separator trop à droite

**Solution**:
```csharp
// Au lieu de calculer pour ligne complète, utiliser hexX réel de la ligne
double separatorX = hexX + 4; // hexX = end of actual hex content
```

---

### Bug 3: Selection Rendering en Multi-Byte 🎨 PRIORITÉ BASSE
**Symptôme**: Sélection peut ne pas couvrir tout le groupe multi-byte visuellement

**Cause**: DrawHexByte() dessine selection background avec `rect = new Rect(x, y, byteWidth, _lineHeight)`
Mais si selection couvre partie d'un groupe, le background ne s'étend pas sur le groupe entier

**Solution Future**: Phase 8 - Snap Selection to Byte Groups

---

### Bug 4: CellWidth Statique - Pas de Support Font/DPI ⚠️ PRIORITÉ HAUTE
**Symptôme**: CellWidth hardcodé ne s'adapte pas aux changements de police ou DPI

**Problème Actuel**:
```csharp
public double CellWidth
{
    get
    {
        return Values.Length switch
        {
            1 => 24,   // HARDCODÉ en pixels!
            2 => 52,   // Ne prend PAS en compte:
            3 => 79,   //   - FontSize (peut être 10, 12, 14, 16...)
            4 => 106,  //   - FontFamily (Consolas vs Courier)
            _ => 24    //   - DPI scaling (96, 120, 144 DPI)
        };
    }
}
```

**Conséquences**:
1. **FontSize changé dans settings**: Texte "ABCD" déborde ou ne remplit pas la cellule
2. **Font changée**: Largeurs différentes (Consolas ≠ Courier ≠ Lucida Console)
3. **DPI scaling**: Sur écrans haute résolution (150%, 200%), cellules mal dimensionnées
4. **Misalignment**: Hit testing décalé car cellule width ≠ texte width réel

**Exemple du Problème**:
```
FontSize = 12px, Consolas:
  Text "ABCD" width réelle = 48px (mesurée)
  CellWidth retourné = 52px ✅ OK (petit padding)

FontSize = 16px, Consolas:
  Text "ABCD" width réelle = 64px (mesurée)
  CellWidth retourné = 52px ❌ DÉBORDEMENT!

FontSize = 10px, Courier New:
  Text "ABCD" width réelle = 40px (mesurée)
  CellWidth retourné = 52px ❌ TROP D'ESPACE!
```

**Solution Proposée**: CellWidth Dynamique avec FormattedText

#### Approche 1: Mesure à la Volée (Simple mais coûteux)
```csharp
// Dans ByteData.cs - Ajouter dépendances rendering
public class ByteData
{
    // Nouveau: Contexte de rendu pour calcul dynamique
    private Typeface _typeface;
    private double _fontSize;
    private double _dpi;

    public void SetRenderingContext(Typeface typeface, double fontSize, double dpi)
    {
        _typeface = typeface;
        _fontSize = fontSize;
        _dpi = dpi;
    }

    public double CellWidth
    {
        get
        {
            if (_typeface == null)
                return GetStaticCellWidth(); // Fallback

            // Mesurer largeur réelle du texte hex
            string hexText = GetHexText();
            var formattedText = new FormattedText(
                hexText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.Black,
                _dpi
            );

            // Ajouter petit padding (2-4px)
            return formattedText.Width + 4;
        }
    }

    private double GetStaticCellWidth()
    {
        // Fallback actuel si contexte pas initialisé
        return Values.Length switch
        {
            1 => 24,
            2 => 52,
            3 => 79,
            4 => 106,
            _ => 24
        };
    }
}
```

**Inconvénient**: Créer FormattedText à chaque accès CellWidth = coûteux (10-100µs/cellule × milliers de cellules)

#### Approche 2: Cache avec Invalidation (Recommandé)
```csharp
// Dans HexViewport.cs - Cache global des largeurs
private Dictionary<(int byteCount, double fontSize, string fontFamily), double> _cellWidthCache = new();

private double CalculateCellWidth(int byteCount)
{
    var key = (byteCount, _fontSize, _typeface.FontFamily.Source);

    if (_cellWidthCache.TryGetValue(key, out double cachedWidth))
        return cachedWidth;

    // Mesurer avec FormattedText
    string sampleText = new string('F', byteCount * 2); // "FF", "FFFF", "FFFFFF"...
    var formattedText = new FormattedText(
        sampleText,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        _typeface,
        _fontSize,
        Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip
    );

    double width = formattedText.Width + 4; // +4px padding
    _cellWidthCache[key] = width;
    return width;
}

// Invalider cache quand font change
private void OnFontChanged()
{
    _cellWidthCache.Clear();
    InvalidateVisual();
}
```

**Avantage**: Cache réduit coût à ~1-4 mesures par session (Bit8, Bit16, Bit32 partial/full)

#### Approche 3: Pre-Calculate dans ViewModel (Optimal)
```csharp
// Dans HexEditorViewModel.cs
private double _cachedCellWidth_1Byte;
private double _cachedCellWidth_2Bytes;
private double _cachedCellWidth_3Bytes;
private double _cachedCellWidth_4Bytes;

public void UpdateCellWidthCache(Typeface typeface, double fontSize, double dpi)
{
    _cachedCellWidth_1Byte = MeasureHexTextWidth("FF", typeface, fontSize, dpi) + 4;
    _cachedCellWidth_2Bytes = MeasureHexTextWidth("FFFF", typeface, fontSize, dpi) + 4;
    _cachedCellWidth_3Bytes = MeasureHexTextWidth("FFFFFF", typeface, fontSize, dpi) + 4;
    _cachedCellWidth_4Bytes = MeasureHexTextWidth("FFFFFFFF", typeface, fontSize, dpi) + 4;

    // Forcer refresh de toutes les lignes
    ClearLineCache();
    RefreshVisibleLines();
}

private double MeasureHexTextWidth(string text, Typeface typeface, double fontSize, double dpi)
{
    var formattedText = new FormattedText(
        text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        typeface, fontSize, Brushes.Black, dpi
    );
    return formattedText.Width;
}

// Dans ByteData.cs - Utiliser cache du ViewModel
public double GetCellWidth(HexEditorViewModel viewModel)
{
    return Values.Length switch
    {
        1 => viewModel._cachedCellWidth_1Byte,
        2 => viewModel._cachedCellWidth_2Bytes,
        3 => viewModel._cachedCellWidth_3Bytes,
        4 => viewModel._cachedCellWidth_4Bytes,
        _ => viewModel._cachedCellWidth_1Byte
    };
}
```

**Avantage**: Calcul une seule fois par changement de font, accès ultra-rapide

**Complexité**: ByteData doit avoir référence au ViewModel (ou passer en paramètre)

#### Solution Recommandée: Approche 2 (Cache dans HexViewport)

**Raison**:
- ✅ Bon compromis performance/simplicité
- ✅ HexViewport a déjà _typeface, _fontSize, _dpi
- ✅ Pas besoin de modifier ByteData (reste POCO)
- ✅ Cache automatique invalidé si font change

**Implémentation**:

1. **HexViewport.cs** - Ajouter méthode de calcul dynamique:
```csharp
private double GetDynamicCellWidth(ByteData byteData)
{
    return CalculateCellWidth(byteData.Values.Length);
}
```

2. **Remplacer tous les usages de `byteData.CellWidth`** par `GetDynamicCellWidth(byteData)`:
   - OnRender() ligne ~870: `hexX += GetDynamicCellWidth(byteData) + HexByteSpacing;`
   - HitTestByteWithArea() ligne ~1753: `double byteHitWidth = GetDynamicCellWidth(byteData) + HexByteSpacing;`
   - RefreshColumnHeader() ligne ~881: Utiliser cache pour headers

3. **Invalider cache** quand FontSize/FontFamily change:
```csharp
private void OnFontSizeChanged()
{
    _cellWidthCache.Clear();
    InvalidateVisual();
}
```

**Impact Performance**:
- **Avant**: 0 calculs (hardcodé)
- **Après**: ~4 calculs par session (1 par byte count, mis en cache)
- **Overhead**: Négligeable (<1ms total)

**Testing**:
1. Ouvrir fichier, changer FontSize 10 → 16 → 20 → vérifier cellules s'adaptent
2. Changer FontFamily Consolas → Courier → vérifier largeurs ajustées
3. DPI 100% → 150% → 200% → vérifier scaling correct
4. Bit16 partial group (1 byte) → vérifier CellWidth = width de 2 chars, pas 4

---

## 📋 Phases Futures - Roadmap Complète

---

## Phase 7: Édition Multi-Byte (FUTURE) 🔒

### Problématique
**Actuellement**: Édition désactivée en mode Bit16/32 (trop complexe pour Phase 1)

**Défis**:
1. Éditer "ABCD" (Bit16) nécessite gérer 4 caractères hex au lieu de 2
2. ByteOrder complique l'édition: éditer byte 0 ou byte 1 du groupe?
3. Curseur doit se déplacer par nibble (0.5 byte) ou par byte?

### Architecture Proposée

#### A. Édition en Mode "Group-Level"
**Concept**: L'utilisateur édite le groupe entier comme une unité, pas byte par byte

```
User voit: "ABCD" (Bit16, LoHi)
User tape: "1234" → Groupe devient 0x12, 0x34
ByteOrder HiLo: "1234" → Groupe devient 0x34, 0x12 (inversé)
```

**Avantages**:
- Simple conceptuellement
- ByteOrder appliqué automatiquement
- Pas de confusion byte 0 vs byte 1

**Inconvénients**:
- Impossible d'éditer un seul byte du groupe
- Pas flexible pour modifications partielles

#### B. Édition en Mode "Byte-Level" avec Visual Feedback
**Concept**: L'utilisateur édite byte par byte, mais avec indication visuelle du byte actif

```
Groupe: [AB][CD] (Bit16, 2 bytes)
Cursor sur byte 0: [AB] surligné, édite "AB"
Cursor sur byte 1: [CD] surligné, édite "CD"
Arrow keys: Déplace entre bytes du groupe
```

**Avantages**:
- Flexibilité maximale
- Compatible avec édition byte-à-byte existante

**Inconvénients**:
- Plus complexe à implémenter
- ByteOrder peut confondre l'utilisateur (éditer byte "logique" 0 ≠ byte "visuel" 0 si HiLo)

#### C. Solution Recommandée: Hybrid Mode
**Phase 7.1**: Édition Group-Level (simple, rapide à implémenter)
**Phase 7.2**: Édition Byte-Level optionnelle (avancée, pour power users)

**Implémentation Phase 7.1**:
```csharp
// Dans EditOperations.cs
public void ModifyByteGroup(VirtualPosition groupStart, byte[] newValues, ByteOrderType order)
{
    if (order == ByteOrderType.HiLo)
        newValues = newValues.Reverse().ToArray();

    for (int i = 0; i < newValues.Length; i++)
        Provider.WriteByte(groupStart.Value + i, newValues[i]);
}
```

**Keyboard Input Handling**:
```csharp
// Dans HexViewport.OnKeyDown pour mode Bit16
if (ByteSize == ByteSizeType.Bit16)
{
    if (_hexInputBuffer.Length >= 4) // 4 chars = 2 bytes
    {
        byte[] bytes = ParseHexString(_hexInputBuffer); // "ABCD" → [0xAB, 0xCD]
        ModifyByteGroup(cursorPos, bytes, ByteOrder);
        _hexInputBuffer = "";
        MoveCursorToNextGroup();
    }
}
```

**Désactivation Temporaire**:
```csharp
// Dans ViewModel ou HexEditor
public bool CanEdit => ByteSize == ByteSizeType.Bit8;
```

---

## Phase 8: Selection Snap to Byte Groups 📍

### Problématique
**Actuellement**: Sélection se fait au byte près, même en mode multi-byte
**Confusion**: Sélectionner bytes 0-1 en Bit16 devrait sélectionner le groupe entier automatiquement

### Comportements Souhaités

#### Option A: Snap Automatique (Recommandé)
```
User clique sur byte 2 (début de groupe 1 en Bit16)
→ Sélection snap à bytes 2-3 (groupe complet)

User shift-click sur byte 5 (milieu de groupe 2)
→ Sélection s'étend jusqu'à byte 7 (fin de groupe 2)
```

#### Option B: Snap Optionnel avec Modifier Key
```
Ctrl+Click: Snap to group
Click normal: Select precise byte (pour édition byte-level)
```

### Implémentation
```csharp
// Dans SelectionService
public (VirtualPosition Start, VirtualPosition Stop) SnapSelectionToGroups(
    VirtualPosition start, VirtualPosition stop, int stride)
{
    long snappedStart = (start.Value / stride) * stride;
    long snappedStop = ((stop.Value / stride) + 1) * stride - 1;
    return (new VirtualPosition(snappedStart), new VirtualPosition(snappedStop));
}
```

---

## Phase 9: Copy/Paste Multi-Byte 📋

### Problématique
**Copy**: Copier en mode Bit16/32 devrait-il copier bytes bruts ou groupes formatés?
**Paste**: Coller "ABCD1234" en Bit32 devrait créer 1 groupe ou 4 bytes individuels?

### Solutions Proposées

#### Copy Modes
```
Mode 1: Raw Bytes (ByteOrder ignoré)
  Copy bytes [0xAB, 0xCD, 0xEF, 0x12] → "ABCDEF12"

Mode 2: Visual Groups (ByteOrder appliqué)
  Bit16 LoHi: bytes [0xAB, 0xCD, 0xEF, 0x12] → "ABCD EF12" (2 groupes)
  Bit16 HiLo: bytes [0xAB, 0xCD, 0xEF, 0x12] → "CDAB 12EF" (inversé)

Mode 3: Structured Format (avec métadonnées)
  "Bit16-LoHi: ABCD EF12" (peut être re-parsed avec contexte)
```

#### Paste Modes
```
Smart Paste: Détecte format et adapte
  - Si input = 4 chars et Bit16 → Crée 1 groupe de 2 bytes
  - Si input = 8 chars et Bit32 → Crée 1 groupe de 4 bytes
  - Si input = 10 chars et Bit16 → Crée 2 groupes + 1 byte isolé
```

### Implémentation
```csharp
// ClipboardService extension
public byte[] ParseClipboardContent(string hexString, ByteSizeType byteSize, ByteOrderType order)
{
    byte[] rawBytes = HexStringToBytes(hexString);

    if (order == ByteOrderType.HiLo)
    {
        // Reverse chaque groupe selon stride
        int stride = GetStride(byteSize);
        for (int i = 0; i < rawBytes.Length; i += stride)
        {
            int groupSize = Math.Min(stride, rawBytes.Length - i);
            Array.Reverse(rawBytes, i, groupSize);
        }
    }

    return rawBytes;
}
```

---

## Phase 10: Search Multi-Byte 🔍

### Problématique
**Recherche hex**: Chercher "ABCD" devrait-il trouver bytes séquentiels [0xAB, 0xCD] ou groupe Bit16?
**ByteOrder**: Avec HiLo, chercher "ABCD" devrait aussi trouver [0xCD, 0xAB]?

### Architecture de Recherche

#### Mode 1: Byte Sequence Search (Actuel)
```
Search "ABCD" → Trouve [0xAB, 0xCD] n'importe où (ignore ByteSize)
```

#### Mode 2: Group-Aware Search
```
Bit16 LoHi: Search "ABCD" → Trouve groupe [0xAB, 0xCD] uniquement si aligné sur groupe
Bit16 HiLo: Search "ABCD" → Trouve groupe [0xCD, 0xAB] (inversé)
Checkbox: "Search aligned groups only"
```

#### Mode 3: Endian-Agnostic Search
```
Search "ABCD" avec "Match any endianness" → Trouve:
  - [0xAB, 0xCD] (LoHi)
  - [0xCD, 0xAB] (HiLo)
```

---

## Phase 11: Compatibilité TBL 📖

### Problématique Critique
**TBL (Character Table)**: Mapping bytes → caractères (ex: 0x01 → "A", 0x0203 → "あ")
**Conflit**: TBL peut avoir encodages multi-byte (DTE/MTE) qui chevauchent avec ByteSize!

### Cas de Conflit

#### Scénario 1: TBL Multi-Byte + ByteSize=Bit8
```
TBL: 0x01 02 → "あ" (2-byte character)
ByteSize: Bit8 (chaque byte affiché séparément)

Question: Afficher "01 02" (hex) ou "あ" (TBL) dans ASCII panel?
```

**Solution**: TBL s'applique uniquement à ASCII panel, ByteSize uniquement à hex panel
```
Hex panel: [01][02] (Bit8, 2 cellules)
ASCII panel: [あ] (TBL, 1 caractère de 2 bytes)
```

#### Scénario 2: TBL + ByteSize=Bit16
```
TBL: 0x01 02 → "あ" (2-byte character)
ByteSize: Bit16 (groupes de 2 bytes)

Hex panel: [0102] (1 cellule de 2 bytes)
ASCII panel: [あ] (1 caractère de 2 bytes)
→ Alignment parfait! ✅
```

**Recommandation**: Bit16 + TBL 2-byte est le mode optimal pour ROM hacking

#### Scénario 3: TBL + ByteSize=Bit32 Misalignment
```
TBL: 0x01 02 → "あ" (2-byte character)
ByteSize: Bit32 (groupes de 4 bytes)

Hex panel: [01020304] (1 cellule de 4 bytes)
ASCII panel: [あ][??] (TBL decode 2 bytes, reste 2 bytes non-alignés)
→ ⚠️ Problème d'alignment
```

**Solution**: Warning si ByteSize stride ≠ TBL max byte length
```csharp
if (TblLoaded && ByteSize != ByteSizeType.Bit8)
{
    int tblMaxBytes = TblStream.GetMaxByteLength();
    int stride = GetStride(ByteSize);

    if (stride != tblMaxBytes)
        ShowWarning($"TBL uses {tblMaxBytes}-byte chars, but ByteSize is Bit{stride*8}. " +
                    "Consider using Bit{tblMaxBytes*8} for alignment.");
}
```

### Architecture Proposée

#### Double Mode: TBL vs ByteSize
```
Setting: "ASCII Rendering Mode"
  - Option 1: "TBL Character Encoding" (ignore ByteSize for ASCII panel)
  - Option 2: "Hex Mirror Mode" (ASCII shows same grouping as hex)

Mode 1 (TBL Priority):
  Hex: [01][02][03] (Bit8)
  ASCII: [あ][.] (TBL decode, 0x0102="あ", 0x03='.')

Mode 2 (Hex Mirror):
  Hex: [0102][03??] (Bit16)
  ASCII: [..][.] (chaque groupe hex → 1 char ASCII)
```

#### Auto-Detect Optimal ByteSize
```csharp
public ByteSizeType SuggestByteSizeForTbl(TBLStream tbl)
{
    int maxBytes = tbl.GetMaxByteLength();
    return maxBytes switch
    {
        1 => ByteSizeType.Bit8,
        2 => ByteSizeType.Bit16,
        3 or 4 => ByteSizeType.Bit32,
        _ => ByteSizeType.Bit8 // Fallback
    };
}
```

**UI**: Bouton "Auto-Adjust ByteSize for TBL" dans settings panel

---

## Phase 12: Bookmark & Highlight Multi-Byte 🔖

### Problématique
**Bookmarks**: Bookmark sur byte 2 en Bit16 devrait-il marquer le byte ou le groupe?
**Highlights**: Search result highlighting doit s'étendre sur tout le groupe

### Solution
```csharp
// Dans CustomBackgroundBlock
public class CustomBackgroundBlock
{
    public long StartOffset { get; set; }
    public long StopOffset { get; set; }  // Inclusive
    public Brush Color { get; set; }

    // Phase 12: Add alignment flag
    public bool SnapToGroups { get; set; } = true;
}

// Lors de l'ajout d'un bookmark
public void AddBookmark(long position, int stride, bool snapToGroups)
{
    if (snapToGroups)
    {
        long groupStart = (position / stride) * stride;
        long groupStop = groupStart + stride - 1;
        position = groupStart;
    }

    // ... ajouter bookmark
}
```

---

## Phase 13: Undo/Redo Multi-Byte 🔄

### Problématique
**Undo**: Undo d'une édition de groupe Bit16 doit restaurer les 2 bytes atomiquement
**Redo**: Redo doit aussi réappliquer ByteOrder

### Solution
```csharp
// ByteProvider undo system déjà supporte multi-byte edits
// Mais besoin de wrapper pour groupes

public class MultiByteEdit
{
    public long GroupStart { get; set; }
    public byte[] OldValues { get; set; }
    public byte[] NewValues { get; set; }
    public ByteOrderType ByteOrder { get; set; }
}

// Apply edit atomiquement
public void ApplyMultiByteEdit(MultiByteEdit edit)
{
    var values = (edit.ByteOrder == ByteOrderType.HiLo)
        ? edit.NewValues.Reverse().ToArray()
        : edit.NewValues;

    for (int i = 0; i < values.Length; i++)
        Provider.WriteByte(edit.GroupStart + i, values[i]);
}
```

---

## Phase 14: Performance Optimizations ⚡

### Profiling Nécessaire
**Hypothèse**: Bit32 mode pourrait être plus lent (moins de ByteData à render mais calculs plus complexes)

### Optimisations Proposées

#### 1. Cache CellWidth calculation
```csharp
// Au lieu de calculer à chaque frame
public double CellWidth => ByteSize switch { ... };

// Cacher dans field
private double _cellWidth;
public double CellWidth => _cellWidth;

// Recalculer uniquement si ByteSize change
```

#### 2. Batch ByteOrder reversal
```csharp
// Actuellement: Reverse() pour chaque ByteData.GetHexText()
// Optimisation: Reverse toute la ligne une fois

byte[] lineBytes = _provider.GetBytes(startPos, lineLength);
if (ByteOrder == ByteOrderType.HiLo)
{
    // Reverse tous les groupes en une fois
    ReverseByteGroups(lineBytes, stride);
}
```

#### 3. FormattedText pooling
```csharp
// Réutiliser FormattedText objects pour hex strings fréquents
private Dictionary<string, FormattedText> _formattedTextCache = new();
```

---

## Phase 15: UI/UX Improvements 🎨

### 1. Visual Indicators
```
Bit8: Cellules normales [AB][CD][EF]
Bit16: Cellules groupées avec barre visuelle [AB|CD][EF|12]
Bit32: Cellules encore plus larges [AB|CD|EF|12]
```

### 2. Status Bar Info
```
"Mode: Bit16 LoHi | Position: 0x0004 (Group 2, Byte 0)"
```

### 3. Context Menu
```
Right-click sur groupe:
  - Edit Group (opens dialog)
  - Copy Group as Hex
  - Bookmark Group
  - Convert to Bit8/16/32
```

### 4. Keyboard Shortcuts
```
Ctrl+1: Switch to Bit8
Ctrl+2: Switch to Bit16
Ctrl+3: Switch to Bit32
Ctrl+E: Toggle ByteOrder (LoHi ↔ HiLo)
```

---

## 🚀 Ordre d'Implémentation Recommandé

### Sprint 1 (Bugs Critiques) - 1.5 semaines
1. ✅ Fix ByteOrder not updating display (DONE - commit 03e1314)
2. ✅ Fix ByteSpacer positioning in multi-byte modes (DONE - commit b33bd8a)
3. ✅ Fix partial group CellWidth (DONE - commit 723c97e)
4. 🔧 **NEXT**: Implement dynamic CellWidth with Font/DPI support (Bug 4 - PRIORITÉ HAUTE)
   - Replace hardcoded pixel values with FormattedText measurements
   - Add cache in HexViewport to avoid performance hit
   - Invalidate cache on FontSize/FontFamily/DPI changes
   - Test with different fonts (Consolas, Courier, sizes 10-20)

### Sprint 2 (Édition) - 2 semaines
4. Phase 7.1: Édition Group-Level (disable Bit16/32 for now, add UI message)
5. Phase 7.2: Keyboard input handling pour Bit16
6. Phase 7.3: Keyboard input handling pour Bit32

### Sprint 3 (Sélection & Clipboard) - 1 semaine
7. Phase 8: Selection snap to groups
8. Phase 9: Copy/Paste multi-byte

### Sprint 4 (Search & TBL) - 2 semaines
9. Phase 10: Search multi-byte modes
10. Phase 11: TBL compatibility (warning system, auto-suggest)

### Sprint 5 (Polish & Performance) - 1 semaine
11. Phase 12: Bookmark alignment
12. Phase 13: Undo/Redo verification
13. Phase 14: Performance profiling + optimizations
14. Phase 15: UI/UX improvements

**Total Estimé**: 7 semaines (35 heures)

---

## 🧪 Plan de Tests

### Tests Unitaires à Ajouter
```csharp
[TestClass]
public class ByteSizeTests
{
    [TestMethod]
    public void ByteData_GetHexText_Bit16_LoHi()
    {
        var byteData = new ByteData
        {
            Values = new byte[] { 0xAB, 0xCD },
            ByteSize = ByteSizeType.Bit16,
            ByteOrder = ByteOrderType.LoHi
        };
        Assert.AreEqual("ABCD", byteData.GetHexText());
    }

    [TestMethod]
    public void ByteData_GetHexText_Bit16_HiLo()
    {
        var byteData = new ByteData
        {
            Values = new byte[] { 0xAB, 0xCD },
            ByteSize = ByteSizeType.Bit16,
            ByteOrder = ByteOrderType.HiLo
        };
        Assert.AreEqual("CDAB", byteData.GetHexText());
    }

    [TestMethod]
    public void HitTest_Bit16_ClickInSecondHalfOfCell()
    {
        // Click at x=40px (in 52px cell) should select byte at position
        // Not throw or select wrong byte
    }
}
```

### Tests Manuels Critiques
- [ ] Ouvrir fichier 1KB, passer Bit8 → Bit16 → Bit32, vérifier affichage
- [ ] Changer ByteOrder en Bit16, vérifier inversion bytes
- [ ] Cliquer sur chaque cellule en Bit16/32, vérifier sélection correcte
- [ ] Fin de fichier impair (15 bytes en Bit16), vérifier partial group
- [ ] Charger TBL 2-byte, passer en Bit16, vérifier alignment
- [ ] Scroll rapide en Bit32, vérifier performance

---

## 📝 Notes de Documentation

### Pour README.md
```markdown
## ByteSize Modes

WpfHexEditor supports three byte display modes:

- **Bit8** (default): Each byte displayed individually (e.g., `AB CD EF 12`)
- **Bit16**: Bytes grouped by 2 (e.g., `ABCD EF12`)
- **Bit32**: Bytes grouped by 4 (e.g., `ABCDEF12`)

### Byte Order

- **LoHi** (Little Endian): Bytes displayed in file order
- **HiLo** (Big Endian): Bytes within groups are reversed

Example: Bytes `[0xAB, 0xCD]` in Bit16 mode
- LoHi: Displays as `ABCD`
- HiLo: Displays as `CDAB`

### TBL Compatibility

When loading a TBL file with multi-byte character encodings, consider setting
ByteSize to match the TBL encoding:
- 1-byte TBL → Use Bit8
- 2-byte TBL → Use Bit16 (optimal alignment)
- 3-4 byte TBL → Use Bit32
```

---

## ⚠️ Limitations Connues

1. **CellWidth Statique**: Largeur des cellules hardcodée, ne s'adapte PAS aux changements de FontSize/FontFamily/DPI (Bug 4 - PRIORITÉ HAUTE)
   - Impact: Débordement de texte ou espace excessif si police changée
   - Solution: Implémenter calcul dynamique avec FormattedText + cache (voir Bug 4)

2. **Édition**: Bit16/32 edit currently disabled (Phase 7)
   - Impact: Impossible de modifier bytes en mode multi-byte
   - Solution future: Édition group-level ou byte-level

3. **TBL Misalignment**: Warning shown but not prevented (Phase 11)
   - Impact: Confusion si TBL 2-byte avec ByteSize=Bit32
   - Solution future: Auto-suggest ByteSize optimal pour TBL

4. **Partial Groups**: End-of-file groups render correctly but may have visual quirks (Sprint 1 - RÉSOLU)
   - Status: CellWidth dynamique implémenté (commit 723c97e) ✅

5. **Performance**: Large files (>100MB) in Bit32 not profiled yet (Phase 14)
   - Impact: Potentielle lenteur non mesurée
   - Solution future: Profiling + optimizations (cache, pooling)

---

## 🔮 Idées Futures (Hors Scope)

- **Bit64 mode**: Pour fichiers 64-bit (rare usage)
- **Custom stride**: Allow user-defined byte groupings (e.g., 3, 5, 6 bytes)
- **Mixed mode**: Different ByteSize per line (complex, low value)
- **Vertical ByteOrder**: Reverse bytes vertically across lines (niche)
