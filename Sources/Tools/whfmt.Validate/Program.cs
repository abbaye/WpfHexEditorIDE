// ==========================================================
// Project: whfmt.Validate
// File: Program.cs
// Description: Entry point for the whfmt dotnet global tool.
//              Commands: validate, list, info
// ==========================================================

using System.CommandLine;
using System.Text;
using WhfmtValidate.Commands;

// B7: force UTF-8 console output so emoji/accented status glyphs render correctly
// on Windows (default code page is CP-1252 in non-UTF8 consoles). No-op on POSIX.
Console.OutputEncoding = Encoding.UTF8;

var rootCmd = new RootCommand("whfmt — binary file format validator powered by 790+ whfmt definitions.")
{
    ValidateCommand.Build(),
    ListCommand.Build(),
    InfoCommand.Build(),
    RepairCommand.Build(),
    LintExpressionsCommand.Build(),
};

rootCmd.Name = "whfmt";
return await rootCmd.InvokeAsync(args);
