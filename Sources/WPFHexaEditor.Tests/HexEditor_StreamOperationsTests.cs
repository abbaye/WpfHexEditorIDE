using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using WpfHexaEditor;

namespace WpfHexaEditor.Tests
{
    /// <summary>
    /// Integration tests for HexEditor.OpenStream() and HexEditor.OpenMemory()
    /// Tests the newly implemented stream operations at HexEditor control level
    ///
    /// Note: These tests run on STA thread with WPF dispatcher to properly test the full control
    /// </summary>
    [TestClass]
    public class HexEditor_StreamOperationsTests
    {
        private HexEditor _hexEditor;
        private Dispatcher _dispatcher;

        #region Test Setup/Cleanup

        [TestInitialize]
        public void TestInitialize()
        {
            // Create HexEditor on STA thread with dispatcher
            var thread = new Thread(() =>
            {
                _hexEditor = new HexEditor();
                _dispatcher = Dispatcher.CurrentDispatcher;
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            // Wait for dispatcher to be ready
            Thread.Sleep(100);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_dispatcher != null && !_dispatcher.HasShutdownStarted)
            {
                _dispatcher.InvokeShutdown();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Execute action on dispatcher thread and wait for completion
        /// </summary>
        private void InvokeOnDispatcher(Action action)
        {
            if (_dispatcher != null)
            {
                _dispatcher.Invoke(action, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// Execute function on dispatcher thread and return result
        /// </summary>
        private T InvokeOnDispatcher<T>(Func<T> func)
        {
            if (_dispatcher != null)
            {
                return _dispatcher.Invoke(func, DispatcherPriority.Normal);
            }
            return default(T);
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

        #region OpenMemory Tests

        [TestMethod]
        public void OpenMemory_ValidData_LoadsSuccessfully()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenMemory(testData);

                // Verify data loaded
                Assert.AreEqual(testData.Length, _hexEditor.Length);
                Assert.IsFalse(_hexEditor.IsModified);
            });
        }

        [TestMethod]
        public void OpenMemory_ReadOnlyMode_PreventModifications()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenMemory(testData, readOnly: true);

                // Verify read-only state
                Assert.IsTrue(_hexEditor.ReadOnlyMode);
            });
        }

        [TestMethod]
        public void OpenMemory_NullData_ThrowsException()
        {
            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                try
                {
                    _hexEditor.OpenMemory(null);
                    Assert.Fail("Expected ArgumentNullException was not thrown");
                }
                catch (ArgumentNullException)
                {
                    // Expected exception
                }
            });
        }

