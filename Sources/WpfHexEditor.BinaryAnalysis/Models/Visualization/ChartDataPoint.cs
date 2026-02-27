//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.BinaryAnalysis.Models.Visualization
{
    /// <summary>
    /// Represents a single data point for charts
    /// </summary>
    public class ChartDataPoint
    {
        /// <summary>
        /// X-axis value (offset, byte value, etc.)
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y-axis value (frequency, entropy, etc.)
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Optional label for this point
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Optional color hint for rendering
        /// </summary>
        public string Color { get; set; }

        public ChartDataPoint()
        {
        }

        public ChartDataPoint(double x, double y, string label = null)
        {
            X = x;
            Y = y;
            Label = label;
        }

        public override string ToString()
        {
            return $"({X}, {Y})" + (string.IsNullOrEmpty(Label) ? "" : $" - {Label}");
        }
    }

    /// <summary>
    /// Collection of chart data points with metadata
    /// </summary>
    public class ChartData
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// X-axis label
        /// </summary>
        public string XAxisLabel { get; set; }

        /// <summary>
        /// Y-axis label
        /// </summary>
        public string YAxisLabel { get; set; }

        /// <summary>
        /// Data points
        /// </summary>
        public List<ChartDataPoint> DataPoints { get; set; } = new List<ChartDataPoint>();

        /// <summary>
        /// Chart type hint
        /// </summary>
        public ChartType ChartType { get; set; } = ChartType.Line;

        /// <summary>
        /// Minimum X value
        /// </summary>
        public double MinX => DataPoints.Count > 0 ? DataPoints.Min(p => p.X) : 0;

        /// <summary>
        /// Maximum X value
        /// </summary>
        public double MaxX => DataPoints.Count > 0 ? DataPoints.Max(p => p.X) : 0;

        /// <summary>
        /// Minimum Y value
        /// </summary>
        public double MinY => DataPoints.Count > 0 ? DataPoints.Min(p => p.Y) : 0;

        /// <summary>
        /// Maximum Y value
        /// </summary>
        public double MaxY => DataPoints.Count > 0 ? DataPoints.Max(p => p.Y) : 0;

        public ChartData()
        {
        }

        public ChartData(string title)
        {
            Title = title;
        }

        /// <summary>
        /// Add data point
        /// </summary>
        public void AddPoint(double x, double y, string label = null)
        {
            DataPoints.Add(new ChartDataPoint(x, y, label));
        }
    }

    /// <summary>
    /// Chart type enumeration
    /// </summary>
    public enum ChartType
    {
        /// <summary>
        /// Line chart
        /// </summary>
        Line,

        /// <summary>
        /// Bar chart
        /// </summary>
        Bar,

        /// <summary>
        /// Area chart
        /// </summary>
        Area,

        /// <summary>
        /// Scatter plot
        /// </summary>
        Scatter,

        /// <summary>
        /// Heat map
        /// </summary>
        HeatMap
    }
}
