# Phase 1 : Tests Unitaires - Récupération de Données

## Vue d'ensemble

Tests pour les 6 méthodes Legacy implémentées dans HexEditor.xaml.cs :
- `GetByte(long position, bool copyChange)`
- `GetByteModifieds(ByteAction act)`
- `GetSelectionByteArray()`
- `GetAllBytes(bool copyChange)`
- `GetAllBytes()`
- `GetCopyData(long start, long stop, bool copyChange)`

---

## Structure de Tests Suggérée

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using WpfHexaEditor;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Tests
{
    [TestClass]
    public class Phase1_DataRetrievalTests
    {
        #region Test Helpers

        /// <summary>
        /// Creates a HexEditor instance with test data
        /// </summary>
        private HexEditor CreateEditorWithData(byte[] data)
        {
            var editor = new HexEditor();
            var stream = new MemoryStream(data);
            editor.Stream = stream;

            // Wait for initialization
            System.Threading.Thread.Sleep(100);

            return editor;
        }

        /// <summary>
        /// Creates test data with known pattern
        /// </summary>
        private byte[] CreateTestData(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            return data;
        }

        #endregion

        #region GetByte Tests

        [TestMethod]
        public void GetByte_ValidPosition_ReturnsCorrectByte()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var editor = CreateEditorWithData(testData);

            // Act
            var (byteValue, success) = editor.GetByte(2);

            // Assert
            Assert.IsTrue(success, "GetByte should succeed");
            Assert.AreEqual(0x22, byteValue, "Byte value should be 0x22");
        }

        [TestMethod]
        public void GetByte_FirstPosition_ReturnsFirstByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var (byteValue, success) = editor.GetByte(0);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(0xAA, byteValue);
        }

        [TestMethod]
        public void GetByte_LastPosition_ReturnsLastByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var (byteValue, success) = editor.GetByte(2);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(0xCC, byteValue);
        }

        [TestMethod]
        public void GetByte_InvalidPosition_ReturnsFalse()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var (byteValue, success) = editor.GetByte(100);

            // Assert
            Assert.IsFalse(success, "GetByte should fail for out-of-bounds position");
            Assert.IsNull(byteValue);
        }

        [TestMethod]
        public void GetByte_NegativePosition_ReturnsFalse()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var (byteValue, success) = editor.GetByte(-1);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(byteValue);
        }

        [TestMethod]
        public void GetByte_NoDataLoaded_ReturnsFalse()
        {
            // Arrange
            var editor = new HexEditor();

            // Act
            var (byteValue, success) = editor.GetByte(0);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(byteValue);
        }

        #endregion

        #region GetByteModifieds Tests

        [TestMethod]
        public void GetByteModifieds_NoModifications_ReturnsEmptyDictionary()
        {
            // Arrange
            var testData = CreateTestData(100);
            var editor = CreateEditorWithData(testData);

            // Act
            var modified = editor.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(modified);
            Assert.AreEqual(0, modified.Count);
        }

        [TestMethod]
        public void GetByteModifieds_WithModifications_ReturnsModifiedBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var editor = CreateEditorWithData(testData);

            // Modify a byte (requires implementing modification methods first)
            // For now, this is a placeholder test
            // TODO: Implement after Phase 3 (byte modification methods)

            // Act
            var modified = editor.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(modified);
            // TODO: Add assertions after modification is implemented
        }

        [TestMethod]
        public void GetByteModifieds_NoDataLoaded_ReturnsEmptyDictionary()
        {
            // Arrange
            var editor = new HexEditor();

            // Act
            var modified = editor.GetByteModifieds(ByteAction.Modified);

            // Assert
            Assert.IsNotNull(modified);
            Assert.AreEqual(0, modified.Count);
        }

        #endregion

        #region GetSelectionByteArray Tests

        [TestMethod]
        public void GetSelectionByteArray_NoSelection_ReturnsEmpty()
        {
            // Arrange
            var testData = CreateTestData(100);
            var editor = CreateEditorWithData(testData);
            editor.SelectionStart = 0;
            editor.SelectionStop = 0;

            // Act
            var result = editor.GetSelectionByteArray();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetSelectionByteArray_WithSelection_ReturnsSelectedBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var editor = CreateEditorWithData(testData);
            editor.SelectionStart = 2;
            editor.SelectionStop = 4;

            // Act
            var result = editor.GetSelectionByteArray();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0x22, result[0]);
            Assert.AreEqual(0x33, result[1]);
            Assert.AreEqual(0x44, result[2]);
        }

        [TestMethod]
        public void GetSelectionByteArray_EntireFile_ReturnsAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);
            editor.SelectionStart = 0;
            editor.SelectionStop = 2;

            // Act
            var result = editor.GetSelectionByteArray();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        #endregion

        #region GetAllBytes Tests

        [TestMethod]
        public void GetAllBytes_WithData_ReturnsAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetAllBytes(true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testData.Length, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetAllBytes_ParameterlessOverload_ReturnsAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetAllBytes();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testData.Length, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetAllBytes_LargeFile_ReturnsAllBytes()
        {
            // Arrange
            var testData = CreateTestData(10000);
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetAllBytes();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(10000, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetAllBytes_NoDataLoaded_ReturnsEmpty()
        {
            // Arrange
            var editor = new HexEditor();

            // Act
            var result = editor.GetAllBytes();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region GetCopyData Tests

        [TestMethod]
        public void GetCopyData_ValidRange_ReturnsCorrectBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetCopyData(2, 4, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0x22, result[0]);
            Assert.AreEqual(0x33, result[1]);
            Assert.AreEqual(0x44, result[2]);
        }

        [TestMethod]
        public void GetCopyData_ReversedRange_HandlesCorrectly()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var editor = CreateEditorWithData(testData);

            // Act - start > stop should be normalized
            var result = editor.GetCopyData(4, 2, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0x22, result[0]);
            Assert.AreEqual(0x33, result[1]);
            Assert.AreEqual(0x44, result[2]);
        }

        [TestMethod]
        public void GetCopyData_SingleByte_ReturnsSingleByte()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetCopyData(1, 1, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0xBB, result[0]);
        }

        [TestMethod]
        public void GetCopyData_EntireFile_ReturnsAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var editor = CreateEditorWithData(testData);

            // Act
            var result = editor.GetCopyData(0, 3, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetCopyData_OutOfBounds_ReturnsClampedRange()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var editor = CreateEditorWithData(testData);

            // Act - request beyond file length
            var result = editor.GetCopyData(0, 100, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            CollectionAssert.AreEqual(testData, result);
        }

        [TestMethod]
        public void GetCopyData_NoDataLoaded_ReturnsEmpty()
        {
            // Arrange
            var editor = new HexEditor();

            // Act
            var result = editor.GetCopyData(0, 10, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_GetByteAndGetCopyData_ConsistentResults()
        {
            // Arrange
            var testData = CreateTestData(100);
            var editor = CreateEditorWithData(testData);

            // Act - Compare GetByte vs GetCopyData for same position
            var (byteFromGetByte, success) = editor.GetByte(50);
            var arrayFromGetCopyData = editor.GetCopyData(50, 50, true);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(1, arrayFromGetCopyData.Length);
            Assert.AreEqual(byteFromGetByte, arrayFromGetCopyData[0]);
        }

        [TestMethod]
        public void Integration_GetAllBytes_MatchesGetCopyData()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var editor = CreateEditorWithData(testData);

            // Act
            var allBytes = editor.GetAllBytes();
            var copyData = editor.GetCopyData(0, testData.Length - 1, true);

            // Assert
            CollectionAssert.AreEqual(allBytes, copyData);
        }

        [TestMethod]
        public void Integration_GetSelectionByteArray_MatchesGetCopyData()
        {
            // Arrange
            var testData = CreateTestData(50);
            var editor = CreateEditorWithData(testData);
            editor.SelectionStart = 10;
            editor.SelectionStop = 20;

            // Act
            var selectionArray = editor.GetSelectionByteArray();
            var copyData = editor.GetCopyData(10, 20, true);

            // Assert
            CollectionAssert.AreEqual(selectionArray, copyData);
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Performance_GetByte_LargeFile()
        {
            // Arrange
            var testData = CreateTestData(1000000); // 1MB
            var editor = CreateEditorWithData(testData);

            // Act & Assert - Should complete in reasonable time
            var startTime = DateTime.Now;

            for (int i = 0; i < 1000; i++)
            {
                var (_, success) = editor.GetByte(i * 100);
                Assert.IsTrue(success);
            }

            var elapsed = DateTime.Now - startTime;
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"GetByte took too long: {elapsed.TotalSeconds}s");
        }

        [TestMethod]
        public void Performance_GetCopyData_LargeRange()
        {
            // Arrange
            var testData = CreateTestData(100000); // 100KB
            var editor = CreateEditorWithData(testData);

            // Act
            var startTime = DateTime.Now;
            var result = editor.GetCopyData(0, 99999, true);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.AreEqual(100000, result.Length);
            Assert.IsTrue(elapsed.TotalSeconds < 0.5,
                $"GetCopyData took too long: {elapsed.TotalSeconds}s");
        }

        #endregion
    }
}
```

---

## Instructions d'Exécution

### Option 1 : Tests Manuels (Quick)

Créer un projet Sample simple :

```csharp
// Dans un nouveau projet Console ou WPF
var editor = new HexEditor();
var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
editor.Stream = new MemoryStream(testData);

// Test GetByte
var (byteValue, success) = editor.GetByte(2);
Console.WriteLine($"GetByte(2): {byteValue:X2} (success: {success})");

// Test GetAllBytes
var allBytes = editor.GetAllBytes();
Console.WriteLine($"GetAllBytes: {allBytes.Length} bytes");

// Test GetCopyData
var range = editor.GetCopyData(1, 3, true);
Console.WriteLine($"GetCopyData(1-3): {range.Length} bytes");
```

### Option 2 : Projet MSTest Complet

1. Créer un nouveau projet de tests :
```bash
cd Sources
dotnet new mstest -n WPFHexaEditor.Tests
dotnet sln add WPFHexaEditor.Tests/WPFHexaEditor.Tests.csproj
```

2. Ajouter la référence au projet principal :
```bash
cd WPFHexaEditor.Tests
dotnet add reference ../WPFHexaEditor/WpfHexEditorCore.csproj
```

3. Copier le code de test ci-dessus dans `Phase1_DataRetrievalTests.cs`

4. Exécuter les tests :
```bash
dotnet test
```

---

## Résultats Attendus

### Tests Passants (Expected)
- ✅ `GetByte_ValidPosition_ReturnsCorrectByte`
- ✅ `GetByte_FirstPosition_ReturnsFirstByte`
- ✅ `GetByte_LastPosition_ReturnsLastByte`
- ✅ `GetByte_InvalidPosition_ReturnsFalse`
- ✅ `GetByte_NoDataLoaded_ReturnsFalse`
- ✅ `GetAllBytes_WithData_ReturnsAllBytes`
- ✅ `GetAllBytes_ParameterlessOverload_ReturnsAllBytes`
- ✅ `GetCopyData_ValidRange_ReturnsCorrectBytes`
- ✅ `GetCopyData_ReversedRange_HandlesCorrectly`
- ✅ `GetCopyData_SingleByte_ReturnsSingleByte`
- ✅ `Integration_GetByteAndGetCopyData_ConsistentResults`

### Tests À Adapter
- ⚠️ `GetByteModifieds_WithModifications_ReturnsModifiedBytes`
  → Nécessite Phase 3 (modification de bytes)

---

## Validation

### Critères de Succès Phase 1
- [x] 6 méthodes implémentées dans HexEditor.xaml.cs
- [ ] Au moins 80% des tests passent
- [ ] Aucune régression sur fonctionnalités V2 existantes
- [ ] Documentation XML complète

### Prochaines Étapes
1. Exécuter les tests (Option 1 ou 2)
2. Valider les résultats
3. Corriger les bugs si nécessaire
4. Passer à Phase 2 (Sélection & Navigation)

---

*Tests créés pour Phase 1 - Migration Legacy API*
*Date : 2026-02-19*
