# 🚀 Plan de Migration V2 → 100% Compatible Legacy

## Vue d'ensemble

**Objectif** : Rendre HexEditor V2 100% rétrocompatible avec HexEditorLegacy
**Membres à migrer** : 187 (92 méthodes, 93 propriétés, 1 événement modifié)
**Approche** : Migration par phases avec tests unitaires

---

## PHASE 1 : Fondations - Récupération de Données ⭐ CRITIQUE

**Durée estimée** : 2-3 jours
**Priorité** : CRITIQUE
**Dépendances** : Aucune

### Objectif
Restaurer les méthodes de base pour lire et accéder aux données du ByteProvider.
Ces méthodes sont **prérequises** pour toutes les autres phases.

### Méthodes à implémenter (6)

```csharp
// 1. HexEditor.xaml.cs - Ajouter ces méthodes publiques

/// <summary>
/// Retrieves a single byte at the specified position
/// </summary>
/// <param name="position">Position in stream</param>
/// <param name="copyChange">Include modifications in result</param>
/// <returns>Tuple with byte value and success flag</returns>
public (byte? singleByte, bool success) GetByte(long position, bool copyChange = true)
{
    if (!CheckIsOpen(_byteProvider)) return (null, false);

    try
    {
        var virtualPos = new VirtualPosition(position);
        var result = _byteProvider.GetByte(virtualPos);
        return (result, true);
    }
    catch
    {
        return (null, false);
    }
}

/// <summary>
/// Gets all bytes modified of the specified action
/// </summary>
public IDictionary<long, ByteModified> GetByteModifieds(ByteAction act)
{
    if (!CheckIsOpen(_byteProvider)) return new Dictionary<long, ByteModified>();

    // Accès à EditsManager via ByteProvider
    return _byteProvider.EditsManager.GetByteModifieds(act);
}

/// <summary>
/// Gets current selection as byte array
/// </summary>
public byte[] GetSelectionByteArray()
{
    if (SelectionLength <= 0) return Array.Empty<byte>();
    return GetCopyData(SelectionStart, SelectionStop, true);
}

/// <summary>
/// Gets all bytes from file/stream
/// </summary>
public byte[] GetAllBytes(bool copyChange = true)
{
    if (!CheckIsOpen(_byteProvider)) return Array.Empty<byte>();
    return GetCopyData(0, Length - 1, copyChange);
}

/// <summary>
/// Gets all bytes (overload)
/// </summary>
public byte[] GetAllBytes() => GetAllBytes(true);

/// <summary>
/// Gets byte data for copy operation
/// </summary>
public byte[] GetCopyData(long selectionStart, long selectionStop, bool copyChange)
{
    if (!CheckIsOpen(_byteProvider)) return Array.Empty<byte>();

    long length = selectionStop - selectionStart + 1;
    if (length <= 0) return Array.Empty<byte>();

    var result = new byte[length];
    for (long i = 0; i < length; i++)
    {
        var pos = new VirtualPosition(selectionStart + i);
        result[i] = _byteProvider.GetByte(pos);
    }

    return result;
}
```

### Modifications ByteProvider nécessaires

Si `EditsManager` n'est pas accessible publiquement, ajouter :

```csharp
// ByteProvider.cs - Exposer EditsManager
public EditsManager EditsManager { get; private set; }

// Ou ajouter méthode wrapper
public IDictionary<long, ByteModified> GetByteModifieds(ByteAction act)
{
    return _editsManager.GetByteModifieds(act);
}
```

### Tests à créer

```csharp
[TestClass]
public class Phase1_DataRetrievalTests
{
    [TestMethod]
    public void GetByte_ValidPosition_ReturnsCorrectByte()
    {
        // Arrange
        var editor = new HexEditor();
        editor.Stream = CreateTestStream();

        // Act
        var (byte, success) = editor.GetByte(0);

        // Assert
        Assert.IsTrue(success);
        Assert.IsNotNull(byte);
    }

    [TestMethod]
    public void GetAllBytes_WithModifications_ReturnsModifiedData()
    {
        // Test avec modifications
    }

    [TestMethod]
    public void GetSelectionByteArray_ReturnsSelectedBytes()
    {
        // Test sélection
    }
}
```

### Critères de réussite
- ✅ Les 6 méthodes fonctionnent
- ✅ Tests unitaires passent (100% coverage)
- ✅ Aucune régression sur fonctionnalités V2 existantes

---

## PHASE 2 : Gestion de la Sélection & Navigation ⭐ HAUTE

**Durée estimée** : 2-3 jours
**Priorité** : HAUTE
**Dépendances** : Phase 1

### Objectif
Restaurer le contrôle programmatique de la sélection et de la navigation.

### Méthodes à implémenter (8 + 4 utilitaires)

