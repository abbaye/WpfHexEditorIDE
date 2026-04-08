// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/MetricsDashboardPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Phase 7 Metrics Dashboard panel code-behind.
//     Hosts MetricsDashboardViewModel as DataContext and wires
//     UI events (row selection, Export CSV) to ViewModel methods.
//     Raises NodeFocusRequested when the user clicks a row so the
//     diagram canvas can pan/zoom to the corresponding node.
//
// Architecture Notes:
//     Pattern: MVVM. ViewModel is instantiated here; no DI container needed.
//     MetricsDashboardViewModel and MetricsRow are declared in this file
//     (inner types) to keep the panel self-contained inside the plugin.
//     Row background colors are frozen SolidColorBrush instances — zero
//     allocation per row after initial construction.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

// ---------------------------------------------------------------------------
// MetricsRow — immutable data row bound to one ListView item
// ---------------------------------------------------------------------------

/// <summary>
/// Flat projection of a <see cref="ClassNode"/> used as a ListView row.
/// </summary>
/// <param name="Node">The originating class node.</param>
/// <param name="Name">Type name.</param>
/// <param name="Kind">Kind label (e.g. "class", "interface").</param>
/// <param name="Ca">Afferent coupling.</param>
/// <param name="Ce">Efferent coupling.</param>
/// <param name="I">Instability index in [0, 1].</param>
/// <param name="MemberCount">Total declared member count.</param>
/// <param name="RowBackground">Background brush derived from instability level.</param>
public sealed record MetricsRow(
    ClassNode Node,
    string    Name,
    string    Kind,
    int       Ca,
    int       Ce,
    double    I,
    int       MemberCount,
    Brush     RowBackground);

// ---------------------------------------------------------------------------
// MetricsDashboardViewModel
// ---------------------------------------------------------------------------

/// <summary>
/// ViewModel for <see cref="MetricsDashboardPanel"/>.
/// Exposes a sorted, observable collection of <see cref="MetricsRow"/> instances.
/// </summary>
public sealed class MetricsDashboardViewModel : INotifyPropertyChanged
{
    // ------------------------------------------------------------------
    // Static brushes — instability bands (frozen = thread-safe, no alloc)
    // ------------------------------------------------------------------

    private static readonly Brush s_greenBrush  = MakeBrush(Color.FromArgb(60, 80,  180,  80));
    private static readonly Brush s_yellowBrush = MakeBrush(Color.FromArgb(60, 200, 180,  40));
    private static readonly Brush s_redBrush    = MakeBrush(Color.FromArgb(60, 200,  60,  60));
    private static readonly Brush s_clearBrush  = Brushes.Transparent;

    private static Brush MakeBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    // ------------------------------------------------------------------
    // Backing fields
    // ------------------------------------------------------------------

    private List<MetricsRow>  _source      = [];
    private int               _sortModeIndex;

    // ------------------------------------------------------------------
    // Public properties
    // ------------------------------------------------------------------

    /// <summary>Sorted rows bound to the ListView.</summary>
    public ObservableCollection<MetricsRow> Rows { get; } = [];

    /// <summary>
    /// Index matching the sort ComboBox:
    /// 0 = Instability (desc), 1 = Ca (desc), 2 = Ce (desc), 3 = Member Count (desc).
    /// Setting this property triggers an immediate re-sort.
    /// </summary>
    public int SortModeIndex
    {
        get => _sortModeIndex;
        set
        {
            if (_sortModeIndex == value) return;
            _sortModeIndex = value;
            OnPropertyChanged();
            ApplySort();
        }
    }

    // ------------------------------------------------------------------
    // Public methods
    // ------------------------------------------------------------------

