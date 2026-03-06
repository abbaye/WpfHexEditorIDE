
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Null-object implementation of IParsedFieldService.
/// Returns empty collections; prevents null reference faults in plugins before ParsedFields wiring.
/// </summary>
public sealed class NullParsedFieldService : IParsedFieldService
{
    public IReadOnlyList<ParsedFieldEntry> GetParsedFields() => [];
    public ParsedFieldEntry? GetFieldAtOffset(long offset) => null;
}