```csharp
// Selection Management

/// <summary>
/// Deselects all bytes
/// </summary>
public void UnSelectAll(bool cleanFocus = false)
{
    SelectionStart = 0;
    SelectionStop = 0;

    if (cleanFocus)
    {
        // Clear focus from all byte controls
        _hexViewport?.ClearFocus();
    }
}

/// <summary>
/// Selects all bytes in file
/// </summary>
public void SelectAll()
{
    if (!CheckIsOpen(_byteProvider)) return;

    SelectionStart = 0;
    SelectionStop = Length - 1;
}

/// <summary>
/// Sets cursor position with byte length selection
/// </summary>
public void SetPosition(long position, long byteLength)
{
    if (!CheckIsOpen(_byteProvider)) return;

    SelectionStart = position;
    if (byteLength > 0)
    {
        SelectionStop = position + byteLength - 1;
    }
    else
    {
        SelectionStop = position;
    }

    // Scroll to position
    ScrollToPosition(position);
}

/// <summary>
/// Sets cursor position
/// </summary>
public void SetPosition(long position) => SetPosition(position, 0);

/// <summary>
/// Sets cursor position from hex string
/// </summary>
public void SetPosition(string hexLiteralPosition)
{
    if (long.TryParse(hexLiteralPosition,
        System.Globalization.NumberStyles.HexNumber,
        null, out long position))
    {
        SetPosition(position);
    }
}

/// <summary>
/// Sets cursor position from hex string with selection length
/// </summary>
public void SetPosition(string hexLiteralPosition, long byteLength)
{
    if (long.TryParse(hexLiteralPosition,
        System.Globalization.NumberStyles.HexNumber,
        null, out long position))
    {
        SetPosition(position, byteLength);
    }
}

/// <summary>
/// Sets focus at selection start position
/// </summary>
public void SetFocusAtSelectionStart()
{
    SetFocusAt(SelectionStart);
}

/// <summary>
/// Reverses byte order within selection
/// </summary>
public void ReverseSelection()
{
    if (SelectionLength <= 1) return;

    var bytes = GetSelectionByteArray();
    Array.Reverse(bytes);

    // Replace selection with reversed bytes
    long pos = SelectionStart;
    foreach (var b in bytes)
    {
        _byteProvider.ReplaceByte(new VirtualPosition(pos++), b);
    }
}

// Utility Methods

/// <summary>
/// Gets line number for byte position
/// </summary>
public long GetLineNumber(long position)
{
    return position / BytePerLine;
}

/// <summary>
/// Gets column number for byte position
/// </summary>
public long GetColumnNumber(long position)
{
    return position % BytePerLine;
}

/// <summary>
/// Checks if byte position is visible in viewport
/// </summary>
public bool IsBytePositionAreVisible(long bytePosition)
{
    long firstVisibleLine = _viewModel.ScrollPosition;
    long lastVisibleLine = firstVisibleLine + _viewModel.VisibleLines;
    long byteLine = GetLineNumber(bytePosition);

    return byteLine >= firstVisibleLine && byteLine <= lastVisibleLine;
}

/// <summary>
/// Updates focus on control
/// </summary>
public void UpdateFocus()
{
    _hexViewport?.Focus();
}
```

### Propriété calculée à ajouter

```csharp
/// <summary>
/// Returns whether selection start is visible
/// </summary>
public bool SelectionStartIsVisible => IsBytePositionAreVisible(SelectionStart);
```

### Tests Phase 2

```csharp
[TestClass]
public class Phase2_SelectionNavigationTests
{
    [TestMethod]
    public void SelectAll_SelectsEntireFile()
    {
        var editor = CreateEditorWithData(1000);
        editor.SelectAll();
        Assert.AreEqual(1000, editor.SelectionLength);
    }

    [TestMethod]
    public void SetPosition_HexString_SetsCorrectPosition()
    {
        var editor = CreateEditor();
        editor.SetPosition("FF");
        Assert.AreEqual(255, editor.SelectionStart);
    }

    [TestMethod]
    public void ReverseSelection_ReversesBytes()
    {
        // Test reverse
    }
}
```

---

## PHASE 3 : Modification Directe des Bytes ⭐ CRITIQUE

**Durée estimée** : 3-4 jours
**Priorité** : CRITIQUE
**Dépendances** : Phase 1, Phase 2

### Objectif
Permettre la modification programmatique complète des bytes.

### Méthodes à implémenter (8)

```csharp
/// <summary>
/// Modifies byte at position with undo support
/// </summary>
public void ModifyByte(byte? @byte, long bytePositionInStream, long undoLength = 1)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode) return;

    var pos = new VirtualPosition(bytePositionInStream);

    if (@byte.HasValue)
    {
        _byteProvider.ReplaceByte(pos, @byte.Value);
    }
    else
    {
        // null = delete byte
        _byteProvider.DeleteByte(pos);
    }

    RefreshViewport();
}

/// <summary>
/// Inserts single byte at position
/// </summary>
public void InsertByte(byte @byte, long bytePositionInStream)
{
    InsertByte(@byte, bytePositionInStream, 1);
}

/// <summary>
/// Inserts byte multiple times at position
/// </summary>
public void InsertByte(byte @byte, long bytePositionInStream, long length)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode || !AllowByteInsertion) return;

    var pos = new VirtualPosition(bytePositionInStream);

    for (long i = 0; i < length; i++)
    {
        _byteProvider.InsertByte(pos, @byte);
    }

    RefreshViewport();
}

/// <summary>
/// Inserts byte array at position
/// </summary>
public void InsertBytes(byte[] bytes, long bytePositionInStream)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode || !AllowByteInsertion) return;
    if (bytes == null || bytes.Length == 0) return;

    var pos = new VirtualPosition(bytePositionInStream);

    foreach (var b in bytes)
    {
        _byteProvider.InsertByte(pos, b);
        pos = new VirtualPosition(pos.Value + 1);
    }

    RefreshViewport();
}

/// <summary>
/// Deletes bytes at specified position
/// </summary>
public void DeleteBytesAtPosition(long bytePositionInStream, long length = 1)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode) return;

    for (long i = 0; i < length; i++)
    {
        var pos = new VirtualPosition(bytePositionInStream);
        _byteProvider.DeleteByte(pos);
    }

    RefreshViewport();
}

/// <summary>
/// Deletes currently selected bytes
/// </summary>
public void DeleteSelection()
{
    if (SelectionLength <= 0) return;
    DeleteBytesAtPosition(SelectionStart, SelectionLength);
}

/// <summary>
/// Fills selection with specified byte value
/// </summary>
public void FillWithByte(byte val)
{
    FillWithByte(SelectionStart, SelectionLength, val);
}

/// <summary>
/// Fills range with specified byte value
/// </summary>
public void FillWithByte(long startPosition, long length, byte val)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode) return;

    for (long i = 0; i < length; i++)
    {
        var pos = new VirtualPosition(startPosition + i);
        _byteProvider.ReplaceByte(pos, val);
    }

    RefreshViewport();

    // Raise event
    FillWithByteCompleted?.Invoke(this, EventArgs.Empty);
}
```

