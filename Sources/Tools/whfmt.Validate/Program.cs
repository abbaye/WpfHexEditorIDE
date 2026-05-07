// ==========================================================
// Project: whfmt.Validate
// File: Program.cs
// Description: Entry point for the whfmt dotnet global tool.
//              Commands: validate, list, info
// ==========================================================

using System.CommandLine;
using WhfmtValidate.Commands;

var rootCmd = new RootCommand("whfmt — binary file format validator powered by 790+ whfmt definitions.")
{
    ValidateCommand.Build(),
    ListCommand.Build(),
    InfoCommand.Build(),
};

rootCmd.Name = "whfmt";
return await rootCmd.InvokeAsync(args);
