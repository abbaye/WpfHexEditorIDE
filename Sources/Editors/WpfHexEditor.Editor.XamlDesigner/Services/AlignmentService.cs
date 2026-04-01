// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AlignmentService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Provides 12 alignment and distribution operations for multiple selected
//     elements on the XAML design canvas. Returns a batch of DesignOperations
//     suitable for undo/redo support.
//
// Architecture Notes:
//     Pure service — stateless methods, no UI dependencies.
//     Each operation returns a list of DesignOperations (one per element)
//     so they can be pushed as a single batch onto the undo stack.
//     Position manipulation uses Margin (most general approach).
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Applies alignment and distribution to a set of selected elements.
/// </summary>
public sealed class AlignmentService
{
    // ── Alignment ─────────────────────────────────────────────────────────────

    public IReadOnlyList<AlignmentResult> AlignLeft(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double left = targets.Min(t => GetLeft(t.el));
        return ApplyX(targets, _ => left);
    }

    public IReadOnlyList<AlignmentResult> AlignRight(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double right = targets.Max(t => GetRight(t.el));
        return ApplyX(targets, t => right - t.ActualWidth);
    }

    public IReadOnlyList<AlignmentResult> AlignCenterH(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double cx = targets.Average(t => GetLeft(t.el) + t.el.ActualWidth / 2.0);
        return ApplyX(targets, t => cx - t.ActualWidth / 2.0);
    }

    public IReadOnlyList<AlignmentResult> AlignTop(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double top = targets.Min(t => GetTop(t.el));
        return ApplyY(targets, _ => top);
    }

    public IReadOnlyList<AlignmentResult> AlignBottom(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double bottom = targets.Max(t => GetBottom(t.el));
        return ApplyY(targets, t => bottom - t.ActualHeight);
    }