### Service Helper nécessaire

```csharp
// ByteModificationService.cs - Étendre si nécessaire
public class ByteModificationService
{
    public void ModifyByteWithUndo(ByteProvider provider, VirtualPosition pos, byte? value)
    {
        // Implementation avec support undo
    }
}
```

### Tests Phase 3

```csharp
[TestClass]
public class Phase3_ByteModificationTests
{
    [TestMethod]
    public void InsertByte_InsertsCorrectly()
    {
        var editor = CreateEditor();
        long originalLength = editor.Length;
        editor.InsertByte(0xFF, 0);
        Assert.AreEqual(originalLength + 1, editor.Length);
    }

    [TestMethod]
    public void FillWithByte_FillsRange()
    {
        var editor = CreateEditor();
        editor.FillWithByte(0, 10, 0xAA);

        for (int i = 0; i < 10; i++)
        {
            var (b, success) = editor.GetByte(i);
            Assert.AreEqual(0xAA, b);
        }
    }
}
```

---

## PHASE 4 : Recherche & Remplacement ⭐ CRITIQUE

**Durée estimée** : 4-5 jours
**Priorité** : CRITIQUE
**Dépendances** : Phase 1, Phase 3

### Objectif
Restaurer toutes les fonctionnalités de recherche et remplacement (38 méthodes!).

### Architecture

```
HexEditor (Public API)
    ↓ Délègue à
FindReplaceService (existant V2)
    ↓ Utilise
LRU Cache + SIMD optimizations (déjà présent)
```

### Méthodes Find (11)

```csharp
// Find Operations - Wrapper autour de FindReplaceService

/// <summary>
/// Finds first occurrence of text starting from position
/// </summary>
public long FindFirst(string text, long startPosition = 0)
{
    return FindFirst(StringToByteArray(text), startPosition, false);
}

/// <summary>
/// Finds first occurrence of byte array
/// </summary>
public long FindFirst(byte[] data, long startPosition = 0, bool highLight = false)
{
    if (data == null || data.Length == 0) return -1;

    var result = _findReplaceService.FindFirst(
        _byteProvider,
        data,
        new VirtualPosition(startPosition)
    );

    if (result.HasValue && highLight)
    {
        AddHighLight(result.Value.Value, data.Length);
    }

    return result?.Value ?? -1;
}

/// <summary>
/// Finds next occurrence from current selection
/// </summary>
public long FindNext(string text)
{
    return FindNext(StringToByteArray(text), false);
}

/// <summary>
/// Finds next occurrence of byte array
/// </summary>
public long FindNext(byte[] data, bool highLight = false)
{
    long startPos = SelectionStart + 1;
    return FindFirst(data, startPos, highLight);
}

/// <summary>
/// Finds last occurrence of text
/// </summary>
public long FindLast(string text)
{
    return FindLast(StringToByteArray(text), false);
}

/// <summary>
/// Finds last occurrence of byte array
/// </summary>
public long FindLast(byte[] data, bool highLight = false)
{
    if (data == null || data.Length == 0) return -1;

    var allResults = FindAll(data, false);
    var lastResult = allResults.LastOrDefault();

    if (lastResult > 0 && highLight)
    {
        AddHighLight(lastResult, data.Length);
    }

    return lastResult > 0 ? lastResult : -1;
}

/// <summary>
/// Finds all occurrences of text
/// </summary>
public IEnumerable<long> FindAll(string text)
{
    return FindAll(StringToByteArray(text), false);
}

/// <summary>
/// Finds all occurrences of byte array
/// </summary>
public IEnumerable<long> FindAll(byte[] data)
{
    return FindAll(data, false);
}

/// <summary>
/// Finds all occurrences with highlight option
/// </summary>
public IEnumerable<long> FindAll(string text, bool highLight)
{
    return FindAll(StringToByteArray(text), highLight);
}

/// <summary>
/// Finds all occurrences with highlight option
/// </summary>
public IEnumerable<long> FindAll(byte[] data, bool highLight)
{
    if (data == null || data.Length == 0) return Enumerable.Empty<long>();

    // Utilise FindReplaceService avec cache LRU
    var results = _findReplaceService.FindAllCachedOptimized(
        _byteProvider,
        data
    );

    if (highLight && results.Any())
    {
        foreach (var pos in results)
        {
            AddHighLight(pos.Value, data.Length, false);
        }
        RefreshViewport();
    }

    return results.Select(vp => vp.Value);
}

/// <summary>
/// Finds all occurrences of current selection
/// </summary>
public IEnumerable<long> FindAllSelection(bool highLight)
{
    var selectionBytes = GetSelectionByteArray();
    return FindAll(selectionBytes, highLight);
}

// Helper
private byte[] StringToByteArray(string text)
{
    return System.Text.Encoding.Default.GetBytes(text);
}
```

### Méthodes Replace (27)

