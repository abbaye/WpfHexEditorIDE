//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ScriptEditor.Controls;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Editor.ScriptEditor;

/// <summary>
/// Factory that creates <see cref="ScriptEditor"/> instances and injects
/// the <see cref="IScriptingService"/> so F5 / Ctrl+F5 work at runtime.
/// </summary>
public sealed class ScriptEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ScriptEditorDescriptor();

    private readonly Func<IScriptingService?>? _scriptingGetter;

    /// <summary>
    /// Creates the factory without a scripting service (F5/Ctrl+F5 disabled).
    /// </summary>
    public ScriptEditorFactory() { }

    /// <summary>
    /// Creates the factory with a lazy getter so the scripting service can be resolved
    /// after plugin-system initialization (called later than factory registration).
    /// </summary>
    public ScriptEditorFactory(Func<IScriptingService?> scriptingGetter) =>
        _scriptingGetter = scriptingGetter;

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(ext) && _descriptor.SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public IDocumentEditor Create()
    {
        var editor = new Controls.ScriptEditor();
        editor.SetScriptingService(_scriptingGetter?.Invoke());
        return editor;
    }
}

file sealed class ScriptEditorDescriptor : IEditorDescriptor
{
    public string Id          => "script-editor";
    public string DisplayName => "Script Editor";
    public string Description => "C# script editor (.csx) and game script formats (.scr, .msg, .evt, .script, .dec). F5 runs · Ctrl+F5 validates.";

    public IReadOnlyList<string> SupportedExtensions =>
    [
        ".csx",    // C# script files — primary target for F5/run
        ".scr", ".msg", ".evt", ".script", ".dec",   // game scripts
    ];
}
