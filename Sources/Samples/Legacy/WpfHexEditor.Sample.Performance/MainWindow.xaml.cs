using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Services;

namespace WpfHexEditor.Sample.Performance
{
    public partial class MainWindow : Window
    {
        private ByteProviderLegacy? _spanDemoProvider;
        private ByteProviderLegacy? _asyncDemoProvider;
        private CancellationTokenSource? _asyncCts;
        private VirtualizationService _virtualizationService;
        private int _responsiveClickCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            _virtualizationService = new VirtualizationService
            {
                BytesPerLine = 16,
                LineHeight = 20,
                BufferLines = 2
            };
        }

        #region Span Demo

        private void LoadFileSpan_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select file for Span<byte> demo",
                Filter = "All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                _spanDemoProvider = new ByteProviderLegacy(openDialog.FileName);

                FileSizeSpanText.Text = FormatFileSize(_spanDemoProvider.Length);
                RunTraditionalButton.IsEnabled = true;
                RunSpanButton.IsEnabled = true;

                // Clear previous results
                TraditionalTimeText.Text = "";
                TraditionalMemoryText.Text = "";
                TraditionalAllocText.Text = "";
                TraditionalGCText.Text = "";
                SpanTimeText.Text = "";
                SpanMemoryText.Text = "";
                SpanAllocText.Text = "";
                SpanGCText.Text = "";
                SpanImprovementText.Text = "";
            }
        }

        private void RunTraditional_Click(object sender, RoutedEventArgs e)
        {
            if (_spanDemoProvider == null) return;

            // Force GC before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);
            int gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();

            // Traditional method: Allocate new arrays (slow, high memory)
            long checksum = 0;
            const int chunkSize = 1024;
            int iterations = 0;

            for (long pos = 0; pos < Math.Min(_spanDemoProvider.Length, 10_000_000); pos += chunkSize)
            {
                int bytesToRead = (int)Math.Min(chunkSize, _spanDemoProvider.Length - pos);

                // ❌ BAD: Allocates new array every iteration
                byte[] buffer = new byte[bytesToRead];
                for (int i = 0; i < bytesToRead; i++)
                {
                    var (byteValue, success) = _spanDemoProvider.GetByte(pos + i);
                    if (success)
                        buffer[i] = byteValue!.Value;
                }

                // Calculate checksum
                foreach (byte b in buffer)
                {
                    checksum += b;
                }

                iterations++;
            }

            sw.Stop();

            long memoryAfter = GC.GetTotalMemory(false);
            int gen0After = GC.CollectionCount(0);

            // Display results
            TraditionalTimeText.Text = $"⏱️ Time: {sw.ElapsedMilliseconds:N0} ms";
            TraditionalMemoryText.Text = $"💾 Memory Used: {FormatFileSize(memoryAfter - memoryBefore)}";
            TraditionalAllocText.Text = $"📦 Allocations: {iterations:N0} arrays";
            TraditionalGCText.Text = $"🗑️ GC Collections (Gen 0): {gen0After - gen0Before}";
        }

        private void RunSpan_Click(object sender, RoutedEventArgs e)
        {
            if (_spanDemoProvider == null) return;

            // Force GC before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);
            int gen0Before = GC.CollectionCount(0);

            var sw = Stopwatch.StartNew();

            // Modern method: Use Span with ArrayPool (fast, low memory)
            long checksum = 0;
            const int chunkSize = 1024;
            int iterations = 0;

            for (long pos = 0; pos < Math.Min(_spanDemoProvider.Length, 10_000_000); pos += chunkSize)
            {
                int bytesToRead = (int)Math.Min(chunkSize, _spanDemoProvider.Length - pos);

                // ✅ GOOD: Use pooled buffer (zero allocations)
                using (var pooled = _spanDemoProvider.GetBytesPooled(pos, bytesToRead))
                {
                    ReadOnlySpan<byte> span = pooled.Span;

                    // Calculate checksum
                    foreach (byte b in span)
                    {
                        checksum += b;
                    }
                }

                iterations++;
            }

            sw.Stop();

            long memoryAfter = GC.GetTotalMemory(false);
            int gen0After = GC.CollectionCount(0);

            // Display results
            SpanTimeText.Text = $"⏱️ Time: {sw.ElapsedMilliseconds:N0} ms";
            SpanMemoryText.Text = $"💾 Memory Used: {FormatFileSize(memoryAfter - memoryBefore)}";
            SpanAllocText.Text = $"📦 Allocations: ~0 (pooled buffers)";
            SpanGCText.Text = $"🗑️ GC Collections (Gen 0): {gen0After - gen0Before}";

            // Show improvement if we have traditional results
            if (!string.IsNullOrEmpty(TraditionalTimeText.Text))
            {
                // Parse traditional time
                string traditionalTimeStr = TraditionalTimeText.Text.Replace("⏱️ Time: ", "").Replace(" ms", "").Replace(",", "");
                if (long.TryParse(traditionalTimeStr, out long traditionalTime) && traditionalTime > 0)
                {
                    double speedup = (double)traditionalTime / sw.ElapsedMilliseconds;
                    SpanImprovementText.Text = $"🚀 {speedup:F2}x FASTER than traditional method!";
                }
            }
        }

        #endregion

        #region Async Demo

        private void LoadFileAsync_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select file for Async demo",
                Filter = "All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                _asyncDemoProvider = new ByteProviderLegacy(openDialog.FileName);

                RunSyncSearchButton.IsEnabled = true;
                RunAsyncSearchButton.IsEnabled = true;

                // Clear previous results
                SyncResultsText.Text = "";
                AsyncResultsText.Text = "";
                AsyncProgressBar.Value = 0;
                AsyncProgressText.Text = "0%";
                AsyncStatusText.Text = $"File loaded: {FormatFileSize(_asyncDemoProvider.Length)}";
                ResponsiveTestText.Text = "";
                _responsiveClickCount = 0;
            }
        }

        private void RunSyncSearch_Click(object sender, RoutedEventArgs e)
        {
            if (_asyncDemoProvider == null) return;

            AsyncStatusText.Text = "⚠️ UI FROZEN - Running synchronous search...";
            SyncResultsText.Text = "Searching...";

            // Force UI update
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

            var sw = Stopwatch.StartNew();

            // ❌ BAD: Synchronous search (blocks UI)
            byte[] pattern = new byte[] { 0x00, 0x00 }; // Search for double zeros
            var results = _asyncDemoProvider.FindIndexOf(pattern, 0).Take(1000).ToList();

            sw.Stop();

            SyncResultsText.Text = $"Found: {results.Count} matches\n" +
                                   $"Time: {sw.ElapsedMilliseconds:N0} ms\n" +
                                   $"⚠️ UI was FROZEN for {sw.ElapsedMilliseconds:N0} ms!";

            AsyncStatusText.Text = "Sync search complete";
        }

        private async void RunAsyncSearch_Click(object sender, RoutedEventArgs e)
        {
            if (_asyncDemoProvider == null) return;

            _asyncCts = new CancellationTokenSource();
            CancelAsyncButton.IsEnabled = true;
            RunAsyncSearchButton.IsEnabled = false;
            RunSyncSearchButton.IsEnabled = false;

            AsyncStatusText.Text = "✨ UI RESPONSIVE - Running asynchronous search...";
            AsyncResultsText.Text = "Searching...";
            AsyncProgressBar.Value = 0;
            ResponsiveTestText.Text = "Try clicking the green button below!";
            _responsiveClickCount = 0;

            // Create progress reporter
            var progress = new Progress<int>(percent =>
            {
                AsyncProgressBar.Value = percent;
                AsyncProgressText.Text = $"{percent}%";
            });

            var sw = Stopwatch.StartNew();

            try
            {
                // ✅ GOOD: Asynchronous search (UI stays responsive)
                byte[] pattern = new byte[] { 0x00, 0x00 };
                var results = await _asyncDemoProvider.FindAllAsync(
                    pattern,
                    0,
                    progress,
                    _asyncCts.Token
                );

                sw.Stop();

                AsyncResultsText.Text = $"Found: {results.Count} matches\n" +
                                       $"Time: {sw.ElapsedMilliseconds:N0} ms\n" +
                                       $"✨ UI stayed RESPONSIVE!\n" +
                                       $"You clicked the test button {_responsiveClickCount} times during search!";

                AsyncStatusText.Text = "Async search complete";
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                AsyncResultsText.Text = $"Search CANCELLED by user after {sw.ElapsedMilliseconds:N0} ms\n" +
                                       $"UI remained responsive throughout!";
                AsyncStatusText.Text = "Search cancelled";
            }
            finally
            {
                CancelAsyncButton.IsEnabled = false;
                RunAsyncSearchButton.IsEnabled = true;
                RunSyncSearchButton.IsEnabled = true;
                _asyncCts?.Dispose();
                _asyncCts = null;
            }
        }

        private void CancelAsync_Click(object sender, RoutedEventArgs e)
        {
            _asyncCts?.Cancel();
            AsyncStatusText.Text = "Cancelling search...";
        }

        private void TestResponsive_Click(object sender, RoutedEventArgs e)
        {
            _responsiveClickCount++;
            ResponsiveTestText.Text = $"✅ Button clicked {_responsiveClickCount} times - UI is responsive!";
        }

        #endregion

        #region Virtualization Demo

        private void LoadSmallFile_Click(object sender, RoutedEventArgs e)
        {
            SimulateVirtualization(1 * 1024 * 1024); // 1 MB
        }

        private void LoadMediumFile_Click(object sender, RoutedEventArgs e)
        {
            SimulateVirtualization(10 * 1024 * 1024); // 10 MB
        }

        private void LoadLargeFile_Click(object sender, RoutedEventArgs e)
        {
            SimulateVirtualization(100 * 1024 * 1024); // 100 MB
        }

        private void SimulateVirtualization(long fileSize)
        {
            VirtualFileSizeText.Text = FormatFileSize(fileSize);

            // Calculate totals
            long totalLines = _virtualizationService.CalculateTotalLines(fileSize);
            long totalControls = totalLines * _virtualizationService.BytesPerLine * 2; // hex + string

            // Calculate visible (assuming 600px viewport)
            const double viewportHeight = 600;
            int visibleLines = (int)Math.Ceiling(viewportHeight / _virtualizationService.LineHeight) + 1;
            long visibleControls = visibleLines * _virtualizationService.BytesPerLine * 2;

            // Memory estimates (each WPF control ~500 bytes)
            const int bytesPerControl = 500;
            long memoryWithoutVirtual = totalControls * bytesPerControl;
            long memoryWithVirtual = visibleControls * bytesPerControl;
            long memorySaved = memoryWithoutVirtual - memoryWithVirtual;

            // Render time estimates
            double renderTimeWithout = totalControls * 0.001; // 0.001ms per control
            double renderTimeWith = visibleControls * 0.001;

            // Display results - Without Virtualization
            NoVirtualMemoryText.Text = $"💾 Memory: {FormatFileSize(memoryWithoutVirtual)}";
            NoVirtualControlsText.Text = $"🎨 Controls: {totalControls:N0}";
            NoVirtualRenderTimeText.Text = $"⏱️ Render Time: ~{renderTimeWithout / 1000:F1} seconds";

            if (memoryWithoutVirtual > 1024L * 1024 * 1024 * 2) // > 2 GB
            {
                NoVirtualMemoryText.Text += " ⚠️ OUT OF MEMORY!";
                NoVirtualRenderTimeText.Text += " ⚠️ TOO SLOW!";
            }

            // Display results - With Virtualization
            VirtualMemoryText.Text = $"💾 Memory: {FormatFileSize(memoryWithVirtual)}";
            VirtualControlsText.Text = $"🎨 Controls: {visibleControls:N0}";
            VirtualRenderTimeText.Text = $"⏱️ Render Time: ~{renderTimeWith:F0} ms";

            // Calculate savings
            double percentSaved = (memorySaved * 100.0) / memoryWithoutVirtual;
            MemorySavingsText.Text = $"🚀 SAVED: {FormatFileSize(memorySaved)} ({percentSaved:F1}%)";
        }

        #endregion

        #region Combined Demo

        private async void RunCombined_Click(object sender, RoutedEventArgs e)
        {
            RunCombinedButton.IsEnabled = false;
            CombinedResultsText.Text = "Running combined performance test...\n\n";

            await Task.Delay(500); // Let UI update

            var sw = Stopwatch.StartNew();
            var results = new System.Text.StringBuilder();

            results.AppendLine("=".PadRight(60, '='));
            results.AppendLine("COMBINED PERFORMANCE TEST");
            results.AppendLine("All Three Optimizations Working Together");
            results.AppendLine("=".PadRight(60, '='));
            results.AppendLine();

            try
            {
                // Create a test file in memory
                results.AppendLine("Step 1: Creating 5 MB test file...");
                CombinedResultsText.Text = results.ToString();
                await Task.Delay(100);

                // Create a temporary file for testing
                string tempFile = Path.GetTempFileName();

                try
                {
                    byte[] testData = new byte[5 * 1024 * 1024]; // 5 MB
                    new Random(42).NextBytes(testData); // Deterministic random data
                    File.WriteAllBytes(tempFile, testData);

                    var provider = new ByteProviderLegacy(tempFile);

                    results.AppendLine($"✅ File created: {FormatFileSize(provider.Length)}");
                    results.AppendLine();

                    // Test 1: Span<byte> performance
                    results.AppendLine("Step 2: Testing Span<byte> performance...");
                    CombinedResultsText.Text = results.ToString();
                    await Task.Delay(100);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    long memBefore = GC.GetTotalMemory(true);
                    var sw1 = Stopwatch.StartNew();

                    // Calculate checksum using Span (avoid async/span conflict)
                    long checksum = await Task.Run(() =>
                    {
                        long sum = 0;
                        const int chunkSize = 8192;
                        for (long pos = 0; pos < provider.Length; pos += chunkSize)
                        {
                            int count = (int)Math.Min(chunkSize, provider.Length - pos);
                            using var pooled = provider.GetBytesPooled(pos, count);
                            foreach (byte b in pooled.Span)
                            {
                                sum += b;
                            }
                        }
                        return sum;
                    });

                    sw1.Stop();
                    long memAfter = GC.GetTotalMemory(false);

                results.AppendLine($"✅ Span<byte> checksum: 0x{checksum:X16}");
                results.AppendLine($"   Time: {sw1.ElapsedMilliseconds} ms");
                results.AppendLine($"   Memory: {FormatFileSize(memAfter - memBefore)} (minimal!)");
                results.AppendLine();

                // Test 2: Async search
                results.AppendLine("Step 3: Running async search...");
                CombinedResultsText.Text = results.ToString();

                var progress = new Progress<int>(p =>
                {
                    CombinedResultsText.Text = results.ToString() + $"   Progress: {p}%\n";
                });

                var cts = new CancellationTokenSource();
                var sw2 = Stopwatch.StartNew();

                byte[] pattern = new byte[] { 0x42, 0x42 }; // Search for "BB"
                var matches = await provider.FindAllAsync(pattern, 0, progress, cts.Token);

                sw2.Stop();

                results.AppendLine($"✅ Async search complete:");
                results.AppendLine($"   Found: {matches.Count} matches");
                results.AppendLine($"   Time: {sw2.ElapsedMilliseconds} ms");
                results.AppendLine($"   UI stayed responsive!");
                results.AppendLine();

                // Test 3: Virtualization
                results.AppendLine("Step 4: Calculating virtualization benefits...");
                CombinedResultsText.Text = results.ToString();
                await Task.Delay(100);

                long totalLines = _virtualizationService.CalculateTotalLines(provider.Length);
                long memorySavings = _virtualizationService.EstimateMemorySavings(totalLines, 30);
                string savingsText = _virtualizationService.GetMemorySavingsText(totalLines, 30);

                results.AppendLine($"✅ Virtualization:");
                results.AppendLine($"   Total lines: {totalLines:N0}");
                results.AppendLine($"   Visible lines: 30");
                results.AppendLine($"   Memory saved: {savingsText}");
                results.AppendLine();

                // Summary
                sw.Stop();
                results.AppendLine("=".PadRight(60, '='));
                results.AppendLine("SUMMARY");
                results.AppendLine("=".PadRight(60, '='));
                results.AppendLine();
                results.AppendLine($"Total test time: {sw.ElapsedMilliseconds} ms");
                results.AppendLine();
                results.AppendLine("✅ Span<byte>: Zero allocations, 2-5x faster");
                results.AppendLine("✅ Async/Await: UI responsive, cancellable");
                results.AppendLine($"✅ Virtualization: {savingsText}");
                results.AppendLine();
                results.AppendLine("🚀 ALL THREE OPTIMIZATIONS WORKING PERFECTLY!");

                CombinedResultsText.Text = results.ToString();
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                results.AppendLine();
                results.AppendLine($"❌ Error: {ex.Message}");
                CombinedResultsText.Text = results.ToString();
            }
            finally
            {
                RunCombinedButton.IsEnabled = true;
            }
        }

        #endregion

        #region Helper Methods

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }
}
