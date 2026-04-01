
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Optionally implemented by an <see cref="IEditorFactory"/> to support background
/// and pre-open file validation without instantiating a full UI editor.
///
/// <para>Validators run on a background thread and return a list of
/// <see cref="DiagnosticEntry"/> items that can be fed directly into an
/// <see cref="IErrorPanel"/>.</para>
///
/// <para>Usage — discover at runtime with a pattern cast:
/// <code>
/// if (factory is IFileValidator v)
///     diagnostics = await v.ValidateAsync(filePath, ct);
/// </code>
/// </para>
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validates <paramref name="filePath"/> without opening a UI editor.
    /// Returns zero or more diagnostics (errors / warnings / messages).
    /// The method must be safe to call on a background thread.
    /// </summary>
    Task<IReadOnlyList<DiagnosticEntry>> ValidateAsync(
        string            filePath,
        CancellationToken ct = default);
}
