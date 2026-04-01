//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// Config / DevOps file templates (5)
// =============================================================================

/// <summary>Template for a TOML configuration file.</summary>
public sealed class TomlFileTemplate : IFileTemplate
{
    public string Name             => "TOML Config";
    public string Description      => "Creates a new TOML configuration file (.toml) with a minimal example structure.";
    public string DefaultExtension => ".toml";
    public string Category         => "Config";
    public string IconGlyph        => "\uE8B1";   // Segoe MDL2: Settings / config

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# TOML Configuration\n\n" +
        "[section]\n" +
        "key = \"value\"\n");
}

/// <summary>Template for an INI configuration file.</summary>
public sealed class IniFileTemplate : IFileTemplate
{
    public string Name             => "INI Config";
    public string Description      => "Creates a new INI configuration file (.ini) with a minimal section/key structure.";
    public string DefaultExtension => ".ini";
    public string Category         => "Config";
    public string IconGlyph        => "\uE8B1";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "; INI Configuration\n\n" +
        "[Section]\n" +
        "Key=Value\n");
}

/// <summary>Template for a SQL query file.</summary>
public sealed class SqlFileTemplate : IFileTemplate
{
    public string Name             => "SQL Query";
    public string Description      => "Creates a new SQL query file (.sql) with a SELECT stub.";
    public string DefaultExtension => ".sql";
    public string Category         => "Config";
    public string IconGlyph        => "\uE1D3";   // Segoe MDL2: Database

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "-- SQL Query\n\n" +
        "SELECT *\nFROM table_name\nWHERE 1 = 1;\n");
}

/// <summary>Template for a Dockerfile.</summary>
public sealed class DockerfileTemplate : IFileTemplate
{
    public string Name             => "Dockerfile";
    public string Description      => "Creates a new Dockerfile with a minimal .NET 8 multi-stage build stub.";
    public string DefaultExtension => "";   // no extension — named "Dockerfile"
    public string Category         => "Config";
    public string IconGlyph        => "\uF0E7";   // Segoe MDL2 Assets: Container / box

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# Dockerfile\n\n" +
        "FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base\n" +
        "WORKDIR /app\n\n" +
        "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build\n" +
        "WORKDIR /src\n" +
        "COPY . .\n" +
        "RUN dotnet build -c Release\n\n" +
        "FROM build AS publish\n" +
        "RUN dotnet publish -c Release -o /app/publish\n\n" +
        "FROM base AS final\n" +
        "WORKDIR /app\n" +
        "COPY --from=publish /app/publish .\n" +
        "ENTRYPOINT [\"dotnet\", \"YourApp.dll\"]\n");
}

/// <summary>Template for an .editorconfig file.</summary>
public sealed class EditorConfigFileTemplate : IFileTemplate
{
    public string Name             => "EditorConfig";
    public string Description      => "Creates a new .editorconfig file with common C# and general formatting rules.";
    public string DefaultExtension => ".editorconfig";
    public string Category         => "Config";
    public string IconGlyph        => "\uE8A5";   // Segoe MDL2: Document / file

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "# EditorConfig — https://editorconfig.org\n" +
        "root = true\n\n" +
        "[*]\n" +
        "indent_style = space\n" +
        "indent_size = 4\n" +
        "end_of_line = crlf\n" +
        "charset = utf-8\n" +
        "trim_trailing_whitespace = true\n" +
        "insert_final_newline = true\n\n" +
        "[*.{cs,vb}]\n" +
        "dotnet_sort_system_directives_first = true\n" +
        "csharp_new_line_before_open_brace = all\n\n" +
        "[*.md]\n" +
        "trim_trailing_whitespace = false\n");
}
