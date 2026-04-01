// ==========================================================
// Project: WpfHexEditor.Terminal
// File: TerminalExportService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Pure static export service for terminal output.
//     Converts a list of TerminalOutputLine to six formats:
//     Plain Text, HTML, RTF/Word, ANSI, Markdown, SpreadsheetML (Excel/LibreOffice Calc).
//     Zero external NuGet dependencies — BCL only.
//
// Architecture Notes:
//     - Each format is a self-contained static method returning a string.
//     - The caller (TerminalPanelViewModel.SaveOutput) writes the string to disk.
//     - Color mapping mirrors OutputKindToBrushConverter: Standard=#CCCCCC, Error=#FF5555,
//       Warning=#FFBB33, Info=#68C5FF, Timestamp=#808080.
//     - SpreadsheetML uses System.Xml.XmlWriter (BCL). No XLSX/ZIP needed.
// ==========================================================

using System.Net;
using System.Text;
using System.Xml;

namespace WpfHexEditor.Terminal;

/// <summary>
/// Converts <see cref="TerminalOutputLine"/> collections to various export formats.
/// All methods are pure (no I/O) — the caller is responsible for writing to disk.
/// </summary>
internal static class TerminalExportService
{
    // -- Color constants (match OutputKindToBrushConverter) -----------------------

    private const string ColStandard  = "#CCCCCC";
    private const string ColError     = "#FF5555";
    private const string ColWarning   = "#FFBB33";
    private const string ColInfo      = "#68C5FF";
    private const string ColTimestamp = "#808080";

    // -- Format helpers -----------------------------------------------------------

    private static string HexColor(TerminalOutputKind kind) => kind switch
    {
        TerminalOutputKind.Error   => ColError,
        TerminalOutputKind.Warning => ColWarning,
        TerminalOutputKind.Info    => ColInfo,
        _                          => ColStandard
    };

    private static string KindLabel(TerminalOutputKind kind) => kind switch
    {
        TerminalOutputKind.Error   => "ERROR",
        TerminalOutputKind.Warning => "WARN",
        TerminalOutputKind.Info    => "INFO",
        _                          => "STD"
    };

    private static string TimestampPrefix(TerminalOutputLine line)
        => $"[{line.Timestamp:HH:mm:ss}] ";

    // ── 1. Plain Text ─────────────────────────────────────────────────────────────

    public static string ToPlainText(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        var sb = new StringBuilder(lines.Count * 80);
        foreach (var line in lines)
        {
            if (showTimestamps) sb.Append(TimestampPrefix(line));
            sb.AppendLine(line.Text);
        }
        return sb.ToString();
    }

    // ── 2. HTML ───────────────────────────────────────────────────────────────────

    public static string ToHtml(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        var sb = new StringBuilder(lines.Count * 120);
        sb.AppendLine("""
            <!DOCTYPE html>
            <html><head>
              <meta charset="UTF-8">
              <title>Terminal Output</title>
              <style>
                body { background:#1e1e1e; margin:0; padding:8px; }
                pre  { font-family:Consolas,monospace; font-size:13px; line-height:1.4;
                       white-space:pre-wrap; word-break:break-all; margin:0; }
                .ts  { color:#808080; }
                .std { color:#cccccc; }
                .err { color:#ff5555; }
                .wrn { color:#ffbb33; }
                .inf { color:#68c5ff; }
              </style>
            </head><body><pre>
            """);

        foreach (var line in lines)
        {
            if (showTimestamps)
                sb.Append($"<span class=\"ts\">{WebUtility.HtmlEncode(TimestampPrefix(line))}</span>");

            var cls = line.Kind switch
            {
                TerminalOutputKind.Error   => "err",
                TerminalOutputKind.Warning => "wrn",
                TerminalOutputKind.Info    => "inf",
                _                          => "std"
            };

            sb.AppendLine($"<span class=\"{cls}\">{WebUtility.HtmlEncode(line.Text)}</span>");
        }

        sb.AppendLine("</pre></body></html>");
        return sb.ToString();
    }

    // ── 3. RTF / Word ─────────────────────────────────────────────────────────────

