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

    private const double HeaderHeight   = 44.0;
    private const double MemberHeight   = 20.0;
    private const double MemberPadding  = 4.0;
    private const double IconWidth      = 18.0;
    private const double HorizPadding   = 8.0;
    private const double CornerRadius   = 3.0;
    private const double BoxMinWidth    = 160.0;

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
        if (Node is null) return new Size(BoxMinWidth, HeaderHeight);

        int memberCount  = Node.Members.Count;
        double boxHeight = HeaderHeight + memberCount * MemberHeight + MemberPadding * 2;
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

        var boxPen    = new Pen(borderHighlight, borderThickness);
        var divPen    = new Pen(divBrush, 0.5);
        var boxRect   = new Rect(0, 0, width, height);
        var headerRect = new Rect(0, 0, width, HeaderHeight);

        // Background with rounded corners
        dc.DrawRoundedRectangle(boxBg, boxPen, boxRect, CornerRadius, CornerRadius);

        // Header background
        dc.DrawRoundedRectangle(headerBg, null, headerRect, CornerRadius, CornerRadius);
        // Cover bottom corners of header so only top corners are rounded
        dc.DrawRectangle(headerBg, null, new Rect(0, HeaderHeight / 2, width, HeaderHeight / 2));

        // Stereotype label
        string stereotype = GetStereotype(Node.Kind, Node.IsAbstract);
        double textY = 6.0;
        if (!string.IsNullOrEmpty(stereotype))
        {
            var sterFt = MakeFormattedText(stereotype, sterColor, 10.0, false);
            dc.DrawText(sterFt, new Point((width - sterFt.Width) / 2, textY));
            textY += sterFt.Height + 1.0;
        }

        // Class name
        var nameFt = MakeFormattedText(Node.Name, nameColor, 13.0, true);
        dc.DrawText(nameFt, new Point((width - nameFt.Width) / 2, textY));

        // Section divider below header
        dc.DrawLine(divPen, new Point(0, HeaderHeight), new Point(width, HeaderHeight));

        // Member rows
        double memberY = HeaderHeight + MemberPadding;
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