    /// <summary>
    /// Rebuilds <see cref="Rows"/> from the classes contained in
    /// <paramref name="doc"/>. Pass <see langword="null"/> to clear the list.
    /// </summary>
    public void SetDocument(DiagramDocument? doc)
    {
        _source = doc is null
            ? []
            : doc.Classes.Select(BuildRow).ToList();

        ApplySort();
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private void ApplySort()
    {
        IEnumerable<MetricsRow> sorted = _sortModeIndex switch
        {
            1 => _source.OrderByDescending(r => r.Ca).ThenBy(r => r.Name),
            2 => _source.OrderByDescending(r => r.Ce).ThenBy(r => r.Name),
            3 => _source.OrderByDescending(r => r.MemberCount).ThenBy(r => r.Name),
            _ => _source.OrderByDescending(r => r.I).ThenBy(r => r.Name),
        };

        Rows.Clear();
        foreach (var row in sorted)
            Rows.Add(row);
    }

    private static MetricsRow BuildRow(ClassNode node)
    {
        var m    = node.Metrics;
        var ca   = m.AfferentCoupling;
        var ce   = m.EfferentCoupling;
        var inst = m.Instability;

        var bg = inst >= 0.65 ? s_redBrush
               : inst >= 0.35 ? s_yellowBrush
               : inst > 0.0   ? s_greenBrush
               :                s_clearBrush;

        return new MetricsRow(
            Node:        node,
            Name:        node.Name,
            Kind:        KindLabel(node.Kind),
            Ca:          ca,
            Ce:          ce,
            I:           inst,
            MemberCount: node.Members.Count,
            RowBackground: bg);
    }

    private static string KindLabel(ClassKind kind) => kind switch
    {
        ClassKind.Interface => "interface",
        ClassKind.Enum      => "enum",
        ClassKind.Struct    => "struct",
        ClassKind.Abstract  => "abstract",
        _                   => "class",
    };

    // ------------------------------------------------------------------
    // INotifyPropertyChanged
    // ------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ---------------------------------------------------------------------------
// MetricsDashboardPanel — code-behind
// ---------------------------------------------------------------------------

/// <summary>
/// Dockable panel displaying per-class coupling metrics for the active diagram.
/// Raises <see cref="NodeFocusRequested"/> when the user selects a row.
/// </summary>
public partial class MetricsDashboardPanel : UserControl
{
    // ------------------------------------------------------------------
    // Public surface
    // ------------------------------------------------------------------

    /// <summary>Gets the ViewModel backing this panel.</summary>
    public MetricsDashboardViewModel ViewModel { get; }

    /// <summary>
    /// Raised when the user clicks a row. The argument is the corresponding
    /// <see cref="ClassNode"/>, or <see langword="null"/> when the selection
    /// is cleared.
    /// </summary>
    public event EventHandler<ClassNode?> NodeFocusRequested;

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public MetricsDashboardPanel()
    {
        ViewModel   = new MetricsDashboardViewModel();
        DataContext = ViewModel;
        InitializeComponent();

        // Satisfy non-nullable event field — default no-op handler.
        NodeFocusRequested = static (_, _) => { };
    }

    // ------------------------------------------------------------------
    // Event handlers
    // ------------------------------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = MetricsList.SelectedItem as MetricsRow;
        NodeFocusRequested.Invoke(this, row?.Node);
    }

    private void OnExportCsvClicked(object sender, RoutedEventArgs e) =>
        ViewModel.ExportCsv();
}

// ---------------------------------------------------------------------------
// ExportCsv extension — keeps code-behind thin
// ---------------------------------------------------------------------------

file static class MetricsDashboardExport
{
    // Exposed via extension so it can be tested independently.
    internal static void ExportCsv(this MetricsDashboardViewModel vm)
    {
        var dlg = new SaveFileDialog
        {
            Title            = "Export Metrics CSV",
            Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt       = "csv",
            FileName         = "ClassDiagramMetrics.csv",
            OverwritePrompt  = true,
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Name,Kind,Ca,Ce,I,Members");

        foreach (var row in vm.Rows)
        {
            sb.Append(EscapeCsv(row.Name)).Append(',');
            sb.Append(EscapeCsv(row.Kind)).Append(',');
            sb.Append(row.Ca).Append(',');
            sb.Append(row.Ce).Append(',');
            sb.Append(row.I.ToString("F4")).Append(',');
            sb.AppendLine(row.MemberCount.ToString());
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
            return value;

        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
