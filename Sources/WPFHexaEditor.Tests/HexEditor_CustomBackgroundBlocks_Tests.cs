//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.HexEditor;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;

namespace WpfHexaEditor.Tests
{
    [TestClass]
    public class HexEditor_CustomBackgroundBlocks_Tests
    {
        private HexEditor _hexEditor;
        private Dispatcher _dispatcher;

        [TestInitialize]
        public void Setup()
        {
            // Create HexEditor on STA thread (WPF requirement)
            var thread = new Thread(() =>
            {
                _hexEditor = new HexEditor();
                _dispatcher = Dispatcher.CurrentDispatcher;
                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            Thread.Sleep(100); // Wait for dispatcher
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dispatcher?.InvokeShutdown();
        }

        private void InvokeOnDispatcher(System.Action action)
        {
            _dispatcher?.Invoke(action, DispatcherPriority.Normal);
        }

        private T InvokeOnDispatcher<T>(System.Func<T> func)
        {
            return _dispatcher != null ?
                _dispatcher.Invoke(func, DispatcherPriority.Normal) :
                default(T);
        }

        [TestMethod]
        public void AddCustomBackgroundBlock_RaisesEvent()
        {
            CustomBackgroundBlockEventArgs capturedArgs = null;

            InvokeOnDispatcher(() =>
            {
                _hexEditor.CustomBackgroundBlockChanged += (s, e) => capturedArgs = e;

                var block = new CustomBackgroundBlock(0, 100, Brushes.Red);
                _hexEditor.AddCustomBackgroundBlock(block);
            });

            Thread.Sleep(50); // Allow event propagation

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(BlockChangeType.Added, capturedArgs.ChangeType);
        }

        [TestMethod]
        public void ServiceIntegration_QueryWorks()
        {
            var result = InvokeOnDispatcher(() =>
            {
                var block = new CustomBackgroundBlock(100, 50, Brushes.Red);
                _hexEditor.AddCustomBackgroundBlock(block);

                return _hexEditor.CustomBackgroundService.GetBlockAt(125);
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(100L, result.StartOffset);
        }

        [TestMethod]
        public void ClearAll_RemovesAllBlocks()
        {
            var count = InvokeOnDispatcher(() =>
            {
                _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(0, 100, Brushes.Red));
                _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(200, 50, Brushes.Blue));

                _hexEditor.ClearCustomBackgroundBlock();

                return _hexEditor.CustomBackgroundService.GetBlockCount();
            });

            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void ServiceMethods_WorkThroughHexEditor()
        {
            var hasBlockAt150 = InvokeOnDispatcher(() =>
            {
                _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(100, 100, Brushes.Red));

                return _hexEditor.CustomBackgroundService.HasBlockAt(150);
            });

            Assert.IsTrue(hasBlockAt150);
        }
    }
}
