// ==========================================================
// Project: whfmt.CodeGen
// File: Commands/GenerateCommand.cs
// Description: `whfmt-codegen generate` — generates a C# parser from a .whfmt.
// ==========================================================

using System.CommandLine;
using WpfHexEditor.Core.Definitions;
using WhfmtCodeGen.Generator;

namespace WhfmtCodeGen.Commands;

internal static class GenerateCommand
{
    internal static Command Build()
    {
        var formatArg = new Argument<string>("format", "Format name, extension, or path to a .whfmt file.");

        var nsOpt      = new Option<string>(["--namespace", "-n"], () => "Generated.Parsers", "C# namespace for the generated class.");
        var classOpt   = new Option<string?>(["--class",     "-c"], "Class name (default: <FormatName>Parser).");
        var outputOpt  = new Option<string?>(["--output",    "-o"], "Output file path (default: stdout).");
        var validateOpt= new Option<bool>   (["--validate"],        "Include assertion + checksum validation in the generated parser.");
        var asyncOpt   = new Option<bool>   (["--async"],           "Generate async Read methods (uses Stream.ReadAsync).");

        var cmd = new Command("generate", "Generate a strongly-typed C# parser class from a .whfmt format definition.")
        {
            formatArg, nsOpt, classOpt, outputOpt, validateOpt, asyncOpt
        };

        cmd.SetHandler(async (format, ns, className, output, validate, async_) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;

            // Resolve entry
            var entry = catalog.GetAll().FirstOrDefault(e =>
                e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
                e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) ||
                (e.ResourceKey?.EndsWith(format, StringComparison.OrdinalIgnoreCase) ?? false));

            if (entry is null)
            {
                // Try as file path
                if (File.Exists(format))
                {
                    string code = ParserGenerator.GenerateFromJson(
                        await File.ReadAllTextAsync(format),
                        ns, className ?? Path.GetFileNameWithoutExtension(format) + "Parser",
                        validate, async_);
                    await WriteOutput(output, code);
                    return;
                }

                Console.Error.WriteLine($"  Format '{format}' not found in catalog and not a valid .whfmt path.");
                Environment.Exit(2);
                return;
            }

            string? json = catalog.GetJson(entry.ResourceKey);
            if (json is null)
            {
                Console.Error.WriteLine($"  No full definition available for '{entry.Name}'.");
                Environment.Exit(2);
                return;
            }

            string resolvedClass = className ?? SanitizeIdentifier(entry.Name) + "Parser";
            string generated = ParserGenerator.GenerateFromJson(json, ns, resolvedClass, validate, async_);
            await WriteOutput(output, generated);
        },
        formatArg, nsOpt, classOpt, outputOpt, validateOpt, asyncOpt);

        return cmd;
    }

    private static async Task WriteOutput(string? path, string code)
    {
        if (path is not null)
        {
            await File.WriteAllTextAsync(path, code);
            Console.WriteLine($"  Generated: {Path.GetFullPath(path)}");
        }
        else
        {
            Console.WriteLine(code);
        }
    }

    private static string SanitizeIdentifier(string name)
    {
        var sb = new System.Text.StringBuilder();
        bool nextUpper = true;
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(nextUpper ? char.ToUpper(c) : c); nextUpper = false; }
            else nextUpper = true;
        }
        return sb.ToString();
    }
}
