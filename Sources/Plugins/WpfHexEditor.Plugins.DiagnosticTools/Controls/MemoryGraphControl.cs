// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Controls/MemoryGraphControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Area graph for working-set memory (MB).
//     Auto-scales Y-axis to the observed peak rounded up to nearest 64 MB.
//     Inherits all rendering from GraphControlBase.
// ==========================================================

using System.Collections.ObjectModel;

namespace WpfHexEditor.Plugins.DiagnosticTools.Controls;

/// <summary>
/// Area graph that plots the last N working-set memory samples (MB).
/// Y-axis auto-scales to the observed peak (minimum 64 MB).
/// </summary>
public sealed class MemoryGraphControl : GraphControlBase
{
    protected override double GetYMax(ObservableCollection<double> data)
        => Math.Max(64, Math.Ceiling(data.Max() / 64.0) * 64.0);

    protected override double GetDefaultYMax() => 256.0;

    protected override string LineColorKey => "DT_MemoryLineColor";
    protected override string FillColorKey => "DT_MemoryFillColor";
    protected override string UnitSuffix   => " MB";
}
