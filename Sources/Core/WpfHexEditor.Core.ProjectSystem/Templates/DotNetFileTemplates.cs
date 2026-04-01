//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
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

/// <summary>Template for a new VB.NET interface file.</summary>
public sealed class VbNetInterfaceTemplate : IFileTemplate
{
    public string Name             => "VB.NET Interface";
    public string Description      => "Creates a new Visual Basic interface file with a minimal interface stub.";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Public Interface IMyInterface\n\nEnd Interface\n");
}

/// <summary>Template for a new VB.NET enum file.</summary>
public sealed class VbNetEnumTemplate : IFileTemplate
{
    public string Name             => "VB.NET Enum";
    public string Description      => "Creates a new Visual Basic enum file with placeholder values.";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Public Enum MyEnum\n    Value1\n    Value2\nEnd Enum\n");
}

/// <summary>Template for a new VB.NET module file.</summary>
public sealed class VbNetModuleTemplate : IFileTemplate
{
    public string Name             => "VB.NET Module";
    public string Description      => "Creates a new Visual Basic module file — shared state and helper methods.";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Module MyModule\n\n    Sub Main()\n    End Sub\n\nEnd Module\n");
}

/// <summary>Template for a new VB.NET structure file.</summary>
public sealed class VbNetStructureTemplate : IFileTemplate
{
    public string Name             => "VB.NET Structure";
    public string Description      => "Creates a new Visual Basic Structure (value type) file.";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Public Structure MyStructure\n\n    Public Property Value As Integer\n\nEnd Structure\n");
}

/// <summary>Template for a new VB.NET record file (VB 16+).</summary>
public sealed class VbNetRecordTemplate : IFileTemplate
{
    public string Name             => "VB.NET Record";
    public string Description      => "Creates a new Visual Basic Record (immutable reference type, VB 16+).";
    public string DefaultExtension => ".vb";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "Public Class MyRecord\n    Public Property Id As Integer\n    Public Property Name As String\nEnd Class\n");
}

/// <summary>Template for a new WpfHexEditor CSX script file.</summary>
public sealed class CsxScriptTemplate : IFileTemplate
{
    public string Name             => "WpfHexEditor Script";
    public string Description      => "Creates a new .csx script that runs inside WpfHexEditor with access to HexEditor, Documents, Output, Terminal and CT globals.";
    public string DefaultExtension => ".csx";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE756";
    // Also visible under "Script" — a .csx is a runnable script as much as a .NET file.
    public IReadOnlyList<string> Categories => ["C# / .NET", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// WpfHexEditor Script (.csx)\n" +
        "// Built-in globals: HexEditor, Documents, Output, Terminal, CT\n" +
        "//\n" +
        "// Run with F5 in the Script Editor, or: run-csharp <path.csx>\n\n" +
        "Print(\"File: \" + HexEditor.CurrentFilePath);\n" +
        "Print(\"Size: \" + HexEditor.FileSize + \" bytes\");\n\n" +
        "// ── Register a custom HxTerminal command (optional) ───────────────────\n" +
        "// Terminal?.RegisterCommand(new ScriptCommand(\n" +
        "//     name:        \"my-cmd\",\n" +
        "//     description: \"My script command.\",\n" +
        "//     usage:       \"my-cmd [arg]\",\n" +
        "//     source:      \"Script\",\n" +
        "//     execute:     async (args, output, ctx, ct) =>\n" +
        "//     {\n" +
        "//         output.WriteInfo(\"Hello from my-cmd!\");\n" +
        "//         return 0;\n" +
        "//     }));\n");
}

/// <summary>Template for a WPF UserControl (.xaml + code-behind).</summary>
public sealed class WpfUserControlTemplate : IFileTemplate
{
    public string Name             => "WPF UserControl";
    public string Description      => "Creates a new WPF UserControl (.xaml) with a minimal XAML stub.";
    public string DefaultExtension => ".xaml";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8A5";
    public IReadOnlyList<string> Categories => ["C# / .NET", "General"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "<UserControl x:Class=\"MyNamespace.MyUserControl\"\n" +
        "             xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n" +
        "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
        "    <Grid>\n" +
        "    </Grid>\n" +
        "</UserControl>\n");
}

/// <summary>Template for a WPF Window (.xaml + code-behind).</summary>
public sealed class WpfWindowTemplate : IFileTemplate
{
    public string Name             => "WPF Window";
    public string Description      => "Creates a new WPF Window (.xaml) with a minimal XAML stub.";
    public string DefaultExtension => ".xaml";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8A5";
    public IReadOnlyList<string> Categories => ["C# / .NET", "General"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "<Window x:Class=\"MyNamespace.MyWindow\"\n" +
        "        xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n" +
        "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
        "        Title=\"MyWindow\" Width=\"800\" Height=\"450\">\n" +
        "    <Grid>\n" +
        "    </Grid>\n" +
        "</Window>\n");
}

/// <summary>Template for an F# script file (.fsx).</summary>
public sealed class FSharpScriptTemplate : IFileTemplate
{
    public string Name             => "F# Script";
    public string Description      => "Creates a new F# interactive script file (.fsx).";
    public string DefaultExtension => ".fsx";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE756";
    public IReadOnlyList<string> Categories => ["C# / .NET", "Script"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "// F# Script\n\nprintfn \"Hello, F#!\"\n");
}

/// <summary>Template for an F# module file (.fs).</summary>
public sealed class FSharpModuleTemplate : IFileTemplate
{
    public string Name             => "F# Module";
    public string Description      => "Creates a new F# module source file (.fs) with a minimal module stub.";
    public string DefaultExtension => ".fs";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8D0";

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "module MyModule\n\nlet hello () = printfn \"Hello, F#!\"\n");
}

/// <summary>Template for a Razor page component (.razor).</summary>
public sealed class RazorPageTemplate : IFileTemplate
{
    public string Name             => "Razor Page";
    public string Description      => "Creates a new Razor component (.razor) with a minimal Blazor stub.";
    public string DefaultExtension => ".razor";
    public string Category         => "C# / .NET";
    public string IconGlyph        => "\uE8A5";
    public IReadOnlyList<string> Categories => ["C# / .NET", "Web"];

    public byte[] CreateContent() => Encoding.UTF8.GetBytes(
        "@page \"/my-page\"\n\n" +
        "<h3>MyComponent</h3>\n\n" +
        "@code {\n\n}\n");
}
