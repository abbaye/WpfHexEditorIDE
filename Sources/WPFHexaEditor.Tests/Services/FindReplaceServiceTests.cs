//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using Xunit;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Services
{
    /// <summary>
    /// Unit tests for FindReplaceService
    /// </summary>
    public class FindReplaceServiceTests : IDisposable
    {
        private readonly FindReplaceService _service;
        private readonly SelectionService _selectionService;
        private readonly ByteProvider _provider;
        private readonly string _testFile;
        private readonly byte[] _searchPattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        public FindReplaceServiceTests()
        {
            _service = new FindReplaceService();
            _selectionService = new SelectionService();

            // Create test file with known patterns
            _testFile = Path.GetTempFileName();
            var data = new byte[1024];

            // Fill with sequential bytes
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            // Insert search patterns at known positions
            Array.Copy(_searchPattern, 0, data, 100, _searchPattern.Length);
            Array.Copy(_searchPattern, 0, data, 200, _searchPattern.Length);
            Array.Copy(_searchPattern, 0, data, 300, _searchPattern.Length);

            File.WriteAllBytes(_testFile, data);
            _provider = new ByteProvider(_testFile);
        }

        public void Dispose()
        {
            _provider?.Dispose();
            if (File.Exists(_testFile))
                File.Delete(_testFile);
        }

        #region FindFirst Tests

        [Fact]
        public void FindFirst_ExistingPattern_ReturnsFirstPosition()
        {
            var position = _service.FindFirst(_provider, _searchPattern);
            Assert.Equal(100, position);
        }

        [Fact]
        public void FindFirst_WithStartPosition_ReturnsNextOccurrence()
        {
            var position = _service.FindFirst(_provider, _searchPattern, 150);
            Assert.Equal(200, position);
        }

        [Fact]
        public void FindFirst_NonExistentPattern_ReturnsMinusOne()
        {
            var nonExistentPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var position = _service.FindFirst(_provider, nonExistentPattern);
            Assert.Equal(-1, position);
        }

        [Fact]
        public void FindFirst_NullData_ReturnsMinusOne()
        {
            var position = _service.FindFirst(_provider, null);
            Assert.Equal(-1, position);
        }

        [Fact]
        public void FindFirst_NullProvider_ReturnsMinusOne()
        {
            var position = _service.FindFirst(null, _searchPattern);
            Assert.Equal(-1, position);
        }

        #endregion

        #region FindNext Tests

        [Fact]
        public void FindNext_AfterFirstOccurrence_ReturnsSecondOccurrence()
        {
            var position = _service.FindNext(_provider, _searchPattern, 100);
            Assert.Equal(200, position);
        }

        [Fact]
        public void FindNext_AfterSecondOccurrence_ReturnsThirdOccurrence()
        {
            var position = _service.FindNext(_provider, _searchPattern, 200);
            Assert.Equal(300, position);
        }

        [Fact]
        public void FindNext_AfterLastOccurrence_ReturnsMinusOne()
        {
            var position = _service.FindNext(_provider, _searchPattern, 300);
            Assert.Equal(-1, position);
        }

        #endregion

        #region FindLast Tests

        [Fact]
        public void FindLast_ExistingPattern_ReturnsLastPosition()
        {
            var position = _service.FindLast(_provider, _searchPattern);
            Assert.Equal(300, position);
        }

        [Fact]
        public void FindLast_NonExistentPattern_ReturnsMinusOne()
        {
            var nonExistentPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var position = _service.FindLast(_provider, nonExistentPattern);
            Assert.Equal(-1, position);
        }

        #endregion

        #region FindAll Tests

        [Fact]
        public void FindAll_ExistingPattern_ReturnsAllOccurrences()
        {
            var results = _service.FindAll(_provider, _searchPattern);
            Assert.NotNull(results);

            var positions = results.ToList();
            Assert.Equal(3, positions.Count);
            Assert.Contains(100L, positions);
            Assert.Contains(200L, positions);
            Assert.Contains(300L, positions);
        }

        [Fact]
        public void FindAll_NonExistentPattern_ReturnsEmptyList()
        {
            var nonExistentPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var results = _service.FindAll(_provider, nonExistentPattern);
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public void FindAll_NullData_ReturnsNull()
        {
            var results = _service.FindAll(_provider, null);
            Assert.Null(results);
        }

        [Fact]
        public void FindAll_NullProvider_ReturnsNull()
        {
            var results = _service.FindAll(null, _searchPattern);
            Assert.Null(results);
        }

        #endregion

        #region FindAllCached Tests

        [Fact]
        public void FindAllCached_FirstCall_ReturnsAndCachesResults()
        {
            var results = _service.FindAllCached(_provider, _searchPattern);
            Assert.NotNull(results);

            var positions = results.ToList();
            Assert.Equal(3, positions.Count);
        }

        [Fact]
        public void FindAllCached_SecondCall_ReturnsCachedResults()
        {
            // First call
            var results1 = _service.FindAllCached(_provider, _searchPattern);
            var positions1 = results1.ToList();

            // Second call - should return cached results
            var results2 = _service.FindAllCached(_provider, _searchPattern);
            var positions2 = results2.ToList();

            Assert.Equal(positions1.Count, positions2.Count);
            Assert.Equal(positions1[0], positions2[0]);
        }

        #endregion

        #region ClearCache Tests

        [Fact]
        public void ClearCache_AfterCachedSearch_ClearsCache()
        {
            // Perform cached search
            _service.FindAllCached(_provider, _searchPattern);

            // Clear cache
            _service.ClearCache();

            // Next search should be fresh (not from cache)
            var results = _service.FindAllCached(_provider, _searchPattern);
            Assert.NotNull(results);
        }

        #endregion

        #region ReplaceByte Tests

        [Fact]
        public void ReplaceByte_ValidRange_ReplacesBytes()
        {
            var original = (byte)0x64; // 100 in decimal
            var replace = (byte)0xFF;

            _service.ReplaceByte(_provider, 0, 200, original, replace, false);

            // Verify replacement (position 100 should now be 0xFF)
            var (byteValue, success) = _provider.GetByte(100);
            Assert.True(success);
            Assert.Equal(replace, byteValue.Value);
        }

        [Fact]
        public void ReplaceByte_ReadOnlyMode_DoesNotReplace()
        {
            var original = (byte)0x64;
            var replace = (byte)0xFF;

            _service.ReplaceByte(_provider, 0, 200, original, replace, true);

            // Verify no replacement (position 100 should still be original)
            var (byteValue, success) = _provider.GetByte(100);
            Assert.True(success);
            Assert.Equal(original, byteValue.Value);
        }

        [Fact]
        public void ReplaceByte_NullProvider_DoesNothing()
        {
            // Should not throw
            _service.ReplaceByte(null, 0, 100, 0x00, 0xFF, false);
        }

        #endregion

        #region ReplaceFirst Tests

        [Fact]
        public void ReplaceFirst_ExistingPattern_ReplacesAndReturnsPosition()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var position = _service.ReplaceFirst(_provider, _searchPattern, replaceData, 0, false, false);

            Assert.Equal(100, position);

            // Verify replacement
            var replacedBytes = _selectionService.GetSelectionBytes(_provider, 100, 103);
            Assert.Equal(replaceData, replacedBytes);
        }

        [Fact]
        public void ReplaceFirst_WithTruncate_ReplacesWithTruncatedData()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }; // 6 bytes

            var position = _service.ReplaceFirst(_provider, _searchPattern, replaceData, 0, true, false);

            Assert.Equal(100, position);

            // Verify only 4 bytes were replaced (truncated to match findData length)
            var replacedBytes = _selectionService.GetSelectionBytes(_provider, 100, 103);
            Assert.Equal(replaceData.Take(4).ToArray(), replacedBytes);
        }

        [Fact]
        public void ReplaceFirst_NonExistentPattern_ReturnsMinusOne()
        {
            var nonExistentPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var position = _service.ReplaceFirst(_provider, nonExistentPattern, replaceData, 0, false, false);

            Assert.Equal(-1, position);
        }

        [Fact]
        public void ReplaceFirst_ReadOnlyMode_ReturnsMinusOne()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var position = _service.ReplaceFirst(_provider, _searchPattern, replaceData, 0, false, true);

            Assert.Equal(-1, position);
        }

        #endregion

        #region ReplaceNext Tests

        [Fact]
        public void ReplaceNext_AfterFirstOccurrence_ReplacesSecondOccurrence()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var position = _service.ReplaceNext(_provider, _searchPattern, replaceData, 100, false, false);

            Assert.Equal(200, position);

            // Verify replacement at second occurrence
            var replacedBytes = _selectionService.GetSelectionBytes(_provider, 200, 203);
            Assert.Equal(replaceData, replacedBytes);
        }

        #endregion

        #region ReplaceAll Tests

        [Fact]
        public void ReplaceAll_MultipleOccurrences_ReplacesAll()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var positions = _service.ReplaceAll(_provider, _searchPattern, replaceData, false, false);

            Assert.NotNull(positions);
            var positionList = positions.ToList();
            Assert.Equal(3, positionList.Count);

            // Verify all replacements
            Assert.Equal(replaceData, _selectionService.GetSelectionBytes(_provider, 100, 103));
            Assert.Equal(replaceData, _selectionService.GetSelectionBytes(_provider, 200, 203));
            Assert.Equal(replaceData, _selectionService.GetSelectionBytes(_provider, 300, 303));
        }

        [Fact]
        public void ReplaceAll_NonExistentPattern_ReturnsNull()
        {
            var nonExistentPattern = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var positions = _service.ReplaceAll(_provider, nonExistentPattern, replaceData, false, false);

            Assert.Null(positions);
        }

        [Fact]
        public void ReplaceAll_ReadOnlyMode_ReturnsNull()
        {
            var replaceData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            var positions = _service.ReplaceAll(_provider, _searchPattern, replaceData, false, true);

            Assert.Null(positions);
        }

        #endregion
    }
}
