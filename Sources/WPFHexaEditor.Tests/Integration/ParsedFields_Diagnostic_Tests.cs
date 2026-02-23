//////////////////////////////////////////////
// Apache 2.0  - 2026
// Diagnostic test to troubleshoot ParsedFields panel
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexaEditor;

namespace WPFHexaEditor.Tests.Integration
{
    [TestClass]
    public class ParsedFields_Diagnostic_Tests
    {
        [TestMethod]
        public void DiagnosticTest_ListAllEmbeddedResources()
        {
            // Get the HexEditor assembly
            var assembly = typeof(HexEditor).Assembly;

            // Get all embedded resource names
            var resourceNames = assembly.GetManifestResourceNames();

            Console.WriteLine($"Total embedded resources: {resourceNames.Length}");
            Console.WriteLine("\n=== All Embedded Resources ===");
            foreach (var name in resourceNames)
            {
                Console.WriteLine($"  - {name}");
            }

            // Filter for FormatDefinitions JSON files
            var formatResources = resourceNames
                .Where(r => r.Contains("FormatDefinitions") && r.EndsWith(".json"))
                .ToList();

            Console.WriteLine($"\n=== FormatDefinitions JSON Resources ===");
            Console.WriteLine($"Count: {formatResources.Count}");
            foreach (var name in formatResources)
            {
                Console.WriteLine($"  - {name}");
            }

            // Try to load one as a test
            if (formatResources.Count > 0)
            {
                var testResource = formatResources[0];
                Console.WriteLine($"\n=== Testing Load of: {testResource} ===");

                try
                {
                    using var stream = assembly.GetManifestResourceStream(testResource);
                    if (stream != null)
                    {
                        using var reader = new System.IO.StreamReader(stream);
                        var json = reader.ReadToEnd();
                        Console.WriteLine($"SUCCESS! Loaded {json.Length} characters");
                        Console.WriteLine($"First 200 chars: {json.Substring(0, Math.Min(200, json.Length))}...");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Stream is null!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.WriteLine($"Stack: {ex.StackTrace}");
                }
            }
        }

        [TestMethod]
        public void DiagnosticTest_HexEditorLoadEmbeddedFormats()
        {
            var hexEditor = new HexEditor();

            Console.WriteLine("=== Testing HexEditor.LoadEmbeddedFormatDefinitions() ===");

            var count = hexEditor.LoadEmbeddedFormatDefinitions();

            Console.WriteLine($"Formats loaded: {count}");
            Console.WriteLine($"LoadedFormatCount property: {hexEditor.LoadedFormatCount}");
            Console.WriteLine($"HasLoadedFormats: {hexEditor.HasLoadedFormats}");

            if (count > 0)
            {
                var formats = hexEditor.LoadedFormats;
                Console.WriteLine($"\n=== First 10 Loaded Formats ===");
                foreach (var format in formats.Take(10))
                {
                    Console.WriteLine($"  - {format.FormatName} ({string.Join(", ", format.FileExtensions ?? new string[0])})");
                }

                // Get statistics
                var stats = hexEditor.GetFormatStatistics();
                Console.WriteLine($"\n=== Statistics ===");
                Console.WriteLine($"Total Formats: {stats.TotalFormats}");
                Console.WriteLine($"Total Extensions: {stats.TotalExtensions}");
                Console.WriteLine($"Categories: {stats.Categories?.Count ?? 0}");
            }
            else
            {
                Console.WriteLine("\nWARNING: No formats were loaded!");
            }

            Assert.IsTrue(count > 0, "Expected at least some formats to be loaded from embedded resources");
        }

        [TestMethod]
        public void DiagnosticTest_FormatDetectionWithZipFile()
        {
            var hexEditor = new HexEditor();

            // Load formats
            var loadCount = hexEditor.LoadEmbeddedFormatDefinitions();
            Console.WriteLine($"Loaded {loadCount} format definitions");

            // Create a test ZIP file signature
            byte[] zipSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

            // Create a minimal ZIP file in memory
            var zipData = new byte[100];
            Array.Copy(zipSignature, zipData, 4);

            // Create a memory stream
            using var memoryStream = new System.IO.MemoryStream(zipData);

            // Open the stream in HexEditor
            hexEditor.OpenStream(memoryStream);

            Console.WriteLine("\n=== Testing Format Detection ===");

            // Try to detect format
            var result = hexEditor.AutoDetectAndApplyFormat("test.zip");

            Console.WriteLine($"Detection Success: {result.Success}");
            if (result.Success)
            {
                Console.WriteLine($"Detected Format: {result.Format?.FormatName}");
                Console.WriteLine($"Blocks Generated: {result.Blocks?.Count ?? 0}");
                Console.WriteLine($"Detection Time: {result.DetectionTimeMs}ms");
            }
            else
            {
                Console.WriteLine($"Detection Failed: {result.ErrorMessage}");
            }

            Assert.IsTrue(result.Success, "Expected ZIP format to be detected");
            Assert.IsNotNull(result.Format, "Expected Format to be populated");
            Assert.AreEqual("ZIP Archive", result.Format.FormatName);
        }
    }
}
