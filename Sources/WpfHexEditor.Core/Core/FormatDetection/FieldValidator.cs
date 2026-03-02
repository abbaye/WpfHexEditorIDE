//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Validates field values against constraints
    /// Supports: range checks, enum values, regex patterns, checksums, custom validators
    /// </summary>
    public class FieldValidator
    {
        private readonly ChecksumValidator _checksumValidator;

        /// <summary>
        /// Global registry of named custom validators.
        /// Callers can register validators via <see cref="RegisterCustomValidator"/>.
        /// A validator returns <see langword="null"/> on success, or an error message on failure.
        /// </summary>
        private static readonly Dictionary<string, Func<object, string?>> _customValidators
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a named custom validator function that can be referenced from a
        /// format definition field's <c>customValidator</c> property.
        /// </summary>
        public static void RegisterCustomValidator(string name, Func<object, string?> validator)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            _customValidators[name] = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public FieldValidator()
        {
            _checksumValidator = new ChecksumValidator();
        }

        /// <summary>
        /// Validation result
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; }

            public static ValidationResult Success() => new ValidationResult { IsValid = true };
            public static ValidationResult Failure(string message) => new ValidationResult { IsValid = false, Message = message };
        }

        /// <summary>
        /// Validate a field value against constraints
        /// </summary>
        public ValidationResult Validate(object value, FieldValidationRules rules)
        {
            if (rules == null)
                return ValidationResult.Success();

            // Range validation
            if (rules.MinValue != null || rules.MaxValue != null)
            {
                if (!ValidateRange(value, rules.MinValue, rules.MaxValue, out string rangeMsg))
                    return ValidationResult.Failure(rangeMsg);
            }

            // Enum validation
            if (rules.AllowedValues != null && rules.AllowedValues.Length > 0)
            {
                if (!ValidateEnum(value, rules.AllowedValues, out string enumMsg))
                    return ValidationResult.Failure(enumMsg);
            }

            // Regex validation (for strings)
            if (!string.IsNullOrWhiteSpace(rules.Pattern) && value is string strValue)
            {
                if (!ValidatePattern(strValue, rules.Pattern, out string patternMsg))
                    return ValidationResult.Failure(patternMsg);
            }

            // Custom validation function — looked up from the static registry
            if (!string.IsNullOrWhiteSpace(rules.CustomValidator))
            {
                if (_customValidators.TryGetValue(rules.CustomValidator, out var customFn))
                {
                    var customError = customFn(value);
                    if (customError != null)
                        return ValidationResult.Failure(customError);
                }
                // Unknown validator name → silently pass (do not block parsing)
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validate a checksum field against data
        /// </summary>
        /// <param name="fileData">Complete file data</param>
        /// <param name="config">Checksum validation configuration</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateChecksum(byte[] fileData, ChecksumValidationConfig config)
        {
            if (fileData == null || config == null)
                return ValidationResult.Failure("Invalid checksum configuration");

            if (string.IsNullOrWhiteSpace(config.Algorithm))
                return ValidationResult.Failure("Checksum algorithm not specified");

            try
            {
                // Extract data to validate
                if (config.DataOffset < 0 || config.DataOffset >= fileData.Length)
                    return ValidationResult.Failure($"Invalid data offset: {config.DataOffset}");

                if (config.DataLength <= 0 || config.DataOffset + config.DataLength > fileData.Length)
                    return ValidationResult.Failure($"Invalid data length: {config.DataLength}");

                byte[] dataToValidate = new byte[config.DataLength];
                Array.Copy(fileData, config.DataOffset, dataToValidate, 0, config.DataLength);

                // Calculate checksum
                string calculatedChecksum = _checksumValidator.Calculate(dataToValidate, config.Algorithm);
                if (calculatedChecksum == null)
                    return ValidationResult.Failure($"Unknown checksum algorithm: {config.Algorithm}");

                // If expected value provided, compare
                if (!string.IsNullOrWhiteSpace(config.ExpectedValue))
                {
                    if (string.Equals(calculatedChecksum, config.ExpectedValue, StringComparison.OrdinalIgnoreCase))
                        return ValidationResult.Success();
                    else
                        return ValidationResult.Failure($"Checksum mismatch: expected {config.ExpectedValue}, got {calculatedChecksum}");
                }

                // Otherwise, read checksum from file at specified offset
                if (config.ChecksumOffset < 0 || config.ChecksumOffset >= fileData.Length)
                    return ValidationResult.Failure($"Invalid checksum offset: {config.ChecksumOffset}");

                if (config.ChecksumLength <= 0 || config.ChecksumOffset + config.ChecksumLength > fileData.Length)
                    return ValidationResult.Failure($"Invalid checksum length: {config.ChecksumLength}");

                // Read stored checksum from file
                byte[] storedChecksumBytes = new byte[config.ChecksumLength];
                Array.Copy(fileData, config.ChecksumOffset, storedChecksumBytes, 0, config.ChecksumLength);
                string storedChecksum = BitConverter.ToString(storedChecksumBytes).Replace("-", "");

                // Compare
                if (string.Equals(calculatedChecksum, storedChecksum, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Success();
                else
                    return ValidationResult.Failure($"Checksum mismatch: stored {storedChecksum}, calculated {calculatedChecksum}");
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Checksum validation error: {ex.Message}");
            }
        }

        private bool ValidateRange(object value, object minValue, object maxValue, out string message)
        {
            message = null;

            try
            {
                long numValue = Convert.ToInt64(value);
                long? min = minValue != null ? Convert.ToInt64(minValue) : (long?)null;
                long? max = maxValue != null ? Convert.ToInt64(maxValue) : (long?)null;

                if (min.HasValue && numValue < min.Value)
                {
                    message = $"Value {numValue} is less than minimum {min.Value}";
                    return false;
                }

                if (max.HasValue && numValue > max.Value)
                {
                    message = $"Value {numValue} is greater than maximum {max.Value}";
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                message = "Invalid value for range comparison";
                return false;
            }
        }

        private bool ValidateEnum(object value, object[] allowedValues, out string message)
        {
            message = null;

            foreach (var allowed in allowedValues)
            {
                if (allowed != null && allowed.Equals(value))
                    return true;
            }

            message = $"Value '{value}' is not in allowed set: {string.Join(", ", allowedValues)}";
            return false;
        }

        private bool ValidatePattern(string value, string pattern, out string message)
        {
            message = null;

            try
            {
                if (!Regex.IsMatch(value, pattern))
                {
                    message = $"Value '{value}' does not match pattern '{pattern}'";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                message = $"Invalid regex pattern: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>
    /// Validation rules for a field
    /// </summary>
    public class FieldValidationRules
    {
        /// <summary>
        /// Minimum allowed value
        /// </summary>
        public object MinValue { get; set; }

        /// <summary>
        /// Maximum allowed value
        /// </summary>
        public object MaxValue { get; set; }

        /// <summary>
        /// Set of allowed values (enum validation)
        /// </summary>
        public object[] AllowedValues { get; set; }

        /// <summary>
        /// Regex pattern for string validation
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Custom validator function name
        /// </summary>
        public string CustomValidator { get; set; }

        /// <summary>
        /// Error message to show if validation fails
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Checksum validation configuration
        /// </summary>
        public ChecksumValidationConfig Checksum { get; set; }
    }

    /// <summary>
    /// Configuration for checksum validation
    /// </summary>
    public class ChecksumValidationConfig
    {
        /// <summary>
        /// Checksum algorithm (crc32, md5, sha1, sha256, sum8, sum16, sum32)
        /// </summary>
        public string Algorithm { get; set; }

        /// <summary>
        /// Offset where the checksum field is located (relative to data being validated)
        /// </summary>
        public long ChecksumOffset { get; set; }

        /// <summary>
        /// Length of the checksum field in bytes
        /// </summary>
        public int ChecksumLength { get; set; }

        /// <summary>
        /// Offset where data to validate starts
        /// </summary>
        public long DataOffset { get; set; }

        /// <summary>
        /// Length of data to validate
        /// </summary>
        public int DataLength { get; set; }

        /// <summary>
        /// Expected checksum value (hex string) if known upfront
        /// </summary>
        public string ExpectedValue { get; set; }
    }
}
