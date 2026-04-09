// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/ClassBoxControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Custom FrameworkElement that renders a UML class box via
//     DrawingContext. Draws header band, stereotype, class name,
//     section dividers, and member rows with kind icons.
//
// Architecture Notes:
//     Pattern: Custom Rendering (OnRender override).
//     All colors via DynamicResource CD_* tokens with fallback defaults.
//     MeasureOverride computes size from member count.
//     Mouse events manage selection/hover state and emit events.
//     Context menu uses Segoe MDL2 Assets icons.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Custom-rendered WPF control for a single UML class box.
/// </summary>
public sealed class ClassBoxControl : FrameworkElement
{
    // ---------------------------------------------------------------------------
    // Layout constants
    // ---------------------------------------------------------------------------

    private const double HeaderBaseHeight = 44.0;
    private const double MemberHeight    = 20.0;
    private const double MemberPadding   = 4.0;
    private const double IconWidth       = 18.0;
    private const double HorizPadding    = 8.0;
    private const double CornerRadius    = 3.0;
    private const double BoxMinWidth     = 160.0;
    private const double AccentBarWidth  = 6.0;
    private const double TypeIconSize    = 16.0;
    private const double NsDashGap       = 6.0;

    // ---------------------------------------------------------------------------
    // Dependency Properties
    // ---------------------------------------------------------------------------

