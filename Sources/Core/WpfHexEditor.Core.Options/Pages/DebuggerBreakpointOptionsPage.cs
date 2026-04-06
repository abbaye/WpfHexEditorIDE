// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/DebuggerBreakpointOptionsPage.cs
// Description:
//     Options page for Debugger > Breakpoints settings.
//     Section 1 — VS Interop: auto-import, auto-export, export path.
//     Section 2 — Storage: solution-scoped vs global.
// Architecture:
//     Code-only UserControl implementing IOptionsPage.
//     Reads/writes AppSettings.Debugger VS interop properties.
//     Registered in OptionsPageRegistry under "Debugger" / "Breakpoints".
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Debugger > Breakpoints.
/// </summary>
public sealed class DebuggerBreakpointOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly CheckBox _autoImport;
    private readonly CheckBox _autoExport;
    private readonly TextBox  _exportPath;

    public DebuggerBreakpointOptionsPage()
    {
        Padding = new Thickness(16);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        // ── Section 1: VS Interop ─────────────────────────────────────────
        stack.Children.Add(SectionHeader("Visual Studio Interop"));

        _autoImport = MakeCheckBox(
            "Auto-import VS breakpoints when .whide store is empty",
            "On first solution open, if no .whide/breakpoints.json exists, look for VS-exported XML in the .vs/ folder and import automatically.");
        stack.Children.Add(_autoImport);

        _autoExport = MakeCheckBox(
            "Auto-export to VS XML on every save",
            "Whenever breakpoints are saved to .whide, also write a VS-compatible XML file alongside for interop.");
        stack.Children.Add(_autoExport);

        var pathLabel = new TextBlock
        {
            Text   = "VS export relative path:",
            Margin = new Thickness(0, 8, 0, 4),
        };
        stack.Children.Add(pathLabel);

        _exportPath = new TextBox
        {
            Width               = 300,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip             = "Relative to solution directory. Default: .whide/breakpoints-vs.xml"
        };
        _exportPath.TextChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        stack.Children.Add(_exportPath);

        // ── Section 2: Info ───────────────────────────────────────────────
        stack.Children.Add(SectionHeader("Storage"));

        var infoText = new TextBlock
        {
            Text         = "Breakpoints are stored in .whide/breakpoints.json (per-solution) or in global settings when no solution is open. This is the central storage — VS XML import/export is a convenience tool.",
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.7,
            Margin       = new Thickness(0, 4, 0, 0),
        };
        stack.Children.Add(infoText);

        scroll.Content = stack;
        Content = scroll;
    }

    public void Load(AppSettings settings)
    {
        _autoImport.IsChecked = settings.Debugger.AutoImportVsBreakpoints;
        _autoExport.IsChecked = settings.Debugger.AutoExportVsXml;
        _exportPath.Text      = settings.Debugger.VsExportRelativePath;
    }

    public void Flush(AppSettings settings)
    {
        settings.Debugger.AutoImportVsBreakpoints = _autoImport.IsChecked == true;
        settings.Debugger.AutoExportVsXml         = _autoExport.IsChecked == true;

        var path = _exportPath.Text?.Trim();
        if (!string.IsNullOrEmpty(path))
            settings.Debugger.VsExportRelativePath = path;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => OptionsPageHelper.SectionHeader(text);

    private CheckBox MakeCheckBox(string content, string? tooltip = null)
    {
        var cb = new CheckBox
        {
            Content = content,
            Margin  = new Thickness(0, 4, 0, 0),
            ToolTip = tooltip,
        };
        cb.Checked   += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        cb.Unchecked += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        return cb;
    }
}
