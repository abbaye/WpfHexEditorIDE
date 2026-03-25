//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// Script file templates (3)
// =============================================================================

/// <summary>Template for a new PowerShell script file.</summary>
public sealed class PowerShellFileTemplate : IFileTemplate
{
    public string Name             => "PowerShell Script";
    public string Description      => "Creates a new PowerShell script file (.ps1).";
    public string DefaultExtension => ".ps1";
    public string Category         => "Script";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# PowerShell Script\n\n");
}

/// <summary>Template for a new Python script file.</summary>
public sealed class PythonFileTemplate : IFileTemplate
{
    public string Name             => "Python Script";
    public string Description      => "Creates a new Python script file (.py) with a shebang line.";
    public string DefaultExtension => ".py";
    public string Category         => "Script";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "#!/usr/bin/env python3\n\n");
}

/// <summary>Template for a new Lua script file.</summary>
public sealed class LuaFileTemplate : IFileTemplate
{
    public string Name             => "Lua Script";
    public string Description      => "Creates a new Lua script file (.lua).";
    public string DefaultExtension => ".lua";
    public string Category         => "Script";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "-- Lua Script\n\n");
}

/// <summary>Template for a new Shell/Bash script file.</summary>
public sealed class BashScriptTemplate : IFileTemplate
{
    public string Name             => "Shell Script";
    public string Description      => "Creates a new Bash/Shell script file (.sh) with a shebang line.";
    public string DefaultExtension => ".sh";
    public string Category         => "Script";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "#!/usr/bin/env bash\nset -euo pipefail\n\n# Shell Script\n\n");
}

/// <summary>Template for a new Windows Batch script file.</summary>
public sealed class BatchScriptTemplate : IFileTemplate
{
    public string Name             => "Batch Script";
    public string Description      => "Creates a new Windows Batch script file (.bat).";
    public string DefaultExtension => ".bat";
    public string Category         => "Script";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "@echo off\nREM Batch Script\n\n");
}
