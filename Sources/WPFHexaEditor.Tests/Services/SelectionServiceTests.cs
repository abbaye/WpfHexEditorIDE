//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;
using Xunit;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Services
{
    /// <summary>
    /// Unit tests for SelectionService
    /// </summary>
    public class SelectionServiceTests : IDisposable
    {
        private readonly SelectionService _service;
        private readonly ByteProvider _provider;
        private readonly string _testFile;

        public SelectionServiceTests()
        {
            _service = new SelectionService();

            // Create test file
            _testFile = Path.GetTempFileName();
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);
            File.WriteAllBytes(_testFile, data);

            _provider = new ByteProvider(_testFile);
        }

        public void Dispose()
        {
            _provider?.Dispose();
            if (File.Exists(_testFile))
                File.Delete(_testFile);
        }

        #region IsValidSelection Tests

        [Fact]
        public void IsValidSelection_ValidSelection_ReturnsTrue()
        {
            Assert.True(_service.IsValidSelection(10, 50));
        }

        [Fact]
        public void IsValidSelection_EqualStartStop_ReturnsTrue()
        {
            Assert.True(_service.IsValidSelection(10, 10));
        }

        [Fact]
        public void IsValidSelection_InvertedSelection_ReturnsFalse()
        {
            Assert.False(_service.IsValidSelection(50, 10));
        }

        [Fact]
        public void IsValidSelection_NegativeStart_ReturnsFalse()
        {
            Assert.False(_service.IsValidSelection(-10, 50));
        }

        [Fact]
        public void IsValidSelection_NegativeStop_ReturnsFalse()
        {
            Assert.False(_service.IsValidSelection(10, -5));
        }

        #endregion

        #region GetSelectionLength Tests

        [Fact]
        public void GetSelectionLength_ValidSelection_ReturnsCorrectLength()
        {
            var length = _service.GetSelectionLength(10, 50);
            Assert.Equal(41, length);
        }

        [Fact]
        public void GetSelectionLength_SingleByte_ReturnsOne()
        {
            var length = _service.GetSelectionLength(10, 10);
            Assert.Equal(1, length);
        }

        [Fact]
        public void GetSelectionLength_InvertedSelection_ReturnsPositiveLength()
        {
            var length = _service.GetSelectionLength(50, 10);
            Assert.Equal(41, length); // Service handles inverted selections
        }

        #endregion

        #region FixSelectionRange Tests

        [Fact]
        public void FixSelectionRange_ValidSelection_ReturnsUnchanged()
        {
            var (start, stop) = _service.FixSelectionRange(10, 50);
            Assert.Equal(10, start);
            Assert.Equal(50, stop);
        }

        [Fact]
        public void FixSelectionRange_InvertedSelection_ReturnsFixed()
        {
            var (start, stop) = _service.FixSelectionRange(50, 10);
            Assert.Equal(10, start);
            Assert.Equal(50, stop);
        }

        [Fact]
        public void FixSelectionRange_EqualValues_ReturnsUnchanged()
        {
            var (start, stop) = _service.FixSelectionRange(25, 25);
            Assert.Equal(25, start);
            Assert.Equal(25, stop);
        }

        #endregion

        #region ValidateSelection Tests

        [Fact]
        public void ValidateSelection_ValidSelection_ReturnsUnchanged()
        {
            var (start, stop) = _service.ValidateSelection(_provider, 10, 50);
            Assert.Equal(10, start);
            Assert.Equal(50, stop);
        }

        [Fact]
        public void ValidateSelection_NegativeStart_ClampsToMinusOne()
        {
            var (start, stop) = _service.ValidateSelection(_provider, -10, 50);
            Assert.Equal(-1, start);
            Assert.Equal(50, stop);
        }

        [Fact]
        public void ValidateSelection_ExceedsLength_ClampsToMax()
        {
            var (start, stop) = _service.ValidateSelection(_provider, 10, _provider.Length + 100);
            Assert.Equal(10, start);
            Assert.Equal(_provider.Length - 1, stop);
        }

        [Fact]
        public void ValidateSelection_BothOutOfBounds_ClampsToValid()
        {
            var (start, stop) = _service.ValidateSelection(_provider, -10, _provider.Length + 100);
            Assert.Equal(-1, start);
            Assert.Equal(_provider.Length - 1, stop);
        }

        #endregion

        #region GetSelectionBytes Tests

        [Fact]
        public void GetSelectionBytes_ValidSelection_ReturnsCorrectBytes()
        {
            var bytes = _service.GetSelectionBytes(_provider, 0, 9);
            Assert.NotNull(bytes);
            Assert.Equal(10, bytes.Length);
            Assert.Equal(0, bytes[0]);
            Assert.Equal(9, bytes[9]);
        }

        [Fact]
        public void GetSelectionBytes_SingleByte_ReturnsOneElement()
        {
            var bytes = _service.GetSelectionBytes(_provider, 5, 5);
            Assert.NotNull(bytes);
            Assert.Equal(1, bytes.Length);
            Assert.Equal(5, bytes[0]);
        }

        [Fact]
        public void GetSelectionBytes_NullProvider_ReturnsNull()
        {
            var bytes = _service.GetSelectionBytes(null, 0, 10);
            Assert.Null(bytes);
        }

        [Fact]
        public void GetSelectionBytes_InvalidRange_ReturnsNull()
        {
            var bytes = _service.GetSelectionBytes(_provider, 50, 10);
            Assert.Null(bytes);
        }

        #endregion

        #region GetAllBytes Tests

        [Fact]
        public void GetAllBytes_ValidProvider_ReturnsAllBytes()
        {
            var bytes = _service.GetAllBytes(_provider);
            Assert.NotNull(bytes);
            Assert.Equal(_provider.Length, bytes.Length);
        }

        [Fact]
        public void GetAllBytes_NullProvider_ReturnsNull()
        {
            var bytes = _service.GetAllBytes(null);
            Assert.Null(bytes);
        }

        #endregion

        #region HasSelection Tests

        [Fact]
        public void HasSelection_ValidSelection_ReturnsTrue()
        {
            Assert.True(_service.HasSelection(10, 50));
        }

        [Fact]
        public void HasSelection_NoSelection_ReturnsFalse()
        {
            Assert.False(_service.HasSelection(-1, -1));
        }

        #endregion

        #region IsAllSelected Tests

        [Fact]
        public void IsAllSelected_EntireFile_ReturnsTrue()
        {
            var start = _service.GetSelectAllStart(_provider);
            var stop = _service.GetSelectAllStop(_provider);
            Assert.True(_service.IsAllSelected(_provider, start, stop));
        }

        [Fact]
        public void IsAllSelected_PartialSelection_ReturnsFalse()
        {
            Assert.False(_service.IsAllSelected(_provider, 10, 50));
        }

        #endregion

        #region ExtendSelection Tests

        [Fact]
        public void ExtendSelection_PositiveOffset_ExtendsCorrectly()
        {
            var newPosition = _service.ExtendSelection(_provider, 50, 10);
            Assert.Equal(60, newPosition);
        }

        [Fact]
        public void ExtendSelection_NegativeOffset_ShrinksCorrectly()
        {
            var newPosition = _service.ExtendSelection(_provider, 50, -5);
            Assert.Equal(45, newPosition);
        }

        [Fact]
        public void ExtendSelection_ExceedsBounds_ClampsToMax()
        {
            var newPosition = _service.ExtendSelection(_provider, 50, 10000);
            Assert.Equal(_provider.Length - 1, newPosition);
        }

        [Fact]
        public void ExtendSelection_NegativeResult_ClampsToZero()
        {
            var newPosition = _service.ExtendSelection(_provider, 10, -50);
            Assert.Equal(0, newPosition);
        }

        #endregion

        #region GetSelectionByte Tests

        [Fact]
        public void GetSelectionByte_ValidPosition_ReturnsByte()
        {
            var byteValue = _service.GetSelectionByte(_provider, 5);
            Assert.NotNull(byteValue);
            Assert.Equal((byte)5, byteValue.Value);
        }

        [Fact]
        public void GetSelectionByte_OutOfBounds_ReturnsNull()
        {
            var byteValue = _service.GetSelectionByte(_provider, _provider.Length + 100);
            Assert.Null(byteValue);
        }

        [Fact]
        public void GetSelectionByte_NullProvider_ReturnsNull()
        {
            var byteValue = _service.GetSelectionByte(null, 5);
            Assert.Null(byteValue);
        }

        #endregion
    }
}