```csharp
// Replace Operations

/// <summary>
/// Replaces all occurrences of original byte with replace byte in selection
/// </summary>
public void ReplaceByte(byte original, byte replace)
{
    ReplaceByte(SelectionStart, SelectionLength, original, replace);
}

/// <summary>
/// Replaces byte within specific range
/// </summary>
public void ReplaceByte(long startPosition, long length, byte original, byte replace)
{
    if (!CheckIsOpen(_byteProvider)) return;
    if (ReadOnlyMode) return;

    for (long i = 0; i < length; i++)
    {
        var pos = new VirtualPosition(startPosition + i);
        var (currentByte, success) = GetByte(pos.Value);

        if (success && currentByte == original)
        {
            _byteProvider.ReplaceByte(pos, replace);
        }
    }

    RefreshViewport();
    ReplaceByteCompleted?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Replaces first occurrence of byte array
/// </summary>
public long ReplaceFirst(byte[] findData, byte[] replaceData, long startPosition = 0, bool highlight = false)
{
    return ReplaceFirst(findData, replaceData, true, startPosition, highlight);
}

/// <summary>
/// Replaces first occurrence with truck length option
/// </summary>
public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true,
                         long startPosition = 0, bool highlight = false)
{
    var foundPos = FindFirst(findData, startPosition, false);

    if (foundPos < 0) return -1;

    PerformReplace(foundPos, findData.Length, replaceData, truckLength);

    if (highlight)
    {
        AddHighLight(foundPos, replaceData.Length);
    }

    return foundPos;
}

// Surcharges ReplaceFirst (5 autres variantes)
public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true, bool highlight = false)
    => ReplaceFirst(findData, replaceData, truckLength, 0, highlight);

public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength = true)
    => ReplaceFirst(findData, replaceData, truckLength, 0, false);

public long ReplaceFirst(byte[] findData, byte[] replaceData)
    => ReplaceFirst(findData, replaceData, true, 0, false);

public long ReplaceFirst(string find, string replace, bool truckLength = true, bool highlight = false)
    => ReplaceFirst(StringToByteArray(find), StringToByteArray(replace), truckLength, 0, highlight);

public long ReplaceFirst(string find, string replace, bool truckLength = true)
    => ReplaceFirst(StringToByteArray(find), StringToByteArray(replace), truckLength, 0, false);

public long ReplaceFirst(string find, string replace)
    => ReplaceFirst(StringToByteArray(find), StringToByteArray(replace), true, 0, false);

/// <summary>
/// Replaces next occurrence
/// </summary>
public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength = true, bool highlight = false)
{
    long startPos = SelectionStart + 1;
    return ReplaceFirst(findData, replaceData, truckLength, startPos, highlight);
}

// Surcharges ReplaceNext (5 autres variantes)
public long ReplaceNext(byte[] findData, byte[] replaceData)
    => ReplaceNext(findData, replaceData, true, false);

public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength = true)
    => ReplaceNext(findData, replaceData, truckLength, false);

public long ReplaceNext(string find, string replace, bool truckLength = true, bool highlight = false)
    => ReplaceNext(StringToByteArray(find), StringToByteArray(replace), truckLength, highlight);

public long ReplaceNext(string find, string replace)
    => ReplaceNext(StringToByteArray(find), StringToByteArray(replace), true, false);

public long ReplaceNext(string find, string replace, bool truckLength = true)
    => ReplaceNext(StringToByteArray(find), StringToByteArray(replace), truckLength, false);

/// <summary>
/// Replaces all occurrences
/// </summary>
public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData,
                                   bool truckLength = true, bool highlight = false)
{
    var positions = FindAll(findData, false).ToList();
    var replacedPositions = new List<long>();

    // Replace from end to start to maintain positions
    for (int i = positions.Count - 1; i >= 0; i--)
    {
        long pos = positions[i];
        PerformReplace(pos, findData.Length, replaceData, truckLength);
        replacedPositions.Add(pos);
    }

    if (highlight)
    {
        foreach (var pos in replacedPositions)
        {
            AddHighLight(pos, replaceData.Length, false);
        }
        RefreshViewport();
    }

    replacedPositions.Reverse();
    return replacedPositions;
}

// Surcharges ReplaceAll (4 autres variantes)
public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData, bool truckLength = true)
    => ReplaceAll(findData, replaceData, truckLength, false);

public IEnumerable<long> ReplaceAll(string find, string replace, bool truckLength = true, bool highlight = false)
    => ReplaceAll(StringToByteArray(find), StringToByteArray(replace), truckLength, highlight);

public IEnumerable<long> ReplaceAll(string find, string replace)
    => ReplaceAll(StringToByteArray(find), StringToByteArray(replace), true, false);

public IEnumerable<long> ReplaceAll(string find, string replace, bool truckLength = true)
    => ReplaceAll(StringToByteArray(find), StringToByteArray(replace), truckLength, false);

// Helper pour effectuer le remplacement
private void PerformReplace(long position, int findLength, byte[] replaceData, bool truckLength)
{
    if (truckLength && findLength == replaceData.Length)
    {
        // Simple replacement, same length
        for (int i = 0; i < replaceData.Length; i++)
        {
            var pos = new VirtualPosition(position + i);
            _byteProvider.ReplaceByte(pos, replaceData[i]);
        }
    }
    else if (truckLength)
    {
        // Different lengths with trucking
        int minLength = Math.Min(findLength, replaceData.Length);

        // Replace overlapping part
        for (int i = 0; i < minLength; i++)
        {
            var pos = new VirtualPosition(position + i);
            _byteProvider.ReplaceByte(pos, replaceData[i]);
        }

        // Handle extra
        if (replaceData.Length > findLength)
        {
            // Insert extra bytes
            for (int i = minLength; i < replaceData.Length; i++)
            {
                var pos = new VirtualPosition(position + i);
                _byteProvider.InsertByte(pos, replaceData[i]);
            }
        }
        else if (findLength > replaceData.Length)
        {
            // Delete extra bytes
            int toDelete = findLength - replaceData.Length;
            for (int i = 0; i < toDelete; i++)
            {
                var pos = new VirtualPosition(position + replaceData.Length);
                _byteProvider.DeleteByte(pos);
            }
        }
    }
    else
    {
        // Non-truck: delete old, insert new
        DeleteBytesAtPosition(position, findLength);
        InsertBytes(replaceData, position);
    }
}
```

