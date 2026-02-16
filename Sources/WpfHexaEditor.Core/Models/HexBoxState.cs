//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

namespace WpfHexaEditor.Models
{
    /// <summary>
    /// Represents the state of a HexBox control (immutable value object).
    /// Used for validation and state management in MVVM architecture.
    /// </summary>
    public class HexBoxState
    {
        /// <summary>
        /// Current decimal value
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// Maximum allowed value
        /// </summary>
        public long MaximumValue { get; }

        /// <summary>
        /// Read-only mode flag
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Creates a new HexBoxState instance
        /// </summary>
        public HexBoxState(long value, long maximumValue, bool isReadOnly = false)
        {
            Value = value;
            MaximumValue = maximumValue;
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// Checks if the current value is within valid range
        /// </summary>
        public bool IsValid => Value >= 0 && Value <= MaximumValue;

        /// <summary>
        /// Checks if value can be incremented
        /// </summary>
        public bool CanIncrement => !IsReadOnly && Value < MaximumValue;

        /// <summary>
        /// Checks if value can be decremented
        /// </summary>
        public bool CanDecrement => !IsReadOnly && Value > 0;

        /// <summary>
        /// Creates a new state with incremented value
        /// </summary>
        public HexBoxState Increment()
        {
            if (!CanIncrement) return this;
            return new HexBoxState(Value + 1, MaximumValue, IsReadOnly);
        }

        /// <summary>
        /// Creates a new state with decremented value
        /// </summary>
        public HexBoxState Decrement()
        {
            if (!CanDecrement) return this;
            return new HexBoxState(Value - 1, MaximumValue, IsReadOnly);
        }

        /// <summary>
        /// Creates a new state with updated value (coerced to valid range)
        /// </summary>
        public HexBoxState WithValue(long newValue)
        {
            var coercedValue = CoerceValue(newValue, MaximumValue);
            return new HexBoxState(coercedValue, MaximumValue, IsReadOnly);
        }

        /// <summary>
        /// Coerces a value to valid range [0, maximum]
        /// </summary>
        private static long CoerceValue(long value, long maximum)
        {
            if (value < 0) return 0;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
