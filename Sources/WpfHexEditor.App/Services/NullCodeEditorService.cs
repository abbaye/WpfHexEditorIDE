
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Null-object implementation of ICodeEditorService.
/// Used until a full CodeEditorServiceImpl is wired; prevents null reference faults in plugins.
/// </summary>
public sealed class NullCodeEditorService : ICodeEditorService
{
    public bool IsActive => false;
    public string? CurrentLanguage => null;
    public int CaretLine => 0;
    public int CaretColumn => 0;
    public event EventHandler? DocumentChanged { add { } remove { } }
    public string? GetContent() => null;
    public string? GetSelectedText() => null;
}