### Tests Phase 4

```csharp
[TestClass]
public class Phase4_FindReplaceTests
{
    [TestMethod]
    public void FindFirst_FindsCorrectPosition()
    {
        var editor = CreateEditorWithPattern();
        long pos = editor.FindFirst(new byte[] { 0xAA, 0xBB });
        Assert.AreEqual(10, pos); // Expected position
    }

    [TestMethod]
    public void ReplaceAll_ReplacesAllOccurrences()
    {
        var editor = CreateEditor();
        var positions = editor.ReplaceAll(
            new byte[] { 0x00 },
            new byte[] { 0xFF }
        ).ToList();

        Assert.IsTrue(positions.Count > 0);

        // Verify all replaced
        foreach (var pos in positions)
        {
            var (b, _) = editor.GetByte(pos);
            Assert.AreEqual(0xFF, b);
        }
    }
}
```

---

## PHASE 5 : Signets, Surlignages & Custom Backgrounds ⭐ MOYENNE

**Durée estimée** : 2-3 jours
**Priorité** : MOYENNE
**Dépendances** : Phase 2

### Méthodes Signets (5)

```csharp
// Bookmark Management

private readonly BookmarkService _bookmarkService;

/// <summary>
/// Gets all bookmarks
/// </summary>
public IEnumerable<BookMark> BookMarks => _bookmarkService.GetAllBookmarks();

/// <summary>
/// Sets bookmark at specified position
/// </summary>
public void SetBookMark(long position)
{
    _bookmarkService.AddBookmark(_byteProvider, new VirtualPosition(position));
    RefreshViewport();
}

/// <summary>
/// Sets bookmark at current selection start
/// </summary>
public void SetBookMark()
{
    SetBookMark(SelectionStart);
}

/// <summary>
/// Clears all scroll markers
/// </summary>
public void ClearAllScrollMarker()
{
    _bookmarkService.ClearAllBookmarks(_byteProvider);
    RefreshViewport();
}

/// <summary>
/// Clears specific type of scroll marker
/// </summary>
public void ClearScrollMarker(ScrollMarker marker)
{
    _bookmarkService.ClearScrollMarker(_byteProvider, marker);
    RefreshViewport();
}

/// <summary>
/// Clears scroll marker at position
/// </summary>
public void ClearScrollMarker(ScrollMarker marker, long position)
{
    _bookmarkService.ClearScrollMarkerAtPosition(
        _byteProvider,
        marker,
        new VirtualPosition(position)
    );
    RefreshViewport();
}

/// <summary>
/// Clears all markers at position
/// </summary>
public void ClearScrollMarker(long position)
{
    foreach (ScrollMarker marker in Enum.GetValues(typeof(ScrollMarker)))
    {
        ClearScrollMarker(marker, position);
    }
}
```

### Méthodes Surlignages (3)

```csharp
// Highlight Management

private readonly HighlightService _highlightService;

/// <summary>
/// Removes all highlights
/// </summary>
public void UnHighLightAll()
{
    _highlightService.ClearAllHighlights(_byteProvider);
    RefreshViewport();
}

/// <summary>
/// Adds highlight at position
/// </summary>
public void AddHighLight(long startPosition, long length, bool updateVisual = true)
{
    _highlightService.AddHighlight(
        _byteProvider,
        new VirtualPosition(startPosition),
        length
    );

    if (updateVisual)
    {
        RefreshViewport();
    }
}

/// <summary>
/// Removes highlight at position
/// </summary>
public void RemoveHighLight(long startPosition, long length, bool updateVisual = true)
{
    _highlightService.RemoveHighlight(
        _byteProvider,
        new VirtualPosition(startPosition),
        length
    );

    if (updateVisual)
    {
        RefreshViewport();
    }
}
```

### Propriété Highlight

```csharp
/// <summary>
/// Highlights the header/offset at selection start
/// </summary>
public bool HighLightSelectionStart { get; set; } = false;
```

### Custom Background Blocks (3)

```csharp
// Custom Background Management

/// <summary>
/// Gets list of custom background blocks
/// </summary>
public List<CustomBackgroundBlock> CustomBackgroundBlockItems
{
    get => _customBackgroundBlocks?.ToList() ?? new List<CustomBackgroundBlock>();
}

/// <summary>
/// Gets custom background block at position
/// </summary>
public CustomBackgroundBlock GetCustomBackgroundBlock(long position)
{
    return _customBackgroundBlocks?
        .FirstOrDefault(block =>
            position >= block.StartOffset &&
            position < block.StartOffset + block.Length);
}

/// <summary>
/// Clears all custom background blocks
/// </summary>
public void ClearCustomBackgroundBlock()
{
    _customBackgroundBlocks?.Clear();
    RefreshViewport();
}
```

---

## PHASE 6 : Opérations Presse-Papiers & Fichiers ⭐ MOYENNE

**Durée estimée** : 2-3 jours
**Priorité** : MOYENNE
**Dépendances** : Phase 1, Phase 2

### Opérations Clipboard (5)

