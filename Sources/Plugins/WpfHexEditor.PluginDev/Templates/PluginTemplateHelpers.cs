// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/PluginTemplateHelpers.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Shared helper utilities used by all IPluginTemplate implementations.
// ==========================================================

using System.Text;

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Shared utilities for plugin scaffold templates.
/// </summary>
internal static class PluginTemplateHelpers
{
    /// <summary>
    /// Converts an arbitrary string into a valid C# identifier fragment
    /// suitable for use as a class or namespace name.
    /// </summary>
    public static string MakeSafeName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "MyPlugin";

        var sb = new StringBuilder();
        var first = true;
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                // First char must not be a digit.
                if (first && char.IsDigit(c))
                    sb.Append('_');

                sb.Append(c);
                first = false;
            }
            else if (!first)
            {
                sb.Append('_');
            }
        }

        return sb.ToString().Trim('_');
    }
}
