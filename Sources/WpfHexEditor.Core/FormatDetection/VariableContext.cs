// ==========================================================
// Project: WpfHexEditor.Core
// File: VariableContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Provides a named variable store for the format script interpreter,
//     allowing offset and length expressions to reference previously parsed
//     field values using "var:name" syntax in .whfmt format definitions.
//
// Architecture Notes:
//     Simple Dictionary wrapper. One instance per script execution —
//     not shared across concurrent evaluations. Used by ExpressionEvaluator
//     and FormatScriptInterpreter. No WPF dependencies.
//
// ==========================================================

using System.Collections.Generic;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Context for storing and retrieving variables during format parsing
    /// Variables can be referenced in offset/length calculations as "var:name"
    /// </summary>
    public class VariableContext
    {
        private readonly Dictionary<string, object> _variables = new Dictionary<string, object>();

        /// <summary>
        /// Set a variable value
        /// </summary>
        public void SetVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            _variables[name] = value;
        }

        /// <summary>
        /// Get a variable value
        /// </summary>
        public object GetVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _variables.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Get a variable as long (for offsets/lengths)
        /// </summary>
        public long GetVariableAsLong(string name, long defaultValue = 0)
        {
            var value = GetVariable(name);

            return value switch
            {
                long l => l,
                int i => i,
                uint ui => ui,
                ushort us => us,
                byte b => b,
                _ => defaultValue
            };
        }

        /// <summary>
        /// Check if a variable exists
        /// </summary>
        public bool HasVariable(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _variables.ContainsKey(name);
        }

        /// <summary>
        /// Clear all variables
        /// </summary>
        public void Clear()
        {
            _variables.Clear();
        }

        /// <summary>
        /// Get count of stored variables
        /// </summary>
        public int Count => _variables.Count;
    }
}