```csharp
// Clipboard Operations

/// <summary>
/// Copies to clipboard using default mode
/// </summary>
public void CopyToClipboard()
{
    CopyToClipboard(DefaultCopyToClipboardMode);
}

/// <summary>
/// Copies to clipboard with specified mode
/// </summary>
public void CopyToClipboard(CopyPasteMode copypastemode)
{
    CopyToClipboard(
        copypastemode,
        SelectionStart,
        SelectionStop,
        true,
        _tblStream
    );
}

/// <summary>
/// Copies to clipboard with full options
/// </summary>
public void CopyToClipboard(CopyPasteMode copypastemode, long selectionStart,
                           long selectionStop, bool copyChange, TblStream tbl)
{
    var data = GetCopyData(selectionStart, selectionStop, copyChange);

    string clipboardText = copypastemode switch
    {
        CopyPasteMode.HexaString => BytesToHexString(data),
        CopyPasteMode.AsciiString => BytesToAsciiString(data),
        CopyPasteMode.TblString => BytesToTblString(data, tbl),
        CopyPasteMode.CSharpCode => BytesToCSharpCode(data),
        CopyPasteMode.CCode => BytesToCCode(data),
        CopyPasteMode.JavaCode => BytesToJavaCode(data),
        _ => BytesToHexString(data)
    };

    Clipboard.SetText(clipboardText);
    DataCopied?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Copies selection to output stream
/// </summary>
public void CopyToStream(Stream output, bool copyChange)
{
    CopyToStream(output, SelectionStart, SelectionStop, copyChange);
}

/// <summary>
/// Copies range to output stream
/// </summary>
public void CopyToStream(Stream output, long selectionStart, long selectionStop, bool copyChange)
{
    if (output == null || !output.CanWrite) return;

    var data = GetCopyData(selectionStart, selectionStop, copyChange);
    output.Write(data, 0, data.Length);
}

// Helpers for format conversion
private string BytesToHexString(byte[] data)
{
    return BitConverter.ToString(data).Replace("-", " ");
}

private string BytesToAsciiString(byte[] data)
{
    return Encoding.ASCII.GetString(data);
}

// ... autres helpers de conversion
```

### Propriétés Clipboard

```csharp
/// <summary>
/// Returns whether copy operation is possible
/// </summary>
public bool CanCopy => SelectionLength > 0 && CheckIsOpen(_byteProvider);

/// <summary>
/// Returns whether deletion is possible
/// </summary>
public bool CanDelete => SelectionLength > 0 &&
                         CheckIsOpen(_byteProvider) &&
                         !ReadOnlyMode &&
                         AllowDeleteByte;

/// <summary>
/// Default copy to clipboard mode
/// </summary>
public CopyPasteMode DefaultCopyToClipboardMode { get; set; } = CopyPasteMode.HexaString;
```

### Opérations Fichiers (4)

```csharp
// File Operations

/// <summary>
/// Closes current provider
/// </summary>
public void CloseProvider(bool clearFileName = true)
{
    if (_byteProvider != null)
    {
        _byteProvider.Dispose();
        _byteProvider = null;
    }

    if (clearFileName)
    {
        FileName = string.Empty;
    }

    FileClosed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Submits changes to current file
/// </summary>
public void SubmitChanges()
{
    if (string.IsNullOrEmpty(FileName))
    {
        throw new InvalidOperationException("No filename specified");
    }

    SubmitChanges(FileName, true);
}

/// <summary>
/// Submits changes to new file
/// </summary>
public void SubmitChanges(string newfilename, bool overwrite = false)
{
    if (!CheckIsOpen(_byteProvider)) return;

    if (File.Exists(newfilename) && !overwrite)
    {
        throw new IOException($"File already exists: {newfilename}");
    }

    _byteProvider.SubmitChanges(newfilename);

    FileName = newfilename;
    ChangesSubmited?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Clears all modification history
/// </summary>
public void ClearAllChange()
{
    if (!CheckIsOpen(_byteProvider)) return;

    _byteProvider.EditsManager.ClearAllEdits();
    _byteProvider.UndoRedoManager.ClearHistory();

    RefreshViewport();
}
```

### Propriété

```csharp
/// <summary>
/// Returns whether file is locked by another process
/// </summary>
public bool IsLockedFile => CheckIsOpen(_byteProvider) && _byteProvider.IsLockedFile;
```

---

## PHASE 7 : Propriétés UI & Fonctions Avancées ⭐ BASSE

**Durée estimée** : 3-4 jours
**Priorité** : BASSE
**Dépendances** : Toutes phases précédentes

### DependencyProperties UI (60+ propriétés)

```csharp
// Boolean Control Properties (27)

public static readonly DependencyProperty AllowBuildinCtrlcProperty =
    DependencyProperty.Register(nameof(AllowBuildinCtrlc), typeof(bool),
        typeof(HexEditor), new PropertyMetadata(true));

public bool AllowBuildinCtrlc
{
    get => (bool)GetValue(AllowBuildinCtrlcProperty);
    set => SetValue(AllowBuildinCtrlcProperty, value);
}

public static readonly DependencyProperty AllowBuildinCtrlvProperty =
    DependencyProperty.Register(nameof(AllowBuildinCtrlv), typeof(bool),
        typeof(HexEditor), new PropertyMetadata(true));

public bool AllowBuildinCtrlv
{
    get => (bool)GetValue(AllowBuildinCtrlvProperty);
    set => SetValue(AllowBuildinCtrlvProperty, value);
}

// ... 25 autres propriétés booléennes similaires

// Brush Properties (18)

public static readonly DependencyProperty SelectionFirstColorProperty =
    DependencyProperty.Register(nameof(SelectionFirstColor), typeof(Brush),
        typeof(HexEditor), new PropertyMetadata(Brushes.Blue));

public Brush SelectionFirstColor
{
    get => (Brush)GetValue(SelectionFirstColorProperty);
    set => SetValue(SelectionFirstColorProperty, value);
}

// ... 17 autres propriétés Brush similaires

// Numeric Properties (9)

public static readonly DependencyProperty LineHeightProperty =
    DependencyProperty.Register(nameof(LineHeight), typeof(double),
        typeof(HexEditor), new PropertyMetadata(20.0));

public double LineHeight
{
    get => (double)GetValue(LineHeightProperty);
    set => SetValue(LineHeightProperty, value);
}

// ... 8 autres propriétés numériques

// Enum Properties (14)

public static readonly DependencyProperty OffSetStringVisualProperty =
    DependencyProperty.Register(nameof(OffSetStringVisual),
        typeof(DataVisualType), typeof(HexEditor),
        new PropertyMetadata(DataVisualType.Hexadecimal));

public DataVisualType OffSetStringVisual
{
    get => (DataVisualType)GetValue(OffSetStringVisualProperty);
    set => SetValue(OffSetStringVisualProperty, value);
}

// ... 13 autres propriétés enum

// Visibility Properties (6)

public static readonly DependencyProperty HexDataVisibilityProperty =
    DependencyProperty.Register(nameof(HexDataVisibility),
        typeof(Visibility), typeof(HexEditor),
        new PropertyMetadata(Visibility.Visible));

public Visibility HexDataVisibility
{
    get => (Visibility)GetValue(HexDataVisibilityProperty);
    set => SetValue(HexDataVisibilityProperty, value);
}

// ... 5 autres propriétés Visibility
```

