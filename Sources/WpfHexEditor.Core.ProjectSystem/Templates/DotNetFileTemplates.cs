//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

// =============================================================================
// C# / .NET file templates (7)
// =============================================================================

/// <summary>Template for a new C# class file.</summary>
public sealed class CSharpClassTemplate : IFileTemplate
{
    public string Name             => "C# Class";
    public string Description      => "Creates a new C# class file with a minimal class stub.";
    public string DefaultExtension => ".cs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "namespace MyNamespace;\n\npublic class MyClass\n{\n}\n");
}

/// <summary>Template for a new C# interface file.</summary>
public sealed class CSharpInterfaceTemplate : IFileTemplate
{
    public string Name             => "C# Interface";
    public string Description      => "Creates a new C# interface file with a minimal interface stub.";
    public string DefaultExtension => ".cs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "namespace MyNamespace;\n\npublic interface IMyInterface\n{\n}\n");
}

/// <summary>Template for a new C# enum file.</summary>
public sealed class CSharpEnumTemplate : IFileTemplate
{
    public string Name             => "C# Enum";
    public string Description      => "Creates a new C# enum file with placeholder values.";
    public string DefaultExtension => ".cs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "namespace MyNamespace;\n\npublic enum MyEnum\n{\n    Value1,\n    Value2,\n}\n");
}

/// <summary>Template for a new C# record file.</summary>
public sealed class CSharpRecordTemplate : IFileTemplate
{
    public string Name             => "C# Record";
    public string Description      => "Creates a new C# record with a single positional property.";
    public string DefaultExtension => ".cs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "namespace MyNamespace;\n\npublic record MyRecord(string Name);\n");
}

/// <summary>Template for a new C# struct file.</summary>
public sealed class CSharpStructTemplate : IFileTemplate
{
    public string Name             => "C# Struct";
    public string Description      => "Creates a new C# struct with a minimal stub.";
    public string DefaultExtension => ".cs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "namespace MyNamespace;\n\npublic struct MyStruct\n{\n}\n");
}

/// <summary>Template for a new VB.NET class file.</summary>
public sealed class VbNetClassTemplate : IFileTemplate
{
    public string Name             => "VB.NET Class";
    public string Description      => "Creates a new Visual Basic class file with a minimal class stub.";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Public Class MyClass\n\nEnd Class\n");
}

/// <summary>Template for a new WpfHexEditor CSX script file.</summary>
public sealed class CsxScriptTemplate : IFileTemplate
{
    public string Name             => "WpfHexEditor Script";
    public string Description      => "Creates a new .csx script that runs inside WpfHexEditor with access to editor, buffer and output built-in references.";
    public string DefaultExtension => ".csx";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE756";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// WpfHexEditor Script (.csx)\n" +
        "// Built-in references: editor, buffer, output\n\n" +
        "output.WriteLine(\"Hello from WpfHexEditor Script!\");\n");
}
