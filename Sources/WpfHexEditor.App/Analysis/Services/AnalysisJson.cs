// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/AnalysisJson.cs
// Description: Shared JsonSerializerOptions used by every Analysis
//              persistence service (snapshot history, baseline, options).
// ==========================================================

using System.Text.Json;

namespace WpfHexEditor.App.Analysis.Services;

internal static class AnalysisJson
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
