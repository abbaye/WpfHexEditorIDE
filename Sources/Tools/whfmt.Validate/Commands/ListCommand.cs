// ==========================================================
// Project: whfmt.Validate
// File: Commands/ListCommand.cs
// Description: `whfmt list` — lists all formats in the catalog.
// ==========================================================

using System.CommandLine;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Query;

namespace WhfmtValidate.Commands;

internal static class ListCommand
{
    internal static Command Build()
    {
        var categoryOpt = new Option<string?>(["--category", "-c"], "Filter by category (e.g. Archives, Images, Game).");
        var searchOpt   = new Option<string?>(["--search",   "-s"], "Filter by name substring.");
        var jsonOpt     = new Option<bool>   (["--json",     "-j"], "Output as JSON array.");

        var cmd = new Command("list", "List all 790+ supported file formats in the catalog.")
        {
            categoryOpt, searchOpt, jsonOpt
        };

        cmd.SetHandler((category, search, json) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;
            var query   = catalog.Query();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.InCategory(category);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Containing(search);

            var entries = query.OrderByName().Execute();

            if (json)
            {
                var list = entries.Select(e => new { e.Name, e.Category, e.Extensions, e.QualityScore });
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            var groups = entries.GroupBy(e => e.Category).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                Console.WriteLine($"\n  {g.Key} ({g.Count()})");
                Console.WriteLine($"  {"─".PadRight(40, '─')}");
                foreach (var e in g)
                {
                    string exts = e.Extensions.Count > 0 ? string.Join(", ", e.Extensions) : "(no extension)";
                    Console.WriteLine($"    {e.Name,-35} {exts}");
                }
            }

            Console.WriteLine($"\n  Total: {entries.Count} formats\n");
        },
        categoryOpt, searchOpt, jsonOpt);

        return cmd;
    }
}
