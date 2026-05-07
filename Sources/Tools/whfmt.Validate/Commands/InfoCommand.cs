// ==========================================================
// Project: whfmt.Validate
// File: Commands/InfoCommand.cs
// Description: `whfmt info` — shows full metadata for a specific format.
// ==========================================================

using System.CommandLine;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;

namespace WhfmtValidate.Commands;

internal static class InfoCommand
{
    internal static Command Build()
    {
        var formatArg = new Argument<string>("format", "Format name or file extension to look up.");
        var jsonOpt   = new Option<bool>(["--json", "-j"], "Output as JSON.");

        var cmd = new Command("info", "Show detailed metadata for a format from the catalog.")
        {
            formatArg, jsonOpt
        };

        cmd.SetHandler((format, json) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;

            var entry = catalog.GetAll()
                .FirstOrDefault(e =>
                    e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
                    e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));

            if (entry is null)
            {
                Console.Error.WriteLine($"  No format found matching '{format}'.");
                Environment.Exit(2);
                return;
            }

            if (json)
            {
                var meta = entry.GetAllMetadata(catalog);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    entry.Name,
                    entry.Category,
                    entry.Extensions,
                    entry.QualityScore,
                    entry.IsTextFormat,
                    entry.MimeTypes,
                    entry.DiffMode,
                    entry.PreferredEditor,
                    Forensic      = meta?.Forensic,
                    AiHints       = meta?.AiHints,
                    TechnicalDetails = meta?.TechnicalDetails
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"  Format   : {entry.Name}");
            Console.WriteLine($"  Category : {entry.Category}");
            Console.WriteLine($"  Extensions: {string.Join(", ", entry.Extensions)}");
            Console.WriteLine($"  Quality  : {entry.QualityScore}/100");
            Console.WriteLine($"  Text     : {entry.IsTextFormat}");
            if (entry.MimeTypes?.Count > 0)
                Console.WriteLine($"  MIME     : {string.Join(", ", entry.MimeTypes)}");
            if (!string.IsNullOrWhiteSpace(entry.DiffMode))
                Console.WriteLine($"  DiffMode : {entry.DiffMode}");
            if (!string.IsNullOrWhiteSpace(entry.PreferredEditor))
                Console.WriteLine($"  Editor   : {entry.PreferredEditor}");

            if (entry.Signatures?.Count > 0)
            {
                Console.WriteLine($"  Signatures:");
                foreach (var s in entry.Signatures)
                    Console.WriteLine($"    {s.Value} @ offset {s.Offset} (weight {s.Weight:F2})");
            }

            var metadata = entry.GetAllMetadata(catalog);
            if (metadata?.Forensic is { } forensic && !string.IsNullOrWhiteSpace(forensic.RiskLevel))
                Console.WriteLine($"  Forensic : {forensic.RiskLevel.ToUpper()} risk — {forensic.Category}");

            if (metadata?.AiHints is { } hints && !string.IsNullOrWhiteSpace(hints.AnalysisContext))
                Console.WriteLine($"  AI Hints : {hints.AnalysisContext}");

            Console.WriteLine();
        },
        formatArg, jsonOpt);

        return cmd;
    }
}