    public static readonly DependencyProperty NodeProperty =
        DependencyProperty.Register(nameof(Node), typeof(ClassNode), typeof(ClassBoxControl),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnNodeChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ClassBoxControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsHoveredProperty =
        DependencyProperty.Register(nameof(IsHovered), typeof(bool), typeof(ClassBoxControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler<ClassNode?>? SelectedClassChanged;
    public event EventHandler<ClassNode?>? HoveredClassChanged;
    public event EventHandler<ClassNode?>? DeleteRequested;
    public event EventHandler<ClassNode?>? PropertiesRequested;
    public event EventHandler<(ClassNode, MemberKind)>? AddMemberRequested;

    // ---------------------------------------------------------------------------
    // CLR wrappers
    // ---------------------------------------------------------------------------

    public ClassNode? Node
    {
        get => (ClassNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsHovered
    {
        get => (bool)GetValue(IsHoveredProperty);
        set => SetValue(IsHoveredProperty, value);
    }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public ClassBoxControl()
    {
        IsHitTestVisible = true;
        Cursor = Cursors.SizeAll;
    }

    // ---------------------------------------------------------------------------
    // Measure
    // ---------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Node is null) return new Size(BoxMinWidth, HeaderBaseHeight);

        double headerH   = ComputeHeaderHeight(Node);
        int memberCount  = Node.Members.Count;
        double boxHeight = headerH + memberCount * MemberHeight + MemberPadding * 2;
        double boxWidth  = ComputeBoxMinWidth();

        return new Size(Math.Max(BoxMinWidth, boxWidth), boxHeight);
    }

    private double ComputeBoxMinWidth()
    {
        if (Node is null) return BoxMinWidth;

        double maxLen = Node.Name.Length * 8.0;
        foreach (var m in Node.Members)
            maxLen = Math.Max(maxLen, m.DisplayLabel.Length * 7.5 + IconWidth + HorizPadding * 2);

        return Math.Max(BoxMinWidth, maxLen);
    }

    // ---------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        if (Node is null) return;

        double width  = RenderSize.Width;
        double height = RenderSize.Height;

        Brush boxBg     = GetBrush("CD_ClassBoxBackground",       Color.FromRgb(50, 50, 60));
        Brush boxBorder = GetBrush("CD_ClassBoxBorderBrush",      Color.FromRgb(80, 80, 100));
        Brush headerBg  = GetBrush("CD_ClassBoxHeaderBackground", Color.FromRgb(40, 40, 70));
        Brush nameColor = GetBrush("CD_ClassNameForeground",      Color.FromRgb(220, 220, 255));
        Brush sterColor = GetBrush("CD_StereotypeForeground",     Color.FromRgb(160, 160, 200));
        Brush memberFg  = GetBrush("CD_MemberTextForeground",     Color.FromRgb(200, 200, 210));
        Brush divBrush  = GetBrush("CD_ClassBoxSectionDivider",   Color.FromRgb(70, 70, 90));

        double borderThickness = IsSelected ? 2.0 : 1.0;
        Brush borderHighlight  = IsSelected
            ? GetBrush("CD_SelectionBorderBrush", Color.FromRgb(0, 120, 215))
            : boxBorder;

        double headerH = ComputeHeaderHeight(Node);
        var boxPen     = new Pen(borderHighlight, borderThickness);
        var boxRect    = new Rect(0, 0, width, height);
        var headerRect = new Rect(0, 0, width, headerH);

        // Background with rounded corners
        dc.DrawRoundedRectangle(boxBg, boxPen, boxRect, CornerRadius, CornerRadius);

        // Gradient header
        Color headerBase = headerBg is SolidColorBrush scb ? scb.Color : Color.FromRgb(37, 40, 64);
        byte lr = (byte)Math.Min(255, headerBase.R + 22);
        byte lg = (byte)Math.Min(255, headerBase.G + 22);
        byte lb = (byte)Math.Min(255, headerBase.B + 28);
        var gradHeader = new LinearGradientBrush(
            Color.FromRgb(lr, lg, lb), headerBase, new Point(0, 0), new Point(0, 1));
        dc.DrawRoundedRectangle(gradHeader, null, headerRect, CornerRadius, CornerRadius);
        dc.DrawRectangle(gradHeader, null, new Rect(0, headerH / 2, width, headerH / 2));

        // Left accent bar (6px, gradient fade)
        Color accentColor = GetAccentColor(Node);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();
        var accentGrad = new LinearGradientBrush(
            accentColor, Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B),
            new Point(0, 0), new Point(0, 1));
        var headerClip = new RectangleGeometry(headerRect, CornerRadius, CornerRadius);
        dc.PushClip(headerClip);
        dc.DrawRectangle(accentGrad, null, new Rect(0, 0, AccentBarWidth, headerH));
        dc.Pop();

        // ── Row 1: [icon] Name ───────────────────────────────────────
        var nameFt = MakeFormattedText(Node.Name, nameColor, 13.0, true);
        double nameRowH = Math.Max(TypeIconSize, nameFt.Height);
        double nameRowY = 6.0;

        // Type icon circle (inline left of name)
        double iconX = AccentBarWidth + 6.0;
        double iconCY = nameRowY + nameRowH / 2;
        var iconCircleBrush = new SolidColorBrush(Color.FromArgb(50, accentColor.R, accentColor.G, accentColor.B));
        iconCircleBrush.Freeze();
        dc.DrawEllipse(iconCircleBrush, new Pen(accentBrush, 1.2),
            new Point(iconX + TypeIconSize / 2, iconCY), TypeIconSize / 2, TypeIconSize / 2);
        var glyphFt = MakeFormattedText(GetTypeGlyph(Node), accentBrush, 9.5, true);
        dc.DrawText(glyphFt, new Point(iconX + (TypeIconSize - glyphFt.Width) / 2,
            iconCY - glyphFt.Height / 2));

        // Name (centered in area after accent bar)
        double nameAreaLeft = AccentBarWidth;
        double nameAreaW = width - nameAreaLeft;
        dc.DrawText(nameFt, new Point(nameAreaLeft + (nameAreaW - nameFt.Width) / 2,
            nameRowY + (nameRowH - nameFt.Height) / 2));

        double textY = nameRowY + nameRowH + 2.0;

        // ── Row 2: «stereotype · Attr1» (merged) ────────────────────
        string stereotype = GetStereotype(Node.Kind, Node.IsAbstract);
        string mergedLabel = BuildStereotypeLabel(Node, stereotype);
        if (mergedLabel.Length > 0)
        {
            var sterFt = MakeFormattedText(mergedLabel, sterColor, 9.0, false);
            sterFt.MaxTextWidth = width - AccentBarWidth - HorizPadding * 2;
            sterFt.Trimming     = TextTrimming.CharacterEllipsis;
            double sterW = Math.Min(sterFt.Width, sterFt.MaxTextWidth);
            dc.DrawText(sterFt, new Point(nameAreaLeft + (nameAreaW - sterW) / 2, textY));
            textY += sterFt.Height + 2.0;
        }

        // ── Row 3: ── Namespace ── (decorative dashes) ──────────────
        if (!string.IsNullOrEmpty(Node.Namespace))
        {
            var nsFt = MakeFormattedText(Node.Namespace, sterColor, 8.5, false);
            nsFt.MaxTextWidth = width - AccentBarWidth - HorizPadding * 2 - 40;
            nsFt.Trimming     = TextTrimming.CharacterEllipsis;
            double nsW  = Math.Min(nsFt.Width, nsFt.MaxTextWidth);
            double nsCX = nameAreaLeft + nameAreaW / 2;
            double nsX  = nsCX - nsW / 2;
            double nsY  = textY;
            double nsMid = nsY + nsFt.Height / 2;

            Brush nsDashBrush = GetBrush("CD_NamespacePillBackground", Color.FromArgb(80, 160, 160, 200));
            var nsDashPen = new Pen(nsDashBrush, 0.8);
            double dashL = AccentBarWidth + HorizPadding;
            double dashR = width - HorizPadding;
            if (nsX - NsDashGap > dashL + 8)
                dc.DrawLine(nsDashPen, new Point(dashL, nsMid), new Point(nsX - NsDashGap, nsMid));
            if (nsX + nsW + NsDashGap < dashR - 8)
                dc.DrawLine(nsDashPen, new Point(nsX + nsW + NsDashGap, nsMid), new Point(dashR, nsMid));

            dc.DrawText(nsFt, new Point(nsX, nsY));
        }

        // Gradient divider
        Color divColor = divBrush is SolidColorBrush dvScb ? dvScb.Color : Color.FromRgb(70, 70, 90);
        var divGrad = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        divGrad.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
        divGrad.GradientStops.Add(new GradientStop(divColor, 0.15));
        divGrad.GradientStops.Add(new GradientStop(divColor, 0.85));
        divGrad.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
        dc.DrawRectangle(divGrad, null, new Rect(0, headerH - 0.5, width, 1.0));

        // Member rows
        double memberY = headerH + MemberPadding;
        foreach (ClassMember member in Node.Members)
        {
            Brush iconBrush = GetMemberIconBrush(member.Kind);
            string iconChar = GetMemberIcon(member.Kind);
            string visPrefix = GetVisibilityPrefix(member.Visibility);

            var iconFt  = MakeFormattedText(iconChar, iconBrush, 11.0, false, "Segoe MDL2 Assets");
            var textFt  = MakeFormattedText($"{visPrefix}{member.DisplayLabel}", memberFg, 11.0, false);

            dc.DrawText(iconFt, new Point(HorizPadding, memberY + (MemberHeight - iconFt.Height) / 2));
            dc.DrawText(textFt, new Point(HorizPadding + IconWidth, memberY + (MemberHeight - textFt.Height) / 2));

            memberY += MemberHeight;
        }
    }

    // ---------------------------------------------------------------------------
    // Rendering helpers
    // ---------------------------------------------------------------------------

    private Brush GetBrush(string token, Color fallback) =>
        TryFindResource(token) as Brush ?? new SolidColorBrush(fallback);

    private FormattedText MakeFormattedText(string text, Brush brush, double size, bool bold,
        string? fontFamily = null)
    {
        var typeface = new Typeface(
            new FontFamily(fontFamily ?? "Segoe UI"),
            FontStyles.Normal,
            bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);

        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static string BuildStereotypeLabel(ClassNode node, string stereotype)
    {
        bool hasSter = stereotype.Length > 0;
        bool hasAttr = node.Attributes.Count > 0;
        if (!hasSter && !hasAttr) return string.Empty;
        if (hasSter && !hasAttr) return stereotype;
        string attrs = string.Join(", ", node.Attributes);
        if (!hasSter) return "«" + attrs + "»";
        string sterCore = stereotype[1..^1];
        return "«" + sterCore + " · " + attrs + "»";
    }

    private double ComputeHeaderHeight(ClassNode node)
    {
        // Row 1: icon + name
        var nameFt = MakeFormattedText(node.Name, Brushes.White, 13.0, true);
        double y = 6.0 + Math.Max(TypeIconSize, nameFt.Height) + 2.0;

        // Row 2: merged stereotype
        string stereotype = GetStereotype(node.Kind, node.IsAbstract);
        string merged = BuildStereotypeLabel(node, stereotype);
        if (merged.Length > 0)
        {
            var sterFt = MakeFormattedText(merged, Brushes.White, 9.0, false);
            y += sterFt.Height + 2.0;
        }

        // Row 3: namespace
        if (!string.IsNullOrEmpty(node.Namespace))
        {
            var nsFt = MakeFormattedText(node.Namespace, Brushes.White, 8.5, false);
            y += nsFt.Height + 2.0;
        }

        return Math.Max(HeaderBaseHeight, y + 4.0);
    }

    private static Color GetAccentColor(ClassNode node)
    {
        if (node.CustomColor.HasValue)
        {
            var c = node.CustomColor.Value;
            return Color.FromRgb(c.R, c.G, c.B);
        }
        if (node.IsRecord)  return Color.FromRgb(156, 220, 254);
        return node.Kind switch
        {
            ClassKind.Interface => Color.FromRgb( 78, 201, 176),
            ClassKind.Enum      => Color.FromRgb(197, 134, 192),
            ClassKind.Struct    => Color.FromRgb(220, 220, 170),
            ClassKind.Abstract  => Color.FromRgb( 86, 156, 214),
            _ => node.IsAbstract
                ? Color.FromRgb(86, 156, 214)
                : Color.FromRgb(79, 193, 255)
        };
    }

    private static string GetTypeGlyph(ClassNode node)
    {
        if (node.IsRecord)  return "R";
        return node.Kind switch
        {
            ClassKind.Interface => "I",
            ClassKind.Enum      => "E",
            ClassKind.Struct    => "S",
            ClassKind.Abstract  => "A",
            _ => node.IsAbstract ? "A" : "C"
        };
    }

    private static string GetStereotype(ClassKind kind, bool isAbstract) => kind switch
    {
        ClassKind.Interface => "«interface»",
        ClassKind.Enum      => "«enum»",
        ClassKind.Struct    => "«struct»",
        ClassKind.Abstract  => "«abstract»",
        ClassKind.Class when isAbstract => "«abstract»",
        _                   => string.Empty
    };

    private Brush GetMemberIconBrush(MemberKind kind) => kind switch
    {
        MemberKind.Field    => GetBrush("CD_FieldForeground",    Color.FromRgb( 86, 156, 214)),
        MemberKind.Property => GetBrush("CD_PropertyForeground", Color.FromRgb( 78, 201, 176)),
        MemberKind.Method   => GetBrush("CD_MethodForeground",   Color.FromRgb(197, 134, 192)),
        MemberKind.Event    => GetBrush("CD_EventForeground",    Color.FromRgb(220, 160,  80)),
        _                   => GetBrush("CD_MemberTextForeground", Color.FromRgb(200, 200, 210))
    };

    private static string GetMemberIcon(MemberKind kind) => kind switch
    {
        MemberKind.Field    => "\uE192",
        MemberKind.Property => "\uE10C",
        MemberKind.Method   => "\uE8F4",
        MemberKind.Event    => "\uECAD",
        _                   => "\uE192"
    };

    private static string GetVisibilityPrefix(MemberVisibility vis) => vis switch
    {
        MemberVisibility.Public    => "+ ",
        MemberVisibility.Private   => "- ",
        MemberVisibility.Protected => "# ",
        MemberVisibility.Internal  => "~ ",
        _                          => "  "
    };

    // ---------------------------------------------------------------------------
    // Mouse handling
    // ---------------------------------------------------------------------------

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        IsHovered = true;
        HoveredClassChanged?.Invoke(this, Node);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsHovered = false;
        HoveredClassChanged?.Invoke(this, null);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (Node is null) return;
        BuildContextMenu().IsOpen = true;
        e.Handled = true;
    }

    // ---------------------------------------------------------------------------
    // Context menu
    // ---------------------------------------------------------------------------

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "DockMenuForegroundBrush");

        menu.Items.Add(MakeItem("\uE70F", "Rename",    () => { }));
        menu.Items.Add(MakeItem("\uE74D", "Delete",    () => DeleteRequested?.Invoke(this, Node)));
        menu.Items.Add(MakeItem("\uE8C8", "Duplicate", () => { }));
        menu.Items.Add(new Separator());

        // Add member submenu
        var addMenu = new MenuItem { Header = "Add Member" };
        addMenu.Items.Add(MakeItem("\uE192", "Field",    () => AddMemberRequested?.Invoke(this, (Node!, MemberKind.Field))));
        addMenu.Items.Add(MakeItem("\uE10C", "Property", () => AddMemberRequested?.Invoke(this, (Node!, MemberKind.Property))));
        addMenu.Items.Add(MakeItem("\uE8F4", "Method",   () => AddMemberRequested?.Invoke(this, (Node!, MemberKind.Method))));
        addMenu.Items.Add(MakeItem("\uECAD", "Event",    () => AddMemberRequested?.Invoke(this, (Node!, MemberKind.Event))));
        menu.Items.Add(addMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8D4", "Properties", () => PropertiesRequested?.Invoke(this, Node)));

        return menu;
    }

    private static MenuItem MakeItem(string icon, string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = new TextBlock
            {
                Text       = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 14
            }
        };
        item.Click += (_, _) => action();
        return item;
    }

    // ---------------------------------------------------------------------------
    // DP callback
    // ---------------------------------------------------------------------------

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClassBoxControl box)
        {
            box.InvalidateMeasure();
            box.InvalidateVisual();
        }
    }
}
