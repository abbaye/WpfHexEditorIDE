// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Controls/CpuGraphControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Area graph for CPU % (0–100 fixed scale).
//     Inherits all rendering from GraphControlBase.
// ==========================================================

using System.Collections.ObjectModel;

namespace WpfHexEditor.Plugins.DiagnosticTools.Controls;

/// <summary>
/// Area graph that plots the last N CPU % samples (0–100 % fixed Y axis).
/// </summary>
public sealed class CpuGraphControl : GraphControlBase
{
    protected override double GetYMax(ObservableCollection<double> data) => 100.0;
    protected override string LineColorKey => "DT_CpuLineColor";
    protected override string FillColorKey => "DT_CpuFillColor";
    protected override string UnitSuffix   => " %";
}
