//////////////////////////////////////////////
// Apache 2.0  - 2026
// Format Schema Validator Unit Tests
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using WpfHexEditor.JsonEditor.Models;
using WpfHexEditor.JsonEditor.Services;

namespace WpfHexaEditor.Tests.Unit
{
    [TestClass]
    public class FormatSchemaValidator_Tests
    {
        private FormatSchemaValidator _validator;

        [TestInitialize]
        public void Setup()
        {
            _validator = new FormatSchemaValidator();
        }

        [TestMethod]
        public void Validate_EmptyString_ReturnsError()
        {
            // Act
            var errors = _validator.Validate("");

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.AreEqual("EMPTY_DOCUMENT", errors[0].ErrorCode);
        }

        [TestMethod]
        public void Validate_InvalidJSON_ReturnsSyntaxError()
        {
            // Arrange
            var json = "{ invalid json }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.AreEqual(ValidationLayer.JsonSyntax, errors[0].Layer);
        }

        [TestMethod]
        public void Validate_MissingRequiredProperty_ReturnsSchemaError()
        {
            // Arrange
            var json = @"{""version"": ""1.0""}"; // Missing formatName and blocks

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "MISSING_REQUIRED_PROP"));
            Assert.IsTrue(errors.Any(e => e.Layer == ValidationLayer.Schema));
        }

        [TestMethod]
        public void Validate_InvalidBlockType_ReturnsFormatRulesError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""invalid_type""
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "INVALID_BLOCK_TYPE_VALUE"));
            Assert.IsTrue(errors.Any(e => e.Layer == ValidationLayer.FormatRules));
        }

        [TestMethod]
        public void Validate_InvalidFieldType_ReturnsFormatRulesError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""field"",
                        ""fields"": [
                            {
                                ""type"": ""invalid_type"",
                                ""name"": ""Test Field""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "INVALID_FIELD_TYPE_VALUE"));
        }

        [TestMethod]
        public void Validate_UndefinedVariable_ReturnsSemanticError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""field"",
                        ""fields"": [
                            {
                                ""type"": ""uint32"",
                                ""name"": ""Length"",
                                ""length"": ""var:undefinedVar""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "UNDEFINED_VARIABLE"));
            Assert.IsTrue(errors.Any(e => e.Layer == ValidationLayer.Semantic));
        }

        [TestMethod]
        public void Validate_ConditionalWithoutCondition_ReturnsFormatRulesError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""conditional""
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "MISSING_CONDITION"));
        }

        [TestMethod]
        public void Validate_LoopWithoutCount_ReturnsFormatRulesError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""loop""
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "MISSING_COUNT"));
        }

        [TestMethod]
        public void Validate_ValidFormat_ReturnsNoErrors()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test Format"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""signature"",
                        ""expected"": ""50 4B 03 04""
                    },
                    {
                        ""type"": ""field"",
                        ""fields"": [
                            {
                                ""type"": ""uint32"",
                                ""name"": ""File Size"",
                                ""description"": ""Total file size""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_EmptyFormatName_ReturnsSchemaError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": """",
                ""version"": ""1.0"",
                ""blocks"": []
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "EMPTY_FORMAT_NAME"));
        }

        [TestMethod]
        public void Validate_InvalidEndianness_ReturnsFormatRulesError()
        {
            // Arrange
            var json = @"
            {
                ""formatName"": ""Test"",
                ""version"": ""1.0"",
                ""blocks"": [
                    {
                        ""type"": ""field"",
                        ""fields"": [
                            {
                                ""type"": ""uint32"",
                                ""name"": ""Value"",
                                ""endianness"": ""middle""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var errors = _validator.Validate(json);

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "INVALID_ENDIANNESS"));
        }
    }
}
