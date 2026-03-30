// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/BreakpointRowEx.cs
// Description:
//     Extended breakpoint row model for the Breakpoint Explorer panel.
//     INPC for IsEnabled/HitCount live updates.
// ==========================================================

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.Debugger.Models;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class BreakpointRowEx : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int  _hitCount;

    public string  FilePath     { get; init; } = string.Empty;
    public string  FileName     { get; init; } = string.Empty;
    public string  ProjectName  { get; init; } = string.Empty;
    public int     Line         { get; init; }
    public string? Condition    { get; init; }
    public bool    IsVerified   { get; init; }

    // Extended settings (populated from BreakpointLocation)
    public BpConditionKind ConditionKind  { get; init; }
    public BpConditionMode ConditionMode  { get; init; }
    public BpHitCountOp    HitCountOp     { get; init; }
    public int             HitCountTarget { get; init; } = 1;
    public string?         FilterExpr     { get; init; }
    public bool            HasAction      { get; init; }
    public string?         LogMessage     { get; init; }
    public bool            ContinueExecution { get; init; } = true;
    public bool            DisableOnceHit { get; init; }
    public string?         DependsOnBpKey { get; init; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeLabel)); OnPropertyChanged(nameof(TypeGlyph)); }
    }

    public int HitCount
    {
        get => _hitCount;
        set { if (_hitCount == value) return; _hitCount = value; OnPropertyChanged(); }
    }

    /// <summary>"Conditional", "Hit Count", "Filter", "Tracepoint", "Standard", or "Disabled".</summary>
    public string TypeLabel
    {
        get
        {
            if (!IsEnabled) return "Disabled";
            return ConditionKind switch
            {
                BpConditionKind.ConditionalExpression => "Conditional",
                BpConditionKind.HitCount              => "Hit Count",
                BpConditionKind.Filter                => "Filter",
                _ => HasAction ? "Tracepoint" : "Standard",
            };
        }
    }

    /// <summary>Segoe MDL2 Assets glyph for the breakpoint type.</summary>
    public string TypeGlyph
    {
        get
        {
            if (!IsEnabled) return "\uECC9";   // Circle outline — disabled
            return ConditionKind switch
            {
                BpConditionKind.ConditionalExpression => "\uEA3F", // Warning — conditional
                BpConditionKind.HitCount              => "\uEA3F", // Warning — hit count
                BpConditionKind.Filter                => "\uEA3F", // Warning — filter
                _ => HasAction ? "\uE756" : "\uEA3B",              // Diamond (tracepoint) / Filled circle
            };
        }
    }

    public string DisplayLocation => $"{FileName}:{Line}";

    /// <summary>Up to 5 source lines centred on the breakpoint line. Empty if file not found.</summary>
    public string SourcePreview => BuildSourcePreview(FilePath, Line, contextLines: 2);

    private static string BuildSourcePreview(string filePath, int line1, int contextLines)
    {
        try
        {
            if (!File.Exists(filePath)) return string.Empty;
            var lines = File.ReadAllLines(filePath);
            int start = Math.Max(0, line1 - 1 - contextLines);
            int end   = Math.Min(lines.Length - 1, line1 - 1 + contextLines);
            return string.Join("\n", lines[start..(end + 1)]);
        }
        catch { return string.Empty; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