        [TestMethod]
        public void OpenMemory_EmptyArray_LoadsSuccessfully()
        {
            // Arrange
            var emptyData = new byte[0];

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenMemory(emptyData);

                Assert.AreEqual(0, _hexEditor.Length);
            });
        }

        [TestMethod]
        public void OpenMemory_LargeData_LoadsSuccessfully()
        {
            // Arrange
            var largeData = CreateTestData(100000); // 100KB

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                var startTime = DateTime.Now;
                _hexEditor.OpenMemory(largeData);
                var elapsed = DateTime.Now - startTime;

                Assert.AreEqual(100000, _hexEditor.Length);
                Assert.IsTrue(elapsed.TotalSeconds < 2.0,
                    $"OpenMemory took too long: {elapsed.TotalSeconds}s");
            });
        }

        [TestMethod]
        public void OpenMemory_GetByte_ReturnsCorrectData()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenMemory(testData);

                // Verify data retrieval
                var result = _hexEditor.GetByte(2, true);
                Assert.IsTrue(result.success);
                Assert.AreEqual((byte)0x22, result.singleByte);
            });
        }

        #endregion

        #region OpenStream Tests

        [TestMethod]
        public void OpenStream_MemoryStream_LoadsSuccessfully()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
            var stream = new MemoryStream(testData);

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(stream);

                // Verify data loaded
                Assert.AreEqual(testData.Length, _hexEditor.Length);
                Assert.IsFalse(_hexEditor.IsModified);
            });
        }

        [TestMethod]
        public void OpenStream_FileStream_LoadsSuccessfully()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var testData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            File.WriteAllBytes(tempFile, testData);

            try
            {
                // Act & Assert
                InvokeOnDispatcher(() =>
                {
                    using (var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read))
                    {
                        _hexEditor.OpenStream(fileStream);

                        Assert.AreEqual(testData.Length, _hexEditor.Length);
                    }
                });
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void OpenStream_ReadOnlyMode_PreventModifications()
        {
            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var stream = new MemoryStream(testData);

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(stream, readOnly: true);

                Assert.IsTrue(_hexEditor.ReadOnlyMode);
            });
        }

        [TestMethod]
        public void OpenStream_NullStream_ThrowsException()
        {
            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                try
                {
                    _hexEditor.OpenStream(null);
                    Assert.Fail("Expected ArgumentNullException was not thrown");
                }
                catch (ArgumentNullException)
                {
                    // Expected exception
                }
            });
        }

        [TestMethod]
        public void OpenStream_EmptyStream_LoadsSuccessfully()
        {
            // Arrange
            var emptyStream = new MemoryStream();

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(emptyStream);

                Assert.AreEqual(0, _hexEditor.Length);
            });
        }

        [TestMethod]
        public void OpenStream_GetByte_ReturnsCorrectData()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            var stream = new MemoryStream(testData);

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(stream);

                // Verify data retrieval
                var result = _hexEditor.GetByte(3, true);
                Assert.IsTrue(result.success);
                Assert.AreEqual((byte)0x33, result.singleByte);
            });
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_OpenMemory_ModifyAndRetrieve()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenMemory(testData);

                // Modify a byte
                _hexEditor.ModifyByte(0xFF, 2);

                // Retrieve modified byte
                var result = _hexEditor.GetByte(2, true);
                Assert.IsTrue(result.success);
                Assert.AreEqual((byte)0xFF, result.singleByte);

                // Check modification tracking
                Assert.IsTrue(_hexEditor.IsModified);
            });
        }

        [TestMethod]
        public void Integration_OpenStream_GetAllBytes()
        {
            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33 };
            var stream = new MemoryStream(testData);

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(stream);

                // Get all bytes
                var allBytes = _hexEditor.GetAllBytes(true);

                Assert.IsNotNull(allBytes);
                Assert.AreEqual(4, allBytes.Length);
                CollectionAssert.AreEqual(testData, allBytes);
            });
        }

        [TestMethod]
        public void Integration_OpenMemory_ThenOpenAnother_ClosesFirst()
        {
            // Arrange
            var firstData = new byte[] { 0x00, 0x11, 0x22 };
            var secondData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                // Open first
                _hexEditor.OpenMemory(firstData);
                Assert.AreEqual(3, _hexEditor.Length);

                // Open second (should close first)
                _hexEditor.OpenMemory(secondData);
                Assert.AreEqual(4, _hexEditor.Length);

                // Verify second data is loaded
                var result = _hexEditor.GetByte(0, true);
                Assert.AreEqual((byte)0xAA, result.singleByte);
            });
        }

        #endregion

        #region V1 Compatibility Tests

        [TestMethod]
        public void V1Compatibility_OpenMemory_ReplacesStreamSetter()
        {
            // This test verifies that OpenMemory() provides the same functionality
            // as the V1 pattern: editor.Stream = new MemoryStream(data)

            // Arrange
            var testData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                // V1 would do: editor.Stream = new MemoryStream(testData);
                // V2 does: editor.OpenMemory(testData);
                _hexEditor.OpenMemory(testData);

                // Verify same behavior: data is loaded and accessible
                Assert.AreEqual(testData.Length, _hexEditor.Length);

                for (int i = 0; i < testData.Length; i++)
                {
                    var result = _hexEditor.GetByte(i, true);
                    Assert.IsTrue(result.success);
                    Assert.AreEqual(testData[i], result.singleByte,
                        $"Byte at position {i} should match");
                }
            });
        }

        [TestMethod]
        public void V1Compatibility_OpenStream_AcceptsMemoryStream()
        {
            // V1 allowed: editor.Stream = new MemoryStream(data);
            // V2 allows: editor.OpenStream(new MemoryStream(data));

            // Arrange
            var testData = new byte[] { 0xAA, 0xBB, 0xCC };
            var memoryStream = new MemoryStream(testData);

            // Act & Assert
            InvokeOnDispatcher(() =>
            {
                _hexEditor.OpenStream(memoryStream);

                Assert.AreEqual(testData.Length, _hexEditor.Length);
                Assert.IsFalse(_hexEditor.IsModified);
            });
        }

        #endregion
    }
}
