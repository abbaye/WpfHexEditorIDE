// ==========================================================
// Project: whfmt.CodeGen
// File: Program.cs
// Description: Entry point for the whfmt-codegen dotnet global tool.
// ==========================================================

using System.CommandLine;
using WhfmtCodeGen.Commands;

var rootCmd = new RootCommand("whfmt-codegen — generate strongly-typed C# parsers from .whfmt format definitions.")
{
    GenerateCommand.Build(),
    ListCommand.Build(),
};

rootCmd.Name = "whfmt-codegen";
return await rootCmd.InvokeAsync(args);
