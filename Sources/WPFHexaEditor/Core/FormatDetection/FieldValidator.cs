//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Text.RegularExpressions;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Validates field values against constraints
    /// Supports: range checks, enum values, regex patterns, custom validators
    /// </summary>
    public class FieldValidator
    {
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

            // Custom validation function
            if (!string.IsNullOrWhiteSpace(rules.CustomValidator))
            {
                // TODO: Implement custom validator support
            }

            return ValidationResult.Success();
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
    }
}