    public IReadOnlyList<AlignmentResult> AlignCenterV(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 2) return Array.Empty<AlignmentResult>();
        double cy = targets.Average(t => GetTop(t.el) + t.el.ActualHeight / 2.0);
        return ApplyY(targets, t => cy - t.ActualHeight / 2.0);
    }

    // ── Distribution ─────────────────────────────────────────────────────────

    public IReadOnlyList<AlignmentResult> DistributeH(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 3) return Array.Empty<AlignmentResult>();
        var sorted = targets.OrderBy(t => GetLeft(t.el)).ToList();
        double leftEdge  = GetLeft(sorted[0].el);
        double rightEdge = GetRight(sorted[^1].el);
        double totalWidth = sorted.Sum(t => t.el.ActualWidth);
        double gap = (rightEdge - leftEdge - totalWidth) / (sorted.Count - 1);

        var results = new List<AlignmentResult>();
        double x = leftEdge;
        foreach (var (el, uid) in sorted)
        {
            results.AddRange(ApplyX([(el, uid)], _ => x));
            x += el.ActualWidth + gap;
        }
        return results;
    }

    public IReadOnlyList<AlignmentResult> DistributeV(IReadOnlyList<(FrameworkElement el, int uid)> targets)
    {
        if (targets.Count < 3) return Array.Empty<AlignmentResult>();
        var sorted = targets.OrderBy(t => GetTop(t.el)).ToList();
        double topEdge    = GetTop(sorted[0].el);
        double bottomEdge = GetBottom(sorted[^1].el);
        double totalHeight = sorted.Sum(t => t.el.ActualHeight);
        double gap = (bottomEdge - topEdge - totalHeight) / (sorted.Count - 1);

        var results = new List<AlignmentResult>();
        double y = topEdge;
        foreach (var (el, uid) in sorted)
        {
            results.AddRange(ApplyY([(el, uid)], _ => y));
            y += el.ActualHeight + gap;
        }
        return results;
    }

    // ── Z-order ───────────────────────────────────────────────────────────────

    public static void BringToFront(FrameworkElement el)
    {
        if (el.Parent is System.Windows.Controls.Panel panel)
        {
            int idx = panel.Children.IndexOf(el);
            if (idx >= 0 && idx < panel.Children.Count - 1)
            {
                panel.Children.RemoveAt(idx);
                panel.Children.Add(el);
            }
        }
    }

    public static void SendToBack(FrameworkElement el)
    {
        if (el.Parent is System.Windows.Controls.Panel panel)
        {
            int idx = panel.Children.IndexOf(el);
            if (idx > 0)
            {
                panel.Children.RemoveAt(idx);
                panel.Children.Insert(0, el);
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static System.Windows.Controls.Canvas? Canvas(FrameworkElement el)
        => el.Parent as System.Windows.Controls.Canvas;

    private static double GetLeft(FrameworkElement el)
        => el.Parent is System.Windows.Controls.Canvas
            ? System.Windows.Controls.Canvas.GetLeft(el) is double d && !double.IsNaN(d) ? d : 0
            : el.Margin.Left;

    private static double GetTop(FrameworkElement el)
        => el.Parent is System.Windows.Controls.Canvas
            ? System.Windows.Controls.Canvas.GetTop(el) is double d && !double.IsNaN(d) ? d : 0
            : el.Margin.Top;

    private static double GetRight(FrameworkElement el) => GetLeft(el) + el.ActualWidth;
    private static double GetBottom(FrameworkElement el) => GetTop(el) + el.ActualHeight;

    private static IReadOnlyList<AlignmentResult> ApplyX(
        IReadOnlyList<(FrameworkElement el, int uid)> targets,
        Func<FrameworkElement, double> newXSelector)
    {
        var results = new List<AlignmentResult>();

        foreach (var (el, uid) in targets)
        {
            double before = GetLeft(el);
            double after  = newXSelector(el);
            if (Math.Abs(before - after) < 0.5) continue;

            var beforeAttr = new Dictionary<string, string?>();
            var afterAttr  = new Dictionary<string, string?>();

            if (el.Parent is System.Windows.Controls.Canvas)
            {
                beforeAttr["Canvas.Left"] = FormatD(before);
                afterAttr["Canvas.Left"]  = FormatD(after);
                System.Windows.Controls.Canvas.SetLeft(el, after);
            }
            else
            {
                beforeAttr["Margin"] = ThicknessStr(el.Margin);
                el.Margin = new Thickness(after, el.Margin.Top, el.Margin.Right, el.Margin.Bottom);
                afterAttr["Margin"]  = ThicknessStr(el.Margin);
            }

            results.Add(new AlignmentResult(
                uid,
                DesignOperation.CreateMove(uid, beforeAttr, afterAttr)));
        }

        return results;
    }

    private static IReadOnlyList<AlignmentResult> ApplyY(
        IReadOnlyList<(FrameworkElement el, int uid)> targets,
        Func<FrameworkElement, double> newYSelector)
    {
        var results = new List<AlignmentResult>();

        foreach (var (el, uid) in targets)
        {
            double before = GetTop(el);
            double after  = newYSelector(el);
            if (Math.Abs(before - after) < 0.5) continue;

            var beforeAttr = new Dictionary<string, string?>();
            var afterAttr  = new Dictionary<string, string?>();

            if (el.Parent is System.Windows.Controls.Canvas)
            {
                beforeAttr["Canvas.Top"] = FormatD(before);
                afterAttr["Canvas.Top"]  = FormatD(after);
                System.Windows.Controls.Canvas.SetTop(el, after);
            }
            else
            {
                beforeAttr["Margin"] = ThicknessStr(el.Margin);
                el.Margin = new Thickness(el.Margin.Left, after, el.Margin.Right, el.Margin.Bottom);
                afterAttr["Margin"]  = ThicknessStr(el.Margin);
            }

            results.Add(new AlignmentResult(
                uid,
                DesignOperation.CreateMove(uid, beforeAttr, afterAttr)));
        }

        return results;
    }

    private static string FormatD(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string ThicknessStr(Thickness t)
        => $"{FormatD(t.Left)},{FormatD(t.Top)},{FormatD(t.Right)},{FormatD(t.Bottom)}";
}

/// <summary>Result of an alignment operation for a single element.</summary>
public sealed record AlignmentResult(int Uid, DesignOperation Operation);