### Stack Properties Undo/Redo

```csharp
/// <summary>
/// Gets undo operation stack
/// </summary>
public Stack<ByteModified> UndoStack
{
    get
    {
        if (!CheckIsOpen(_byteProvider)) return new Stack<ByteModified>();
        return _byteProvider.UndoRedoManager.GetUndoStack();
    }
}

/// <summary>
/// Gets redo operation stack
/// </summary>
public Stack<ByteModified> RedoStack
{
    get
    {
        if (!CheckIsOpen(_byteProvider)) return new Stack<ByteModified>();
        return _byteProvider.UndoRedoManager.GetRedoStack();
    }
}

/// <summary>
/// Undoes multiple operations
/// </summary>
public void Undo(int repeat = 1)
{
    for (int i = 0; i < repeat; i++)
    {
        Undo();
    }
}

/// <summary>
/// Redoes multiple operations
/// </summary>
public void Redo(int repeat = 1)
{
    for (int i = 0; i < repeat; i++)
    {
        Redo();
    }
}
```

### État/Sérialisation (2)

```csharp
/// <summary>
/// Saves current state to XML file
/// </summary>
public void SaveCurrentState(string filename)
{
    var state = new XDocument(
        new XElement("HexEditorState",
            new XElement("FileName", FileName),
            new XElement("SelectionStart", SelectionStart),
            new XElement("SelectionStop", SelectionStop),
            new XElement("Position", Position),
            new XElement("BytePerLine", BytePerLine),
            new XElement("ReadOnlyMode", ReadOnlyMode)
            // ... autres propriétés d'état
        )
    );

    state.Save(filename);
}

/// <summary>
/// Loads state from XML file
/// </summary>
public void LoadCurrentState(string filename)
{
    if (!File.Exists(filename)) return;

    var state = XDocument.Load(filename);
    var root = state.Root;

    FileName = root.Element("FileName")?.Value;
    SelectionStart = long.Parse(root.Element("SelectionStart")?.Value ?? "0");
    SelectionStop = long.Parse(root.Element("SelectionStop")?.Value ?? "0");
    // ... restaurer autres propriétés
}

/// <summary>
/// Current state as XDocument
/// </summary>
public XDocument CurrentState
{
    get
    {
        return new XDocument(
            new XElement("HexEditorState",
                // Build state XML
            )
        );
    }
}
```

### Comparaison (2)

```csharp
/// <summary>
/// Compares with another ByteProvider
/// </summary>
public IEnumerable<ByteDifference> Compare(ByteProviderLegacy providerToCompare,
                                           bool compareChange = false)
{
    if (!CheckIsOpen(_byteProvider) || providerToCompare == null)
        return Enumerable.Empty<ByteDifference>();

    return _comparisonService.Compare(_byteProvider, providerToCompare, compareChange);
}

/// <summary>
/// Compares with another HexEditor
/// </summary>
public IEnumerable<ByteDifference> Compare(HexEditorLegacy hexeditor,
                                           bool compareChange = false)
{
    if (hexeditor?._provider == null)
        return Enumerable.Empty<ByteDifference>();

    return Compare(hexeditor._provider, compareChange);
}
```

### Contrôle UI (6)

```csharp
/// <summary>
/// Updates visual properties for all byte controls
/// </summary>
public void UpdateVisual()
{
    RefreshViewport();
}

/// <summary>
/// Refreshes entire view
/// </summary>
public void RefreshView(bool controlResize = false, bool refreshData = true)
{
    if (controlResize)
    {
        // Recalculate layout
        InvalidateMeasure();
        InvalidateArrange();
    }

    if (refreshData)
    {
        _viewModel.RefreshVisibleLines();
    }

    RefreshViewport();
}

/// <summary>
/// Forces preloading of bytes
/// </summary>
public void ForcePreloadByteInEditor(PreloadByteInEditor preloadByte, bool refreshView = true)
{
    // Implémentation selon l'enum PreloadByteInEditor
    if (refreshView)
    {
        RefreshView();
    }
}

/// <summary>
/// Forces preloading of specified number of lines
/// </summary>
public void ForcePreloadByteInEditor(int nbLine, bool refreshView = true)
{
    // Précharger nbLine lignes
    if (refreshView)
    {
        RefreshView();
    }
}

/// <summary>
/// Resets zoom to 1.0
/// </summary>
public void ResetZoom()
{
    ZoomScale = 1.0;
}

/// <summary>
/// Implements IDisposable
/// </summary>
public void Dispose()
{
    CloseProvider();

    // Dispose services
    _findReplaceService?.Dispose();
    _bookmarkService?.Dispose();

    GC.SuppressFinalize(this);
}
```

