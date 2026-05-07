// ==========================================================
// Project: whfmt.CodeGen
// File: Commands/ListCommand.cs
// Description: `whfmt-codegen list` — lists formats available for code generation.
// ==========================================================

using System.CommandLine;
using WpfHexEditor.Core.Definitions;

namespace WhfmtCodeGen.Commands;

internal static class ListCommand
{
    internal static Command Build()
    {
        var searchOpt   = new Option<string?>(["--search", "-s"], "Filter by name substring.");
        var categoryOpt = new Option<string?>(["--category","-c"], "Filter by category.");

        var cmd = new Command("list", "List all formats available for C# parser generation.")
        {
            searchOpt, categoryOpt
        };

        cmd.SetHandler((search, category) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;
            var entries = catalog.GetAll()
                .Where(e => !e.IsTextFormat)
                .Where(e => search is null || e.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(e => category is null || e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Category).ThenBy(e => e.Name);

            string? lastCat = null;
            foreach (var e in entries)
            {
                if (e.Category != lastCat) { Console.WriteLine($"\n  {e.Category}"); lastCat = e.Category; }
                string exts = e.Extensions.Count > 0 ? string.Join(", ", e.Extensions) : "";
                Console.WriteLine($"    {e.Name,-40} {exts}");
            }
            Console.WriteLine();
        },
        searchOpt, categoryOpt);

        return cmd;
    }
}
