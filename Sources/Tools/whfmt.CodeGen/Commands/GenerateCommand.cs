// ==========================================================
// Project: whfmt.CodeGen
// File: Commands/GenerateCommand.cs
// Description: `whfmt-codegen generate` — generates typed C# (or F#/Rust) parser from a .whfmt.
// ==========================================================

using System.CommandLine;
using WpfHexEditor.Core.Definitions;
using WhfmtCodeGen.Generator;

namespace WhfmtCodeGen.Commands;

internal static class GenerateCommand
{
    internal static Command Build()
    {
        var formatArg  = new Argument<string>("format", "Format name, extension, or path to a .whfmt file.");

        var nsOpt      = new Option<string>(["--namespace", "-n"], () => "Generated.Parsers", "C# namespace for the generated class.");
        var classOpt   = new Option<string?>(["--class",    "-c"], "Class name (default: <FormatName>Parser).");
        var outputOpt  = new Option<string?>(["--output",   "-o"], "Output file path (default: stdout). Ignored when --project is used.");
        var projectOpt = new Option<string?>(["--project",  "-p"], "Output directory for a complete multi-file C# project (overrides --output).");
        var validateOpt= new Option<bool>   (["--validate"],       "Include signature assertion + checksum validation in the generated parser.");
        var asyncOpt   = new Option<bool>   (["--async"],          "Generate async Parse methods (uses Stream.ReadExactlyAsync).");
        var langOpt    = new Option<string> (["--lang",     "-l"], () => "csharp",
            "Output language: csharp (default), csharp-span (zero-alloc), fsharp, rust, vb.");

        var cmd = new Command("generate", "Generate a strongly-typed parser from a .whfmt format definition.")
        {
            formatArg, nsOpt, classOpt, outputOpt, projectOpt, validateOpt, asyncOpt, langOpt
        };

        cmd.SetHandler(async (format, ns, className, output, project, validate, async_, lang) =>
        {
            var catalog = EmbeddedFormatCatalog.Instance;
            OutputLanguage language = ParseLanguage(lang);

            // Resolve entry — restrict to .whfmt resources so .grammar (Synalysis XML) files
            // with the same basename (bmp, elf, flac, gif, jpeg, mp3, png, sqlite, zip) are never matched.
            var entry = catalog.GetAll()
                .Where(e => e.ResourceKey?.EndsWith(".whfmt", StringComparison.OrdinalIgnoreCase) == true)
                .FirstOrDefault(e =>
                    e.Name.Equals(format, StringComparison.OrdinalIgnoreCase) ||
                    e.Extensions.Any(x => x.TrimStart('.').Equals(format.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) ||
                    (e.ResourceKey?.EndsWith("." + format + ".whfmt", StringComparison.OrdinalIgnoreCase) ?? false));

            string json;
            string resolvedClass;

            if (entry is null)
            {
                if (!File.Exists(format))
                {
                    Console.Error.WriteLine($"  Format '{format}' not found in catalog and not a valid .whfmt path.");
                    Environment.Exit(2);
                    return;
                }
                json = await File.ReadAllTextAsync(format);
                resolvedClass = className ?? Path.GetFileNameWithoutExtension(format) + "Parser";
            }
            else
            {
                json = catalog.GetJson(entry.ResourceKey) ?? "";
                if (string.IsNullOrEmpty(json))
                {
                    Console.Error.WriteLine($"  No full definition available for '{entry.Name}'.");
                    Environment.Exit(2);
                    return;
                }
                resolvedClass = className ?? SanitizeIdentifier(entry.Name) + "Parser";
            }

            // Multi-file project mode
            if (project is not null)
            {
                await ProjectEmitter.EmitAsync(json, ns, resolvedClass, project, validate, async_);
                return;
            }

            // Single-file / stdout mode
            string generated = ParserGenerator.GenerateFromJson(json, ns, resolvedClass, validate, async_, language);
            await WriteOutput(output, generated);
        },
        formatArg, nsOpt, classOpt, outputOpt, projectOpt, validateOpt, asyncOpt, langOpt);

        return cmd;
    }

    private static OutputLanguage ParseLanguage(string lang) => lang.ToLowerInvariant() switch
    {
        "csharp-span" or "span"                    => OutputLanguage.CSharpSpan,
        "fsharp"      or "fs"                      => OutputLanguage.FSharp,
        "rust"        or "rs"                      => OutputLanguage.Rust,
        "vb"          or "vbnet" or "visualbasic"  => OutputLanguage.VisualBasic,
        _                                          => OutputLanguage.CSharp,
    };

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
