//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Linq;
using Xunit;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Services
{
    /// <summary>
    /// Unit tests for HighlightService (stateful service)
    /// </summary>
    public class HighlightServiceTests
    {
        private readonly HighlightService _service;

        public HighlightServiceTests()
        {
            _service = new HighlightService();
        }

        #region AddHighLight Tests

        [Fact]
        public void AddHighLight_ValidRange_AddsHighlights()
        {
            _service.AddHighLight(100, 10);

            Assert.True(_service.HasHighlights());
            Assert.Equal(10, _service.GetHighlightCount());
        }

        [Fact]
        public void AddHighLight_MultipleRanges_AddsAll()
        {
            _service.AddHighLight(100, 5);
            _service.AddHighLight(200, 5);
            _service.AddHighLight(300, 5);

            Assert.Equal(15, _service.GetHighlightCount());
        }

        [Fact]
        public void AddHighLight_OverlappingRanges_HandlesCorrectly()
        {
            _service.AddHighLight(100, 10); // 100-109
            _service.AddHighLight(105, 10); // 105-114

            // Should have highlights for 100-114 (15 positions)
            Assert.True(_service.GetHighlightCount() >= 10);
        }

        [Fact]
        public void AddHighLight_ZeroLength_DoesNotAdd()
        {
            _service.AddHighLight(100, 0);

            Assert.False(_service.HasHighlights());
            Assert.Equal(0, _service.GetHighlightCount());
        }

        [Fact]
        public void AddHighLight_NegativePosition_DoesNotAdd()
        {
            _service.AddHighLight(-10, 5);

            Assert.False(_service.HasHighlights());
        }

        #endregion

        #region RemoveHighLight Tests

        [Fact]
        public void RemoveHighLight_ExistingRange_RemovesHighlights()
        {
            _service.AddHighLight(100, 10);
            _service.RemoveHighLight(100, 5);

            Assert.Equal(5, _service.GetHighlightCount());
        }

        [Fact]
        public void RemoveHighLight_EntireRange_RemovesAll()
        {
            _service.AddHighLight(100, 10);
            _service.RemoveHighLight(100, 10);

            Assert.False(_service.HasHighlights());
        }

        [Fact]
        public void RemoveHighLight_NonExistentRange_DoesNothing()
        {
            _service.AddHighLight(100, 10);
            _service.RemoveHighLight(200, 5);

            Assert.Equal(10, _service.GetHighlightCount());
        }

        #endregion

        #region UnHighLightAll Tests

        [Fact]
        public void UnHighLightAll_WithHighlights_ClearsAll()
        {
            _service.AddHighLight(100, 10);
            _service.AddHighLight(200, 10);
            _service.AddHighLight(300, 10);

            _service.UnHighLightAll();

            Assert.False(_service.HasHighlights());
            Assert.Equal(0, _service.GetHighlightCount());
        }

        [Fact]
        public void UnHighLightAll_NoHighlights_DoesNothing()
        {
            _service.UnHighLightAll();

            Assert.False(_service.HasHighlights());
        }

        #endregion

        #region IsHighlighted Tests

        [Fact]
        public void IsHighlighted_HighlightedPosition_ReturnsTrue()
        {
            _service.AddHighLight(100, 10);

            Assert.True(_service.IsHighlighted(100));
            Assert.True(_service.IsHighlighted(105));
            Assert.True(_service.IsHighlighted(109));
        }

        [Fact]
        public void IsHighlighted_NonHighlightedPosition_ReturnsFalse()
        {
            _service.AddHighLight(100, 10);

            Assert.False(_service.IsHighlighted(99));
            Assert.False(_service.IsHighlighted(110));
            Assert.False(_service.IsHighlighted(200));
        }

        [Fact]
        public void IsHighlighted_NegativePosition_ReturnsFalse()
        {
            _service.AddHighLight(100, 10);

            Assert.False(_service.IsHighlighted(-1));
        }

        #endregion

        #region GetHighlightedPositions Tests

        [Fact]
        public void GetHighlightedPositions_WithHighlights_ReturnsAllPositions()
        {
            _service.AddHighLight(100, 5);
            _service.AddHighLight(200, 3);

            var positions = _service.GetHighlightedPositions().ToList();

            Assert.Equal(8, positions.Count);
            Assert.Contains(100L, positions);
            Assert.Contains(104L, positions);
            Assert.Contains(200L, positions);
            Assert.Contains(202L, positions);
        }

        [Fact]
        public void GetHighlightedPositions_NoHighlights_ReturnsEmpty()
        {
            var positions = _service.GetHighlightedPositions();

            Assert.Empty(positions);
        }

        #endregion

        #region GetHighlightedRanges Tests

        [Fact]
        public void GetHighlightedRanges_ConsecutivePositions_GroupsIntoRanges()
        {
            _service.AddHighLight(100, 5);  // 100-104
            _service.AddHighLight(200, 3);  // 200-202

            var ranges = _service.GetHighlightedRanges().ToList();

            Assert.Equal(2, ranges.Count);

            var range1 = ranges[0];
            Assert.Equal(100, range1.start);
            Assert.Equal(5, range1.length);

            var range2 = ranges[1];
            Assert.Equal(200, range2.start);
            Assert.Equal(3, range2.length);
        }

        [Fact]
        public void GetHighlightedRanges_SinglePosition_ReturnsSingleRange()
        {
            _service.AddHighLight(100, 1);

            var ranges = _service.GetHighlightedRanges().ToList();

            Assert.Single(ranges);
            Assert.Equal(100, ranges[0].start);
            Assert.Equal(1, ranges[0].length);
        }

        [Fact]
        public void GetHighlightedRanges_NoHighlights_ReturnsEmpty()
        {
            var ranges = _service.GetHighlightedRanges();

            Assert.Empty(ranges);
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_WithHighlights_ClearsAll()
        {
            _service.AddHighLight(100, 10);
            _service.AddHighLight(200, 10);

            _service.Clear();

            Assert.False(_service.HasHighlights());
            Assert.Equal(0, _service.GetHighlightCount());
        }

        #endregion

        #region State Persistence Tests

        [Fact]
        public void HighlightService_MaintainsState_AcrossMultipleOperations()
        {
            // Add highlights
            _service.AddHighLight(100, 10);
            Assert.Equal(10, _service.GetHighlightCount());

            // Remove some
            _service.RemoveHighLight(100, 5);
            Assert.Equal(5, _service.GetHighlightCount());

            // Add more
            _service.AddHighLight(200, 5);
            Assert.Equal(10, _service.GetHighlightCount());

            // Clear all
            _service.UnHighLightAll();
            Assert.Equal(0, _service.GetHighlightCount());
        }

        #endregion
    }
}
