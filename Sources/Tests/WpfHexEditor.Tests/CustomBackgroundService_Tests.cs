//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Tests
{
    [TestClass]
    public class CustomBackgroundService_Tests
    {
        private CustomBackgroundService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new CustomBackgroundService();
        }

        #region Add Operations Tests

        [TestMethod]
        public void AddBlock_ValidBlock_ReturnsTrue()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);

            bool result = _service.AddBlock(block);

            Assert.IsTrue(result);
            Assert.AreEqual(1, _service.GetBlockCount());
        }

        [TestMethod]
        public void AddBlock_NullBlock_ReturnsFalse()
        {
            bool result = _service.AddBlock(null);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _service.GetBlockCount());
        }

        [TestMethod]
        public void AddBlock_InvalidBlock_ReturnsFalse()
        {
            var block = new CustomBackgroundBlock(-10, 100, Brushes.Red);

            bool result = _service.AddBlock(block);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void AddBlocks_MultipleValid_ReturnsCorrectCount()
        {
            var blocks = new[]
            {
                new CustomBackgroundBlock(0, 100, Brushes.Red),
                new CustomBackgroundBlock(200, 50, Brushes.Blue),
                new CustomBackgroundBlock(500, 200, Brushes.Green)
            };

            int count = _service.AddBlocks(blocks);

            Assert.AreEqual(3, count);
            Assert.AreEqual(3, _service.GetBlockCount());
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public void AddBlock_RaisesBlockAddedEvent()
        {
            CustomBackgroundBlockEventArgs capturedArgs = null;
            _service.BlockAdded += (s, e) => capturedArgs = e;

            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);
            _service.AddBlock(block);

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(BlockChangeType.Added, capturedArgs.ChangeType);
            Assert.AreSame(block, capturedArgs.Block);
            Assert.AreEqual(1, capturedArgs.AffectedCount);
            Assert.AreEqual(1, capturedArgs.TotalBlockCount);
        }

        [TestMethod]
        public void AddBlocks_RaisesBlockAddedEventWithMultiple()
        {
            CustomBackgroundBlockEventArgs capturedArgs = null;
            _service.BlockAdded += (s, e) => capturedArgs = e;

            var blocks = new[]
            {
                new CustomBackgroundBlock(0, 100, Brushes.Red),
                new CustomBackgroundBlock(200, 50, Brushes.Blue)
            };

            _service.AddBlocks(blocks);

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(BlockChangeType.AddedMultiple, capturedArgs.ChangeType);
            Assert.AreEqual(2, capturedArgs.AffectedCount);
            Assert.AreEqual(2, capturedArgs.Blocks.Count);
        }

        [TestMethod]
        public void RemoveBlock_RaisesBlockRemovedEvent()
        {
            var block = new CustomBackgroundBlock(0, 100, Brushes.Red);
            _service.AddBlock(block);

            CustomBackgroundBlockEventArgs capturedArgs = null;
            _service.BlockRemoved += (s, e) => capturedArgs = e;

            _service.RemoveBlock(block);

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(BlockChangeType.Removed, capturedArgs.ChangeType);
            Assert.AreSame(block, capturedArgs.Block);
            Assert.AreEqual(0, capturedArgs.TotalBlockCount);
        }

        [TestMethod]
        public void ClearAll_RaisesClearedEvent()
        {
            _service.AddBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
            _service.AddBlock(new CustomBackgroundBlock(200, 50, Brushes.Blue));

            CustomBackgroundBlockEventArgs capturedArgs = null;
            _service.BlocksCleared += (s, e) => capturedArgs = e;

            _service.ClearAll();

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(BlockChangeType.Cleared, capturedArgs.ChangeType);
            Assert.AreEqual(2, capturedArgs.AffectedCount);
            Assert.AreEqual(0, capturedArgs.TotalBlockCount);
        }

        [TestMethod]
        public void BlocksChanged_RaisedForAllOperations()
        {
            int changeCount = 0;
            _service.BlocksChanged += (s, e) => changeCount++;

            _service.AddBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
            _service.RemoveBlocksAt(50);
            _service.ClearAll();

            Assert.AreEqual(3, changeCount, "BlocksChanged should fire for all operations");
        }

        #endregion

        #region Query Tests

        [TestMethod]
        public void GetBlockAt_ExistingPosition_ReturnsBlock()
        {
            var block = new CustomBackgroundBlock(100, 50, Brushes.Red); // 100-150
            _service.AddBlock(block);

            var result = _service.GetBlockAt(125);

            Assert.IsNotNull(result);
            Assert.AreEqual(block, result);
        }

        [TestMethod]
        public void GetBlockAt_NonExistingPosition_ReturnsNull()
        {
            _service.AddBlock(new CustomBackgroundBlock(100, 50, Brushes.Red));

            var result = _service.GetBlockAt(200);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetBlocksInRange_OverlappingBlocks_ReturnsAll()
        {
            _service.AddBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
            _service.AddBlock(new CustomBackgroundBlock(50, 100, Brushes.Blue));
            _service.AddBlock(new CustomBackgroundBlock(200, 50, Brushes.Green));

            var results = _service.GetBlocksInRange(40, 110).ToList();

            Assert.AreEqual(2, results.Count, "Should find 2 overlapping blocks");
        }

        #endregion

        #region Remove Operations Tests

        [TestMethod]
        public void RemoveBlocksAt_RemovesOverlappingBlocks()
        {
            _service.AddBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
            _service.AddBlock(new CustomBackgroundBlock(100, 100, Brushes.Blue));
            _service.AddBlock(new CustomBackgroundBlock(200, 100, Brushes.Green));

            int removed = _service.RemoveBlocksAt(150);

            Assert.AreEqual(1, removed);
            Assert.AreEqual(2, _service.GetBlockCount());
        }

        [TestMethod]
        public void RemoveBlocksInRange_RemovesCorrectBlocks()
        {
            _service.AddBlock(new CustomBackgroundBlock(0, 50, Brushes.Red));
            _service.AddBlock(new CustomBackgroundBlock(100, 50, Brushes.Blue));
            _service.AddBlock(new CustomBackgroundBlock(200, 50, Brushes.Green));

            int removed = _service.RemoveBlocksInRange(90, 210);

            Assert.AreEqual(2, removed); // Blue and Green blocks
            Assert.AreEqual(1, _service.GetBlockCount());
        }

        #endregion

        #region Overlap Validation Tests

        [TestMethod]
        public void WouldOverlap_WithExistingBlock_ReturnsTrue()
        {
            _service.AddBlock(new CustomBackgroundBlock(100, 50, Brushes.Red)); // 100-150

            bool result = _service.WouldOverlap(120, 50); // 120-170

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void WouldOverlap_NoOverlap_ReturnsFalse()
        {
            _service.AddBlock(new CustomBackgroundBlock(100, 50, Brushes.Red)); // 100-150

            bool result = _service.WouldOverlap(200, 50);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetOverlappingBlocks_ReturnsCorrectBlocks()
        {
            _service.AddBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
            _service.AddBlock(new CustomBackgroundBlock(50, 100, Brushes.Blue));
            _service.AddBlock(new CustomBackgroundBlock(200, 50, Brushes.Green));

            var overlapping = _service.GetOverlappingBlocks(75, 50).ToList();

            Assert.AreEqual(2, overlapping.Count);
        }

        #endregion
    }
}