    public static string ToRtf(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        var sb = new StringBuilder(lines.Count * 100);

        // RTF header: font table + color table
        sb.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0");
        sb.AppendLine(@"{\fonttbl{\f0\fmodern\fcharset0 Consolas;}}");
        // Color table: cf1=Standard, cf2=Error, cf3=Warning, cf4=Info, cf5=Timestamp
        sb.AppendLine(@"{\colortbl ;" +
                      @"\red204\green204\blue204;" +   // cf1 Standard
                      @"\red255\green85\blue85;"   +   // cf2 Error
                      @"\red255\green187\blue51;"  +   // cf3 Warning
                      @"\red104\green197\blue255;" +   // cf4 Info
                      @"\red128\green128\blue128;" +   // cf5 Timestamp
                      @"}");
        sb.AppendLine(@"\f0\fs22\sl240\slmult1");

        foreach (var line in lines)
        {
            if (showTimestamps)
                sb.Append($@"{{\cf5 {RtfEscape(TimestampPrefix(line))}}}");

            int cf = line.Kind switch
            {
                TerminalOutputKind.Error   => 2,
                TerminalOutputKind.Warning => 3,
                TerminalOutputKind.Info    => 4,
                _                          => 1
            };

            sb.AppendLine($@"{{\cf{cf} {RtfEscape(line.Text)}}}\line");
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Escapes a string for embedding in an RTF stream.
    /// Backslash, braces are prefixed with backslash; non-ASCII chars use \'hh notation.
    /// </summary>
    private static string RtfEscape(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (c == '\\' || c == '{' || c == '}')
                sb.Append('\\').Append(c);
            else if (c > 127)
                sb.Append($@"\'{(int)c:x2}");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    // ── 4. ANSI Text ──────────────────────────────────────────────────────────────

    public static string ToAnsi(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        const string Reset = "\x1B[0m";
        const string Dim   = "\x1B[90m"; // timestamp

        var sb = new StringBuilder(lines.Count * 80);
        foreach (var line in lines)
        {
            if (showTimestamps)
                sb.Append(Dim).Append(TimestampPrefix(line)).Append(Reset);

            string ansi = line.Kind switch
            {
                TerminalOutputKind.Error   => "\x1B[91m",
                TerminalOutputKind.Warning => "\x1B[93m",
                TerminalOutputKind.Info    => "\x1B[94m",
                _                          => "\x1B[0m"
            };

            sb.Append(ansi).Append(line.Text).AppendLine(Reset);
        }
        return sb.ToString();
    }

    // ── 5. Markdown ───────────────────────────────────────────────────────────────

    public static string ToMarkdown(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        var sb = new StringBuilder(lines.Count * 100);

        sb.AppendLine("# Terminal Output");
        sb.AppendLine($"> Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (showTimestamps)
        {
            sb.AppendLine("| Timestamp | Kind | Message |");
            sb.AppendLine("|-----------|------|---------|");
            foreach (var line in lines)
                sb.AppendLine($"| `{line.Timestamp:HH:mm:ss}` | **{KindLabel(line.Kind)}** | {MdEscape(line.Text)} |");
        }
        else
        {
            sb.AppendLine("| Kind | Message |");
            sb.AppendLine("|------|---------|");
            foreach (var line in lines)
                sb.AppendLine($"| **{KindLabel(line.Kind)}** | {MdEscape(line.Text)} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes pipe characters and truncates very long cells for Markdown tables.
    /// </summary>
    private static string MdEscape(string text)
    {
        var escaped = text.Replace("|", @"\|").Replace("\r", "").Replace("\n", " ");
        return escaped.Length > 200 ? escaped[..200] + "…" : escaped;
    }

    // ── 6. SpreadsheetML (Excel 2003 XML / LibreOffice Calc) ─────────────────────

    public static string ToSpreadsheetMl(IReadOnlyList<TerminalOutputLine> lines, bool showTimestamps)
    {
        var ms  = new System.IO.MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };

        using (var xw = XmlWriter.Create(ms, settings))
        {
            xw.WriteProcessingInstruction("mso-application", "progid=\"Excel.Sheet\"");

            xw.WriteStartElement("Workbook", "urn:schemas-microsoft-com:office:spreadsheet");
            xw.WriteAttributeString("xmlns", "ss", null, "urn:schemas-microsoft-com:office:spreadsheet");
            xw.WriteAttributeString("xmlns", "x",  null, "urn:schemas-microsoft-com:office:excel");

            WriteSpreadsheetStyles(xw);
            WriteSpreadsheetData(xw, lines, showTimestamps);

            xw.WriteEndElement(); // Workbook
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteSpreadsheetStyles(XmlWriter xw)
    {
        xw.WriteStartElement("Styles", "urn:schemas-microsoft-com:office:spreadsheet");

        WriteStyle(xw, "sHDR", "#FFFFFF", bold: true,  bg: "#333333");
        WriteStyle(xw, "sSTD", ColStandard,  bold: false, bg: "#1E1E1E");
        WriteStyle(xw, "sERR", ColError,     bold: false, bg: "#1E1E1E");
        WriteStyle(xw, "sWRN", ColWarning,   bold: false, bg: "#1E1E1E");
        WriteStyle(xw, "sINF", ColInfo,      bold: false, bg: "#1E1E1E");
        WriteStyle(xw, "sTS",  ColTimestamp, bold: false, bg: "#1E1E1E");

        xw.WriteEndElement(); // Styles
    }

    private static void WriteStyle(XmlWriter xw, string id, string color, bool bold, string bg)
    {
        xw.WriteStartElement("Style", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "ID", null, id);

        xw.WriteStartElement("Font", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "Color", null, color);
        xw.WriteAttributeString("ss", "FontName", null, "Consolas");
        xw.WriteAttributeString("ss", "Size", null, "10");
        if (bold) xw.WriteAttributeString("ss", "Bold", null, "1");
        xw.WriteEndElement();

        xw.WriteStartElement("Interior", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "Color",   null, bg);
        xw.WriteAttributeString("ss", "Pattern", null, "Solid");
        xw.WriteEndElement();

        xw.WriteEndElement(); // Style
    }

    private static void WriteSpreadsheetData(
        XmlWriter xw,
        IReadOnlyList<TerminalOutputLine> lines,
        bool showTimestamps)
    {
        xw.WriteStartElement("Worksheet", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "Name", null, "Terminal Output");

        xw.WriteStartElement("Table", "urn:schemas-microsoft-com:office:spreadsheet");

        // Header row
        xw.WriteStartElement("Row", "urn:schemas-microsoft-com:office:spreadsheet");
        if (showTimestamps) WriteCell(xw, "Timestamp", "sHDR");
        WriteCell(xw, "Kind",    "sHDR");
        WriteCell(xw, "Message", "sHDR");
        xw.WriteEndElement();

        // Data rows
        foreach (var line in lines)
        {
            string styleId = line.Kind switch
            {
                TerminalOutputKind.Error   => "sERR",
                TerminalOutputKind.Warning => "sWRN",
                TerminalOutputKind.Info    => "sINF",
                _                          => "sSTD"
            };

            xw.WriteStartElement("Row", "urn:schemas-microsoft-com:office:spreadsheet");
            if (showTimestamps)
                WriteCell(xw, line.Timestamp.ToString("HH:mm:ss"), "sTS");
            WriteCell(xw, KindLabel(line.Kind), styleId);
            WriteCell(xw, line.Text,             styleId);
            xw.WriteEndElement();
        }

        xw.WriteEndElement(); // Table
        xw.WriteEndElement(); // Worksheet
    }

    private static void WriteCell(XmlWriter xw, string value, string styleId)
    {
        xw.WriteStartElement("Cell", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "StyleID", null, styleId);

        xw.WriteStartElement("Data", "urn:schemas-microsoft-com:office:spreadsheet");
        xw.WriteAttributeString("ss", "Type", null, "String");
        xw.WriteValue(value);
        xw.WriteEndElement();

        xw.WriteEndElement(); // Cell
    }
}
