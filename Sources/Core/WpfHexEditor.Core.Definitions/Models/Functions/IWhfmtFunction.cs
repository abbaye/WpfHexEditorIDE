//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Functions/IWhfmtFunction.cs
// Description: Contract for functions callable from whfmt expressions.
//              Implementations are registered into WhfmtFunctionRegistry
//              and looked up by Name from CallNode evaluation.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Functions;

/// <summary>
/// A function callable from a whfmt expression.
/// Implementations are typically stateless and registered as singletons.
/// </summary>
public interface IWhfmtFunction
{
    /// <summary>Canonical function name (matched against the source token).</summary>
    string Name { get; }

    /// <summary>
    /// Invokes the function. Implementations decide their own arity/type checks
    /// and should throw <see cref="System.ArgumentException"/> on invalid input.
    /// </summary>
    object? Invoke(IReadOnlyList<object?> args);
}
