//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// ROM Hacking / Game script file templates (5)
// Covers all extensions supported by ScriptEditorFactory:
//   .scr  .msg  .evt  .script  .dec
// Each template also appears under the "Script" category via Categories override.
// =============================================================================

/// <summary>Template for a game script file (.scr) — open with the Script Editor.</summary>
public sealed class GameScriptTemplate : IFileTemplate
{
    public string Name             => "Game Script";
    public string Description      => "Creates a new game script file (.scr). Opened by the Script Editor with F5 run support.";
    public string DefaultExtension => ".scr";
    public string Category         => "ROM Hacking";
    public string IconGlyph        => "\uE756";   // Segoe MDL2: Script / code
    public IReadOnlyList<string> Categories => ["ROM Hacking", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// Game Script\n\n");
}

/// <summary>Template for a game message / dialogue file (.msg).</summary>
public sealed class GameMessageTemplate : IFileTemplate
{
    public string Name             => "Game Message";
    public string Description      => "Creates a new game message/dialogue file (.msg). Opened by the Script Editor.";
    public string DefaultExtension => ".msg";
    public string Category         => "ROM Hacking";
    public string IconGlyph        => "\uE8F2";   // Segoe MDL2: Chat / message

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// Game Message\n\n");
}

/// <summary>Template for a game event script file (.evt) — open with the Script Editor.</summary>
public sealed class GameEventTemplate : IFileTemplate
{
    public string Name             => "Game Event Script";
    public string Description      => "Creates a new game event script file (.evt). Opened by the Script Editor.";
    public string DefaultExtension => ".evt";
    public string Category         => "ROM Hacking";
    public string IconGlyph        => "\uE8F3";   // Segoe MDL2: Lightning / event
    public IReadOnlyList<string> Categories => ["ROM Hacking", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// Game Event Script\n\n");
}

/// <summary>Template for a generic game script file (.script) — open with the Script Editor.</summary>
public sealed class GenericGameScriptTemplate : IFileTemplate
{
    public string Name             => "Generic Script";
    public string Description      => "Creates a new generic game script file (.script). Opened by the Script Editor.";
    public string DefaultExtension => ".script";
    public string Category         => "ROM Hacking";
    public string IconGlyph        => "\uE756";
    public IReadOnlyList<string> Categories => ["ROM Hacking", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// Generic Script\n\n");
}

/// <summary>Template for a decompiled game script file (.dec) — open with the Script Editor.</summary>
public sealed class GameDecompiledScriptTemplate : IFileTemplate
{
    public string Name             => "Decompiled Script";
    public string Description      => "Creates a new decompiled script file (.dec). Opened by the Script Editor.";
    public string DefaultExtension => ".dec";
    public string Category         => "ROM Hacking";
    public string IconGlyph        => "\uE756";
    public IReadOnlyList<string> Categories => ["ROM Hacking", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// Decompiled Script\n\n");
}
