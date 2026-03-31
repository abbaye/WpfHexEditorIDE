// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: Commands/ParsedExportCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — export parsed fields as JSON or CSV.
// ==========================================================

using System.IO;
using System.Text;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.ParsedFields.Commands;

internal sealed class ParsedExportCommand : PluginTerminalCommandBase
{
    public override string CommandName => "parsed-export";
    public override string Description => "Export parsed fields as JSON or CSV to stdout or a file.";
    public override string Usage       => "parsed-export [json|csv] [output-file]";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var svc = ctx.IDE.FormatParsing;

        if (svc is null || !svc.HasParsedFields)
        {
            output.WriteWarning("No parsed fields available. Open a file with a known format first.");
            return Task.FromResult(0);
        }

        var format     = args.Length > 0 ? args[0].ToLowerInvariant() : "json";
        var outputFile = args.Length > 1 ? args[1] : null;

        if (format is not ("json" or "csv"))
        {
            output.WriteError($"Unknown format '{format}'. Supported: json, csv");
            return Task.FromResult(1);
        }

        var fields  = svc.GetParsedFields();
        var content = format == "json" ? SerializeJson(fields) : SerializeCsv(fields);

        if (outputFile is not null)
        {
            File.WriteAllText(outputFile, content, Encoding.UTF8);
            output.WriteInfo($"Exported {fields.Count} fields → {outputFile}");
        }
        else
        {
            output.WriteLine(content);
        }

        return Task.FromResult(0);
    }

    private static string SerializeJson(IReadOnlyList<ParsedFieldViewModel> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            sb.Append($"  {{ \"name\": {JsonStr(f.Name)}, \"type\": {JsonStr(f.ValueType)}, " +
                      $"\"offset\": {f.Offset}, \"length\": {f.Length}, \"value\": {JsonStr(f.FormattedValue)} }}");
            if (i < fields.Count - 1) sb.Append(',');
            sb.AppendLine();
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string SerializeCsv(IReadOnlyList<ParsedFieldViewModel> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Type,Offset,Length,Value");
        foreach (var f in fields)
            sb.AppendLine($"{CsvEsc(f.Name)},{CsvEsc(f.ValueType)},{f.Offset},{f.Length},{CsvEsc(f.FormattedValue)}");
        return sb.ToString();
    }

    private static string JsonStr(string? s)
        => s is null ? "null" : $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string CsvEsc(string? s)
    {
        if (s is null) return "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
