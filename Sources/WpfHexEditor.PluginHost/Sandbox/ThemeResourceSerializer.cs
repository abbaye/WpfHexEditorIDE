//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/ThemeResourceSerializer.cs
// Created: 2026-03-15
// Description:
//     Serializes the host's active WPF theme ResourceDictionary into a
//     XAML string suitable for transmission over the sandbox IPC channel.
//     The sandbox re-applies the XAML via ThemeBootstrapper so plugin
//     controls inherit the same brush/color tokens as the IDE.
//
// Architecture Notes:
//     - Only SolidColorBrush and Color entries are serialized (safe, no
//       type-converter ambiguity) — these cover all theme tokens plugins use.
//     - Recursion over MergedDictionaries ensures the full theme hierarchy
//       is captured; local overrides win (child dict visited last).
//     - Double, Thickness, and CornerRadius primitives are included via
//       the sys: namespace so layout constants are also forwarded.
// ==========================================================

using System.Collections;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Converts a WPF <see cref="ResourceDictionary"/> (theme resources) into
/// a portable XAML string that can be sent to the sandbox process over IPC.
/// </summary>
public static class ThemeResourceSerializer
{
    private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlXNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string SysNs = "clr-namespace:System;assembly=System.Runtime";

    /// <summary>
    /// Serializes all <see cref="SolidColorBrush"/>, <see cref="Color"/>,
    /// <see cref="double"/>, <see cref="Thickness"/>, and <see cref="CornerRadius"/>
    /// entries from <paramref name="resources"/> (and its merged dictionaries)
    /// into a XAML <c>&lt;ResourceDictionary&gt;</c> string.
    /// Returns <see cref="string.Empty"/> on error.
    /// </summary>
    public static string Serialize(ResourceDictionary resources)
    {
        try
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine($"<ResourceDictionary xmlns=\"{XamlNs}\"");
            sb.AppendLine($"                    xmlns:x=\"{XamlXNs}\"");
            sb.AppendLine($"                    xmlns:sys=\"{SysNs}\">");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            CollectEntries(resources, sb, seen);

            sb.AppendLine("</ResourceDictionary>");
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void CollectEntries(ResourceDictionary rd, StringBuilder sb, HashSet<string> seen)
    {
        // Recurse merged dicts first so local keys override inherited ones
        foreach (var merged in rd.MergedDictionaries)
            CollectEntries(merged, sb, seen);

        foreach (DictionaryEntry entry in rd)
        {
            if (entry.Key is not string key) continue;
            if (!seen.Add(key)) continue; // already written (local override wins)

            switch (entry.Value)
            {
                case SolidColorBrush brush:
                    sb.AppendLine(
                        $"    <SolidColorBrush x:Key=\"{EscapeXml(key)}\" " +
                        $"Color=\"{ColorToHex(brush.Color)}\"/>");
                    break;

                case Color color:
                    sb.AppendLine(
                        $"    <Color x:Key=\"{EscapeXml(key)}\">" +
                        $"{ColorToHex(color)}</Color>");
                    break;

                case double d when double.IsFinite(d):
                    sb.AppendLine(
                        $"    <sys:Double x:Key=\"{EscapeXml(key)}\">{d}</sys:Double>");
                    break;

                case Thickness t:
                    sb.AppendLine(
                        $"    <Thickness x:Key=\"{EscapeXml(key)}\">" +
                        $"{t.Left},{t.Top},{t.Right},{t.Bottom}</Thickness>");
                    break;

                case CornerRadius cr:
                    sb.AppendLine(
                        $"    <CornerRadius x:Key=\"{EscapeXml(key)}\">" +
                        $"{cr.TopLeft},{cr.TopRight},{cr.BottomRight},{cr.BottomLeft}</CornerRadius>");
                    break;
            }
        }
    }

    private static string ColorToHex(Color c)
        => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>
    /// Recursively collects all <see cref="ResourceDictionary.Source"/> URIs from
    /// <paramref name="resources"/> and its entire merged-dictionary hierarchy.
    /// Only file-based (pack://) URIs are returned; inline dictionaries are omitted
    /// because their primitive values are already captured by <see cref="Serialize"/>.
    /// Results are deduplicated; children appear before parents so the sandbox can
    /// merge them bottom-up without conflicts.
    /// </summary>
    public static IReadOnlyList<string> CollectSourceUris(ResourceDictionary resources)
    {
        var result = new List<string>();
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectUrisRecursive(resources, result, seen);
        return result;
    }

    private static void CollectUrisRecursive(
        ResourceDictionary rd, List<string> result, HashSet<string> seen)
    {
        // Depth-first: nested children first so their keys are overridable by parents.
        foreach (var merged in rd.MergedDictionaries)
            CollectUrisRecursive(merged, result, seen);

        if (rd.Source is not null)
        {
            var uri = rd.Source.OriginalString;
            if (!string.IsNullOrEmpty(uri) && seen.Add(uri))
                result.Add(uri);
        }
    }

    private static string EscapeXml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");
}
