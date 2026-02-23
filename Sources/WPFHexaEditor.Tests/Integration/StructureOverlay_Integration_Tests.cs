//////////////////////////////////////////////
// Apache 2.0  - 2026
// Structure Overlay Integration Tests
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using WpfHexaEditor.Services;

namespace WpfHexaEditor.Tests.Integration
{
    [TestClass]
    public class StructureOverlay_Integration_Tests
    {
        private StructureOverlayService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new StructureOverlayService();
        }

        [TestMethod]
        public void CreateCustomOverlay_ValidFields_ReturnsOverlay()
        {
            // Arrange
            var fields = new List<(string, string, int)>
            {
                ("Header", "uint32", 4),
                ("Version", "uint16", 2),
                ("Flags", "uint16", 2)
            };

            // Act
            var overlay = _service.CreateCustomOverlay("Test Structure", fields, 0);

            // Assert
            Assert.IsNotNull(overlay);
            Assert.AreEqual("Test Structure", overlay.Name);
            Assert.AreEqual(3, overlay.Fields.Count);
            Assert.AreEqual(8, overlay.TotalLength); // 4 + 2 + 2
        }

        [TestMethod]
        public void CreateCustomOverlay_WithOffset_CalculatesCorrectOffsets()
        {
            // Arrange
            var fields = new List<(string, string, int)>
            {
                ("Field1", "uint32", 4),
                ("Field2", "uint32", 4)
            };

            // Act
            var overlay = _service.CreateCustomOverlay("Test", fields, 100);

            // Assert
            Assert.AreEqual(100, overlay.Fields[0].Offset);
            Assert.AreEqual(104, overlay.Fields[1].Offset);
        }

        [TestMethod]
        public void CreateOverlayFromFormat_SimpleSignature_CreatesSignatureField()
        {
            // Arrange
            var formatDef = JObject.Parse(@"
            {
                ""formatName"": ""Test Format"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""signature"",
                        ""expected"": ""50 4B 03 04""
                    }
                ]
            }");
            var fileBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };

            // Act
            var overlay = _service.CreateOverlayFromFormat(formatDef, fileBytes);

            // Assert
            Assert.IsNotNull(overlay);
            Assert.AreEqual("Test Format", overlay.Name);
            Assert.IsTrue(overlay.Fields.Count > 0);
            Assert.AreEqual("Signature", overlay.Fields[0].Name);
        }

        [TestMethod]
        public void CreateOverlayFromFormat_WithFields_CreatesFieldOverlays()
        {
            // Arrange
            var formatDef = JObject.Parse(@"
            {
                ""formatName"": ""Test Format"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""field"",
                        ""fields"": [
                            {
                                ""name"": ""Magic"",
                                ""type"": ""uint32"",
                                ""description"": ""File magic number""
                            },
                            {
                                ""name"": ""Version"",
                                ""type"": ""uint16"",
                                ""description"": ""Format version""
                            }
                        ]
                    }
                ]
            }");
            var fileBytes = new byte[10];

            // Act
            var overlay = _service.CreateOverlayFromFormat(formatDef, fileBytes);

            // Assert
            Assert.IsNotNull(overlay);
            Assert.AreEqual(2, overlay.Fields.Count);
            Assert.AreEqual("Magic", overlay.Fields[0].Name);
            Assert.AreEqual("uint32", overlay.Fields[0].Type);
            Assert.AreEqual(4, overlay.Fields[0].Length);
            Assert.AreEqual("Version", overlay.Fields[1].Name);
            Assert.AreEqual("uint16", overlay.Fields[1].Type);
            Assert.AreEqual(2, overlay.Fields[1].Length);
        }

        [TestMethod]
        public void CreateOverlayFromFormat_NullFormat_ReturnsNull()
        {
            // Act
            var overlay = _service.CreateOverlayFromFormat(null, new byte[10]);

            // Assert
            Assert.IsNull(overlay);
        }

        [TestMethod]
        public void CreateOverlayFromFormat_NullBytes_ReturnsNull()
        {
            // Arrange
            var formatDef = JObject.Parse(@"{""formatName"":""Test"",""version"":""1.0"",""blocks"":[]}");

            // Act
            var overlay = _service.CreateOverlayFromFormat(formatDef, null);

            // Assert
            Assert.IsNull(overlay);
        }

        [TestMethod]
        public void CreateOverlayFromFormat_EmptyBlocks_ReturnsNull()
        {
            // Arrange
            var formatDef = JObject.Parse(@"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": []
            }");

            // Act
            var overlay = _service.CreateOverlayFromFormat(formatDef, new byte[10]);

            // Assert
            Assert.IsNull(overlay);
        }
    }
}
