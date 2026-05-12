//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/WhfmtVariableStore.cs
// Description: Runtime key/value store for variables declared in a whfmt file.
//              Populated as the parser walks the block tree (storeAs writes here)
//              and read by the expression evaluator (P4).
// Architecture notes:
//              Loosely typed (object). The store does not enforce VariableDefinition.Type
//              at write time — parsers are responsible for producing the right runtime
//              type. Reads use TryGet<T>() for safe casting.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// Runtime key/value store for whfmt variables. One instance per format-parse session.
/// Not thread-safe — variables are populated and read by a single parser/evaluator pipeline.
/// </summary>
public sealed class WhfmtVariableStore
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VariableDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>Number of variables currently in the store.</summary>
    public int Count => _values.Count;

    /// <summary>All variable names currently in the store (insertion order not guaranteed).</summary>
    public IEnumerable<string> Names => _values.Keys;

    /// <summary>All variable definitions registered (a subset of Names — dict-schema vars without explicit declarations are absent).</summary>
    public IEnumerable<VariableDefinition> Definitions => _definitions.Values;

    /// <summary>
    /// Registers a variable definition. If the variable carries an InitialValue it
    /// is also written to the store as the current value.
    /// </summary>
    public void Register(VariableDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        _definitions[def.Name] = def;
        if (def.InitialValue is not null)
            _values[def.Name] = def.InitialValue;
        else
            _values.TryAdd(def.Name, null);
    }

    /// <summary>Writes a value. Overwrites any previous value for the same name.</summary>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _values[name] = value;
    }

    /// <summary>
    /// Reads a value as <typeparamref name="T"/>. Returns false when the variable is missing,
    /// null, or not convertible to <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(string name, out T value)
    {
        if (_values.TryGetValue(name, out var raw) && raw is not null)
        {
            if (raw is T direct) { value = direct; return true; }
            try
            {
                value = (T)Convert.ChangeType(raw, typeof(T))!;
                return true;
            }
            catch
            {
                // Fallthrough to default
            }
        }
        value = default!;
        return false;
    }

    /// <summary>Returns the raw object value, or null when missing.</summary>
    public object? GetRaw(string name)
        => _values.TryGetValue(name, out var v) ? v : null;

    /// <summary>Returns the registered definition, or null when only a loose value was Set().</summary>
    public VariableDefinition? GetDefinition(string name)
        => _definitions.TryGetValue(name, out var d) ? d : null;

    /// <summary>True when the store contains a value (including null) for the given name.</summary>
    public bool Contains(string name) => _values.ContainsKey(name);
}
