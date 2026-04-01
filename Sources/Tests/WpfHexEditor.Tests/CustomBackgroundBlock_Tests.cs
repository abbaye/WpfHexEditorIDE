//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Media;
using WpfHexEditor.Core;

namespace WpfHexEditor.Tests
{
    [TestClass]
    public class CustomBackgroundBlock_Tests
    {
        #region Equality Tests

        [TestMethod]
        public void Equals_SameProperties_ReturnsTrue()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red, "Test");
            var block2 = new CustomBackgroundBlock(100, 50, Brushes.Red, "Test");

            Assert.IsTrue(block1.Equals(block2));
            Assert.AreEqual(block1.GetHashCode(), block2.GetHashCode());
        }

        [TestMethod]
        public void Equals_DifferentOffset_ReturnsFalse()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red);
            var block2 = new CustomBackgroundBlock(200, 50, Brushes.Red);

            Assert.IsFalse(block1.Equals(block2));
        }

        [TestMethod]
        public void Equals_DifferentLength_ReturnsFalse()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red);
            var block2 = new CustomBackgroundBlock(100, 100, Brushes.Red);

            Assert.IsFalse(block1.Equals(block2));
        }

        [TestMethod]
        public void Equals_DifferentOpacity_ReturnsFalse()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red, "", 0.3);
            var block2 = new CustomBackgroundBlock(100, 50, Brushes.Red, "", 0.5);

            Assert.IsFalse(block1.Equals(block2));
        }

        [TestMethod]
        public void Equals_Null_ReturnsFalse()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red);

            Assert.IsFalse(block1.Equals(null));
        }

        [TestMethod]
        public void Equals_SameReference_ReturnsTrue()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red);

            Assert.IsTrue(block1.Equals(block1));
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public void IsValid_ValidBlock_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Blue);
            Assert.IsTrue(block.IsValid);
        }

        [TestMethod]
        public void IsValid_NegativeOffset_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(-10, 100, Brushes.Blue);
            Assert.IsFalse(block.IsValid);
        }

        [TestMethod]
        public void IsValid_ZeroLength_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(0, 0, Brushes.Blue);
            Assert.IsFalse(block.IsValid);
        }

        [TestMethod]
        public void IsValid_NegativeLength_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(0, -50, Brushes.Blue);
            Assert.IsFalse(block.IsValid);
        }

        [TestMethod]
        public void IsValid_NullColor_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(0, 100, null);
            Assert.IsFalse(block.IsValid);
        }

        #endregion

        #region ContainsPosition Tests

        [TestMethod]
        public void ContainsPosition_PositionAtStart_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.ContainsPosition(100));
        }

        [TestMethod]
        public void ContainsPosition_PositionInMiddle_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.ContainsPosition(125));
        }

        [TestMethod]
        public void ContainsPosition_PositionAtEndMinus1_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.ContainsPosition(149));
        }

        [TestMethod]
        public void ContainsPosition_PositionBeforeStart_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.ContainsPosition(99));
        }

        [TestMethod]
        public void ContainsPosition_PositionAtStopOffset_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.ContainsPosition(150)); // StopOffset is exclusive
        }

        [TestMethod]
        public void ContainsPosition_PositionAfterEnd_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.ContainsPosition(200));
        }

        #endregion

        #region Overlaps Tests

        [TestMethod]
        public void Overlaps_CompleteOverlap_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.Overlaps(90, 70));  // Covers entire block
        }

        [TestMethod]
        public void Overlaps_InsideBlock_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.Overlaps(110, 30)); // Inside block
        }

        [TestMethod]
        public void Overlaps_OverlapsStart_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.Overlaps(50, 60));  // 50-110, overlaps start
        }

        [TestMethod]
        public void Overlaps_OverlapsEnd_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsTrue(block.Overlaps(140, 20)); // 140-160, overlaps end
        }

        [TestMethod]
        public void Overlaps_NoOverlap_Before_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.Overlaps(0, 100));   // 0-100, before
        }

        [TestMethod]
        public void Overlaps_NoOverlap_After_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.Overlaps(150, 50));  // 150-200, after
        }

        [TestMethod]
        public void Overlaps_NoOverlap_FarAfter_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            Assert.IsFalse(block.Overlaps(200, 100)); // 200-300, far after
        }

        #endregion

        #region GetIntersection Tests

        [TestMethod]
        public void GetIntersection_FullOverlap_ReturnsFullBlock()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(90, 70); // 90-160

            Assert.IsNotNull(result);
            Assert.AreEqual(100L, result.Value.start);
            Assert.AreEqual(50L, result.Value.length);
        }

        [TestMethod]
        public void GetIntersection_PartialOverlap_Start_ReturnsIntersection()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(80, 30); // 80-110

            Assert.IsNotNull(result);
            Assert.AreEqual(100L, result.Value.start);
            Assert.AreEqual(10L, result.Value.length); // 100-110
        }

        [TestMethod]
        public void GetIntersection_PartialOverlap_End_ReturnsIntersection()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(120, 50); // 120-170

            Assert.IsNotNull(result);
            Assert.AreEqual(120L, result.Value.start);
            Assert.AreEqual(30L, result.Value.length); // 120-150
        }

        [TestMethod]
        public void GetIntersection_InsideBlock_ReturnsRange()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(110, 20); // 110-130

            Assert.IsNotNull(result);
            Assert.AreEqual(110L, result.Value.start);
            Assert.AreEqual(20L, result.Value.length);
        }

        [TestMethod]
        public void GetIntersection_NoOverlap_Before_ReturnsNull()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(0, 100); // 0-100

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetIntersection_NoOverlap_After_ReturnsNull()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150

            var result = block.GetIntersection(200, 50); // 200-250

            Assert.IsNull(result);
        }

        #endregion

        #region Brush Caching Tests

        [TestMethod]
        public void GetTransparentBrush_SameInstance_ReturnsCached()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            var brush1 = block.GetTransparentBrush();
            var brush2 = block.GetTransparentBrush();

            Assert.AreSame(brush1, brush2, "Should return cached instance");
        }

        [TestMethod]
        public void GetTransparentBrush_AfterColorChange_ReturnsNewBrush()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            var brush1 = block.GetTransparentBrush();
            block.Color = Brushes.Blue; // Invalidates cache
            var brush2 = block.GetTransparentBrush();

            Assert.AreNotSame(brush1, brush2, "Should return new brush after color change");
        }

        [TestMethod]
        public void GetTransparentBrush_AfterOpacityChange_ReturnsNewBrush()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            var brush1 = block.GetTransparentBrush();
            block.Opacity = 0.7; // Invalidates cache
            var brush2 = block.GetTransparentBrush();

            Assert.AreNotSame(brush1, brush2, "Should return new brush after opacity change");
        }

        [TestMethod]
        public void GetTransparentBrush_ChecksOpacity()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red, "", 0.5);

            var brush = block.GetTransparentBrush();

            Assert.AreEqual(0.5, brush.Opacity, 0.001);
        }

        [TestMethod]
        public void GetTransparentBrush_IsFrozen()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            var brush = block.GetTransparentBrush();

            Assert.IsTrue(brush.IsFrozen, "Brush should be frozen for performance");
        }

        [TestMethod]
        public void GetTransparentBrush_NullColor_ReturnsTransparent()
        {
            var block = new CustomBackgroundBlock
            {
                StartOffset = 0,
                Length = 100,
                Color = null
            };

            var brush = block.GetTransparentBrush();

            Assert.AreEqual(Brushes.Transparent, brush);
        }

        [TestMethod]
        public void InvalidateBrushCache_ClearsCache()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            var brush1 = block.GetTransparentBrush();
            block.InvalidateBrushCache();
            var brush2 = block.GetTransparentBrush();

            Assert.AreNotSame(brush1, brush2, "Cache should be cleared");
        }

        #endregion

        #region Opacity Tests

        [TestMethod]
        public void Opacity_DefaultValue_Is03()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            Assert.AreEqual(0.3, block.Opacity, 0.001);
        }

        [TestMethod]
        public void Opacity_SetValid_StoresValue()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            block.Opacity = 0.7;

            Assert.AreEqual(0.7, block.Opacity, 0.001);
        }

        [TestMethod]
        public void Opacity_SetAbove1_ClampsTo1()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            block.Opacity = 1.5;

            Assert.AreEqual(1.0, block.Opacity, 0.001);
        }

        [TestMethod]
        public void Opacity_SetBelow0_ClampsTo0()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            block.Opacity = -0.5;

            Assert.AreEqual(0.0, block.Opacity, 0.001);
        }

        #endregion

        #region ToString Tests

        [TestMethod]
        public void ToString_FormatsCorrectly()
        {
            var block = new CustomBackgroundBlock(0x100, 0x50, Brushes.Red, "Header");

            var result = block.ToString();

            StringAssert.Contains(result, "0x00000100"); // Hex start
            StringAssert.Contains(result, "0x00000150"); // Hex stop
            StringAssert.Contains(result, "80");         // Length (0x50 = 80)
            StringAssert.Contains(result, "Header");     // Description
        }

        [TestMethod]
        public void ToString_WithoutDescription_FormatsCorrectly()
        {
            var block = new CustomBackgroundBlock(256, 128, Brushes.Blue);

            var result = block.ToString();

            StringAssert.Contains(result, "0x00000100"); // 256 in hex
            StringAssert.Contains(result, "Len=128");
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        public void Constructor_Full_SetsAllProperties()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red, "Test Block", 0.5);

            Assert.AreEqual(100L, block.StartOffset);
            Assert.AreEqual(50L, block.Length);
            Assert.AreEqual(150L, block.StopOffset);
            Assert.AreEqual("Test Block", block.Description);
            Assert.AreEqual(0.5, block.Opacity, 0.001);
            Assert.AreEqual(Brushes.Red, block.Color);
        }

        [TestMethod]
        public void Constructor_RandomBrush_SetsRandomColor()
        {
            var block = new CustomBackgroundBlock(0, 100, setRandomBrush: true);

            Assert.IsNotNull(block.Color);
            Assert.AreNotEqual(Brushes.Transparent, block.Color);
        }

        [TestMethod]
        public void Constructor_NoRandomBrush_DoesNotSetColor()
        {
            var block = new CustomBackgroundBlock(0, 100, setRandomBrush: false);

            // Default color should be Transparent
            Assert.AreEqual(Brushes.Transparent, block.Color);
        }

        #endregion

        #region Clone Tests

        [TestMethod]
        public void Clone_CreatesNewInstance()
        {
            var block1 = new CustomBackgroundBlock(100, 50, Brushes.Red, "Original");

            var block2 = (CustomBackgroundBlock)block1.Clone();

            Assert.AreNotSame(block1, block2);
            Assert.AreEqual(block1.StartOffset, block2.StartOffset);
            Assert.AreEqual(block1.Length, block2.Length);
            Assert.AreEqual(block1.Description, block2.Description);
        }

        #endregion
    }
}