### Command Properties (2)

```csharp
/// <summary>
/// Command for refreshing view
/// </summary>
public ICommand RefreshViewCommand => new RelayCommand(
    () => RefreshView(),
    () => CheckIsOpen(_byteProvider)
);

/// <summary>
/// Command for submitting changes
/// </summary>
public ICommand SubmitChangesCommand => new RelayCommand(
    () => SubmitChanges(),
    () => CheckIsOpen(_byteProvider) && !ReadOnlyMode && IsModified
);
```

### Propriétés calculées

```csharp
/// <summary>
/// Maximum number of lines preloaded
/// </summary>
public int MaxLinePreloaded => _viewModel.VisibleLines + 10;

/// <summary>
/// Selection line number
/// </summary>
public long SelectionLine => GetLineNumber(SelectionStart);

/// <summary>
/// Selection as string
/// </summary>
public string SelectionString
{
    get
    {
        var bytes = GetSelectionByteArray();
        return Encoding.Default.GetString(bytes);
    }
}

/// <summary>
/// Selection as hex string
/// </summary>
public string SelectionHex
{
    get
    {
        var bytes = GetSelectionByteArray();
        return BitConverter.ToString(bytes).Replace("-", " ");
    }
}

/// <summary>
/// Visual byte address stop
/// </summary>
public long VisualByteAdressStop => VisualByteAdressStart + VisualByteAdressLength;
```

---

## 📊 RÉSUMÉ DU PLAN

### Chronologie

| Phase | Durée | Dépendances | Priorité |
|-------|-------|-------------|----------|
| Phase 1 : Récupération Données | 2-3 jours | Aucune | ⭐⭐⭐ CRITIQUE |
| Phase 2 : Sélection & Navigation | 2-3 jours | Phase 1 | ⭐⭐⭐ HAUTE |
| Phase 3 : Modification Bytes | 3-4 jours | Phase 1, 2 | ⭐⭐⭐ CRITIQUE |
| Phase 4 : Recherche & Remplacement | 4-5 jours | Phase 1, 3 | ⭐⭐⭐ CRITIQUE |
| Phase 5 : Signets & Surlignages | 2-3 jours | Phase 2 | ⭐⭐ MOYENNE |
| Phase 6 : Clipboard & Fichiers | 2-3 jours | Phase 1, 2 | ⭐⭐ MOYENNE |
| Phase 7 : Propriétés UI Avancées | 3-4 jours | Toutes | ⭐ BASSE |

**Durée totale estimée** : **19-25 jours** (3.8-5 semaines)

### Répartition des 187 membres

| Catégorie | Nombre | Phases concernées |
|-----------|--------|-------------------|
| Méthodes | 92 | Phases 1-7 |
| Propriétés | 93 | Phases 1, 2, 5, 6, 7 |
| Événements | 1 | Phase 7 |
| **TOTAL** | **187** | |

### Approche Recommandée

1. **Séquentielle pour Phases 1-4** (critiques, dépendances fortes)
2. **Parallèle pour Phases 5-7** (peuvent être développées simultanément)

---

## ✅ CRITÈRES DE SUCCÈS GLOBAUX

### Technique
- ✅ 187 membres publics restaurés
- ✅ 100% des tests unitaires passent
- ✅ Aucune régression V2
- ✅ Architecture MVVM préservée
- ✅ Performance maintenue ou améliorée

### Qualité
- ✅ Code coverage > 80%
- ✅ Documentation XML complète
- ✅ Exemples d'utilisation fournis
- ✅ Guide de migration publié

### Compatibilité
- ✅ Code Legacy compile sans changements
- ✅ Bindings XAML fonctionnent
- ✅ Événements Legacy déclenchés correctement
- ✅ Comportement identique à Legacy

---

## 🔧 OUTILLAGE RECOMMANDÉ

### Tests
```csharp
// Structure de test par phase
[TestClass]
public class Phase1_DataRetrievalTests { }

[TestClass]
public class Phase2_SelectionNavigationTests { }

// etc.
```

### Documentation
- XML comments sur toutes les APIs restaurées
- Fichier MIGRATION_GUIDE.md avec exemples avant/après
- Attributs `[Obsolete]` si certaines APIs doivent évoluer

### CI/CD
- Tests automatiques à chaque phase
- Vérification de non-régression V2
- Benchmarks de performance

---

## 📝 NOTES IMPORTANTES

### Événement BytesModified
L'événement `BytesModified<ByteEventArgs>` de Legacy diffère de `ByteModified<ByteModifiedEventArgs>` de V2.
**Solution** : Conserver les deux événements pour compatibilité totale.

### Architecture Préservée
Toutes les méthodes Legacy seront des **wrappers** appelant les services V2 existants.
Cela préserve l'architecture MVVM tout en offrant l'API Legacy.

### Performance
Les optimisations V2 (SIMD, cache LRU, etc.) restent actives.
Legacy API bénéficiera automatiquement des améliorations V2.

---

## 🎯 PROCHAINES ÉTAPES

1. **Valider ce plan** avec l'équipe
2. **Commencer Phase 1** (fondations critiques)
3. **Tests unitaires** parallèles au développement
4. **Review de code** après chaque phase
5. **Release beta** après Phase 4 (fonctionnalités critiques)
6. **Release finale** après Phase 7

---

*Document créé le : 2026-02-19*
*Dernière mise à jour : 2026-02-19*
