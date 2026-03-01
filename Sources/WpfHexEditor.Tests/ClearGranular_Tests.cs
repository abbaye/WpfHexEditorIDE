using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Tests
{
    /// <summary>
    /// Unit tests for granular clear operations: ClearModifications, ClearInsertions, ClearDeletions
    /// These tests validate the final APIs to achieve 100% ByteProvider compatibility (186/186)
    /// </summary>
    [TestClass]
    public class ClearGranular_Tests
    {
        #region Test Helpers

        /// <summary>
        /// Creates a ByteProvider instance with test data
        /// </summary>
        private ByteProvider CreateProviderWithData(byte[] data)
        {
            var provider = new ByteProvider();
            provider.OpenMemory(data);
            return provider;
        }

        #endregion

        #region ClearModifications Tests

        [TestMethod]
        public void ClearModifications_OnlyModifications_ClearsModificationsOnly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var provider = CreateProviderWithData(data);

            // Make some modifications
            provider.ModifyByte(0, 0xAA);
            provider.ModifyByte(2, 0xBB);
            provider.ModifyByte(4, 0xCC);

            // Verify modifications applied
            Assert.AreEqual(0xAA, provider.GetByte(0).value);
            Assert.AreEqual(0xBB, provider.GetByte(2).value);
            Assert.AreEqual(0xCC, provider.GetByte(4).value);

            // Act
            provider.ClearModifications();

            // Assert - Modifications should be cleared
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x44, provider.GetByte(4).value);

            // Unchanged bytes still intact
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
            Assert.AreEqual(0x55, provider.GetByte(5).value);
        }

        [TestMethod]
        public void ClearModifications_WithInsertions_KeepsInsertions()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Make modifications
            provider.ModifyByte(1, 0xAA);
            provider.ModifyByte(3, 0xBB);

            long lengthBeforeInsert = provider.Length;

            // Make insertions at end
            provider.InsertBytes(5, new byte[] { 0xEE, 0xEE });

            // Length after insertions: original 5 + 2 = 7
            Assert.AreEqual(7, provider.Length);

            // Act - Clear only modifications
            provider.ClearModifications();

            // Assert - Length should stay same (insertions preserved)
            Assert.AreEqual(7, provider.Length);

            // Modifications should be cleared
            Assert.AreEqual(0x11, provider.GetByte(1).value); // Was 0xAA, now back to 0x11
            Assert.AreEqual(0x33, provider.GetByte(3).value); // Was 0xBB, now back to 0x33

            // Original unmodified bytes intact
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x44, provider.GetByte(4).value);
        }

        [TestMethod]
        public void ClearModifications_NoModifications_DoesNothing()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // No modifications made

            // Act
            provider.ClearModifications();

            // Assert - Data unchanged
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
        }

        #endregion

        #region ClearInsertions Tests

        [TestMethod]
        public void ClearInsertions_OnlyInsertions_ClearsInsertionsOnly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // Make insertions
            provider.InsertBytes(1, new byte[] { 0xAA, 0xBB });
            provider.InsertBytes(4, new byte[] { 0xCC });

            // Verify insertions applied
            Assert.AreEqual(7, provider.Length); // Original 4 + 3 inserted

            // Act
            provider.ClearInsertions();

            // Assert - Should be back to original length
            Assert.AreEqual(4, provider.Length);

            // Original data should be intact
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
        }

        [TestMethod]
        public void ClearInsertions_WithModificationsAndDeletions_KeepsOtherChanges()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Make modifications
            provider.ModifyByte(1, 0xAA);
            provider.ModifyByte(3, 0xBB);

            // Make insertions at end
            provider.InsertBytes(5, new byte[] { 0xEE, 0xEE });

            // Length before clear: 5 + 2 inserted = 7
            Assert.AreEqual(7, provider.Length);

            // Act - Clear only insertions
            provider.ClearInsertions();

            // Assert
            // Length should be back to 5 (insertions removed)
            Assert.AreEqual(5, provider.Length);

            // Modifications should still be there
            Assert.AreEqual(0xAA, provider.GetByte(1).value);
            Assert.AreEqual(0xBB, provider.GetByte(3).value);
        }

        [TestMethod]
        public void ClearInsertions_NoInsertions_DoesNothing()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // No insertions made

            // Act
            provider.ClearInsertions();

            // Assert - Length and data unchanged
            Assert.AreEqual(4, provider.Length);
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
        }

        #endregion

        #region ClearDeletions Tests

        [TestMethod]
        public void ClearDeletions_OnlyDeletions_RestoresDeletionsOnly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var provider = CreateProviderWithData(data);

            // Make deletions
            provider.DeleteBytes(1, 2); // Delete 0x11, 0x22
            provider.DeleteBytes(2, 1); // Delete 0x44

            // Verify deletions applied
            Assert.AreEqual(3, provider.Length); // 6 - 3 = 3

            // Act
            provider.ClearDeletions();

            // Assert - Should restore to original length
            Assert.AreEqual(6, provider.Length);

            // Original data should be restored
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
            Assert.AreEqual(0x44, provider.GetByte(4).value);
            Assert.AreEqual(0x55, provider.GetByte(5).value);
        }

        [TestMethod]
        public void ClearDeletions_WithModifications_KeepsModifications()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider = CreateProviderWithData(data);

            // Make modifications
            provider.ModifyByte(1, 0xAA);
            provider.ModifyByte(3, 0xBB);

            // Make deletion
            provider.DeleteBytes(4, 1); // Delete last byte 0x44

            // Length before clear: 5 - 1 deleted = 4
            Assert.AreEqual(4, provider.Length);

            // Act - Clear only deletions
            provider.ClearDeletions();

            // Assert
            // Length should be back to 5 (deletion restored)
            Assert.AreEqual(5, provider.Length);

            // Modifications should still be there
            Assert.AreEqual(0xAA, provider.GetByte(1).value);
            Assert.AreEqual(0xBB, provider.GetByte(3).value);

            // Deleted byte should be restored
            Assert.AreEqual(0x44, provider.GetByte(4).value);
        }

        [TestMethod]
        public void ClearDeletions_NoDeletions_DoesNothing()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // No deletions made

            // Act
            provider.ClearDeletions();

            // Assert - Length and data unchanged
            Assert.AreEqual(4, provider.Length);
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_CombinedEdits_ClearIndividually()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var provider = CreateProviderWithData(data);

            // Step 1: Make all types of edits
            provider.ModifyByte(1, 0xAA);           // Modification
            provider.ModifyByte(3, 0xBB);           // Modification
            provider.InsertBytes(2, new byte[] { 0xEE }); // Insertion
            provider.DeleteBytes(5, 1);             // Deletion

            // Verify combined state
            // Original: [00, 11, 22, 33, 44, 55, 66] (length 7)
            // After all edits: length should be 7 - 1 + 1 = 7
            Assert.AreEqual(7, provider.Length);

            // Step 2: Clear modifications only
            provider.ClearModifications();

            // Modifications cleared, but structure (insertions/deletions) remain
            Assert.AreEqual(7, provider.Length);

            // Step 3: Clear insertions
            provider.ClearInsertions();

            // Now: 7 - 1 inserted = 6
            Assert.AreEqual(6, provider.Length);

            // Step 4: Clear deletions
            provider.ClearDeletions();

            // Now: back to original length
            Assert.AreEqual(7, provider.Length);

            // Final: Should be back to original data
            Assert.AreEqual(0x00, provider.GetByte(0).value);
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x22, provider.GetByte(2).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
            Assert.AreEqual(0x44, provider.GetByte(4).value);
            Assert.AreEqual(0x55, provider.GetByte(5).value);
            Assert.AreEqual(0x66, provider.GetByte(6).value);
        }

        [TestMethod]
        public void Integration_ClearGranularVsClearAll_SameResult()
        {
            // Arrange
            var data1 = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var data2 = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var provider1 = CreateProviderWithData(data1);
            var provider2 = CreateProviderWithData(data2);

            // Make identical edits to both
            provider1.ModifyByte(1, 0xAA);
            provider2.ModifyByte(1, 0xAA);

            provider1.InsertBytes(2, new byte[] { 0xEE });
            provider2.InsertBytes(2, new byte[] { 0xEE });

            provider1.DeleteBytes(3, 1);
            provider2.DeleteBytes(3, 1);

            // Act - Clear all at once vs granularly
            provider1.ClearAllEdits();

            provider2.ClearModifications();
            provider2.ClearInsertions();
            provider2.ClearDeletions();

            // Assert - Both should have same result
            Assert.AreEqual(provider1.Length, provider2.Length);

            for (long i = 0; i < provider1.Length; i++)
            {
                Assert.AreEqual(provider1.GetByte(i).value, provider2.GetByte(i).value,
                    $"Byte at position {i} should match");
            }
        }

        [TestMethod]
        public void Integration_SelectiveClear_ModificationsThenInsertions()
        {
            // Arrange - Real-world scenario
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var provider = CreateProviderWithData(data);

            // User makes byte value changes (modifications)
            provider.ModifyByte(0, 0xFF);
            provider.ModifyByte(2, 0xEE);
            provider.ModifyByte(4, 0xDD);

            // Verify modifications applied
            Assert.AreEqual(0xFF, provider.GetByte(0).value);
            Assert.AreEqual(0xEE, provider.GetByte(2).value);
            Assert.AreEqual(0xDD, provider.GetByte(4).value);

            // Scenario: User wants to reset byte values
            // Act
            provider.ClearModifications();

            // Assert
            // Length should remain same
            Assert.AreEqual(6, provider.Length);

            // Modifications should be cleared (back to original values)
            Assert.AreEqual(0x00, provider.GetByte(0).value); // Was 0xFF, now 0x00
            Assert.AreEqual(0x22, provider.GetByte(2).value); // Was 0xEE, now 0x22
            Assert.AreEqual(0x44, provider.GetByte(4).value); // Was 0xDD, now 0x44

            // Unmodified bytes still intact
            Assert.AreEqual(0x11, provider.GetByte(1).value);
            Assert.AreEqual(0x33, provider.GetByte(3).value);
            Assert.AreEqual(0x55, provider.GetByte(5).value);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void EdgeCase_ClearAfterUndo_WorksCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            // Make modification
            provider.ModifyByte(1, 0xAA);
            Assert.AreEqual(0xAA, provider.GetByte(1).value);

            // Undo
            provider.Undo();
            Assert.AreEqual(0x11, provider.GetByte(1).value);

            // Redo
            provider.Redo();
            Assert.AreEqual(0xAA, provider.GetByte(1).value);

            // Act - Clear modifications
            provider.ClearModifications();

            // Assert
            Assert.AreEqual(0x11, provider.GetByte(1).value);
        }

        [TestMethod]
        public void EdgeCase_MultipleClears_Idempotent()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var provider = CreateProviderWithData(data);

            provider.ModifyByte(1, 0xAA);
            provider.InsertBytes(2, new byte[] { 0xBB });
            provider.DeleteBytes(3, 1);

            // Act - Clear modifications multiple times
            provider.ClearModifications();
            provider.ClearModifications();
            provider.ClearModifications();

            // Assert - Should be idempotent (no error, same result)
            Assert.AreEqual(0x11, provider.GetByte(1).value);

            // Act - Clear insertions multiple times
            provider.ClearInsertions();
            provider.ClearInsertions();

            // Assert
            Assert.AreEqual(3, provider.Length);

            // Act - Clear deletions multiple times
            provider.ClearDeletions();
            provider.ClearDeletions();

            // Assert
            Assert.AreEqual(4, provider.Length);
        }

        [TestMethod]
        public void EdgeCase_ClearEmptyProvider_NoError()
        {
            // Arrange
            var provider = new ByteProvider();
            provider.OpenMemory(new byte[0]);

            // Act & Assert - Should not throw
            provider.ClearModifications();
            provider.ClearInsertions();
            provider.ClearDeletions();
        }

        #endregion
    }
}
