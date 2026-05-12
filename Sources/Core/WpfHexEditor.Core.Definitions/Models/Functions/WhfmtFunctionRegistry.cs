//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Functions/WhfmtFunctionRegistry.cs
// Description: Registry of IWhfmtFunction implementations. The evaluator
//              resolves CallNode.Target (an IdentifierNode) to a registered
//              IWhfmtFunction at eval time.
// Architecture notes:
//              "Default" registry exposes the built-in functions every whfmt
//              can rely on (e.g. min, max, abs, length, hex, toUpper, toLower).
//              Hosts can add domain-specific functions via Register().
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Functions;

/// <summary>Lookup table for whfmt expression functions.</summary>
public sealed class WhfmtFunctionRegistry
{
    private readonly Dictionary<string, IWhfmtFunction> _functions =
        new(StringComparer.Ordinal);

    /// <summary>Registers a function, replacing any existing entry with the same Name.</summary>
    public void Register(IWhfmtFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _functions[function.Name] = function;
    }

    /// <summary>Tries to resolve a function by name. Returns false when unknown.</summary>
    public bool TryGet(string name, out IWhfmtFunction function)
    {
        if (_functions.TryGetValue(name, out var f)) { function = f; return true; }
        function = null!;
        return false;
    }

    /// <summary>All registered function names (sorted).</summary>
    public IEnumerable<string> Names => _functions.Keys.OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>
    /// Returns a registry pre-populated with the built-in functions every whfmt
    /// can rely on. Hosts may register additional functions on top.
    /// </summary>
    public static WhfmtFunctionRegistry CreateDefault()
    {
        var r = new WhfmtFunctionRegistry();
        foreach (var fn in WhfmtBuiltinFunctions.All) r.Register(fn);
        return r;
    }
}
