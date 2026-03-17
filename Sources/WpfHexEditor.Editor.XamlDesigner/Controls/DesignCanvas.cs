// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignCanvas.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Live WPF rendering surface for the XAML designer.
//     Parses XAML via XamlReader.Parse() and presents the result
//     in a ContentPresenter. Overlays a SelectionAdorner when the
//     user clicks an element.
//
// Architecture Notes:
//     Inherits Border. Contains a ContentPresenter + AdornerLayer.
//     XamlReader.Parse() runs on the UI thread inside a try/catch.
//     Missing xmlns attributes are auto-injected before parsing to
//     handle partial snippets gracefully.
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Design surface that renders live XAML inside a sandboxed WPF content host.
/// </summary>
public sealed class DesignCanvas : Border
{
    // ── Child controls ────────────────────────────────────────────────────────

    private readonly ContentPresenter _presenter;

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty XamlSourceProperty =
        DependencyProperty.Register(
            nameof(XamlSource),
            typeof(string),
            typeof(DesignCanvas),
            new FrameworkPropertyMetadata(string.Empty, OnXamlSourceChanged));

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignCanvas()
    {
        SetResourceReference(BackgroundProperty, "XD_CanvasBackground");
        SetResourceReference(BorderBrushProperty, "XD_CanvasBorderBrush");
        BorderThickness = new Thickness(1);

        _presenter = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(8)
        };

        Child = _presenter;

        // Element selection via click-through.
        PreviewMouseLeftButtonDown += OnCanvasMouseDown;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>XAML text to render. Triggers a re-render on change.</summary>
    public string XamlSource
    {
        get => (string)GetValue(XamlSourceProperty);
        set => SetValue(XamlSourceProperty, value);
    }

    /// <summary>The last successfully rendered root UIElement; null when empty or failed.</summary>
    public UIElement? DesignRoot { get; private set; }

    /// <summary>The currently selected element (highlighted with SelectionAdorner).</summary>
    public UIElement? SelectedElement { get; private set; }

    /// <summary>Fired after each render attempt. Null = success, non-null = error message.</summary>
    public event EventHandler<string?>? RenderError;

    /// <summary>Fired when the selected element changes.</summary>
    public event EventHandler? SelectedElementChanged;

    /// <summary>Programmatically selects an element and places the adorner.</summary>
    public void SelectElement(UIElement? el)
    {
        // Remove existing adorner.
        if (SelectedElement is not null)
        {
            var oldLayer = AdornerLayer.GetAdornerLayer(SelectedElement);
            if (oldLayer is not null)
            {
                var existing = oldLayer.GetAdorners(SelectedElement);
                if (existing is not null)
                    foreach (var a in existing.OfType<SelectionAdorner>())
                        oldLayer.Remove(a);
            }
        }

        SelectedElement = el;

        // Place new adorner.
        if (el is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            layer?.Add(new SelectionAdorner(el));
        }

        SelectedElementChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static void OnXamlSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DesignCanvas)d).RenderXaml((string)e.NewValue);

    private void RenderXaml(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            _presenter.Content = null;
            DesignRoot         = null;
            SelectElement(null);
            RenderError?.Invoke(this, null);
            return;
        }

        try
        {
            var prepared = EnsureWpfNamespaces(SanitizeForPreview(xaml));
            var result   = XamlReader.Parse(prepared);

            if (result is UIElement uiResult)
            {
                _presenter.Content = uiResult;
                DesignRoot         = uiResult;
                SelectElement(null);
                RenderError?.Invoke(this, null);
            }
            else if (result is FrameworkElement fe)
            {
                _presenter.Content = fe;
                DesignRoot         = fe;
                SelectElement(null);
                RenderError?.Invoke(this, null);
            }
            else
            {
                // Non-visual result (e.g. ResourceDictionary) — show type name.
                _presenter.Content = new TextBlock
                {
                    Text       = $"[{result?.GetType().Name ?? "null"} — non-visual root]",
                    Foreground = Brushes.Gray,
                    Margin     = new Thickness(4)
                };
                DesignRoot = null;
                RenderError?.Invoke(this, null);
            }
        }
        catch (Exception ex)
        {
            _presenter.Content = null;
            DesignRoot         = null;
            RenderError?.Invoke(this, ex.Message);
        }
    }

    // ── Mouse selection ───────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Walk up from the hit-tested element to find the first direct child
        // of the rendered root (or the root itself).
        if (DesignRoot is null) return;

        var hit = e.OriginalSource as DependencyObject;
        if (hit is null) return;

        // Find the topmost UIElement in the visual tree that is a child of _presenter.
        var target = FindSelectableElement(hit);
        SelectElement(target);
        e.Handled = false; // Let events propagate normally.
    }

    private UIElement? FindSelectableElement(DependencyObject? source)
    {
        if (source is null) return null;

        // Walk up the visual tree until we find a UIElement whose parent
        // is the ContentPresenter or the design root.
        var current = source as UIElement;
        while (current is not null)
        {
            var parent = VisualTreeHelper.GetParent(current) as UIElement;
            if (parent is null || ReferenceEquals(parent, _presenter) || ReferenceEquals(current, DesignRoot))
                return current;
            current = parent;
        }

        return DesignRoot;
    }

    // ── XAML preprocessing ────────────────────────────────────────────────────

    // Window-specific attributes that are meaningless (or forbidden) inside a ContentPresenter.
    private static readonly string[] WindowOnlyAttributes =
    [
        "Title", "Icon", "WindowStyle", "WindowStartupLocation", "WindowState",
        "ResizeMode", "ShowInTaskbar", "Topmost", "AllowsTransparency",
        "SizeToContent", "ShowActivated"
    ];

    /// <summary>
    /// Strips code-behind directives (x:Class, x:Subclass) and replaces a Window
    /// root element with a Border so the XAML can be hosted in a ContentPresenter
    /// without requiring the code-behind type to be present in the AppDomain.
    /// </summary>
    private static string SanitizeForPreview(string xaml)
    {
        // Remove x:Class, x:Subclass, x:FieldModifier — all require code-behind resolution.
        xaml = Regex.Replace(xaml, @"\s+x:(Class|Subclass|FieldModifier)=""[^""]*""", string.Empty);

        // Replace a Window root with Border (Window cannot be hosted in a ContentPresenter).
        xaml = ReplaceWindowRoot(xaml);

        return xaml;
    }

    /// <summary>
    /// Replaces &lt;Window …&gt;…&lt;/Window&gt; at the root with &lt;Border …&gt;…&lt;/Border&gt;,
    /// removing Window-only attributes that would cause parse errors on Border.
    /// </summary>
    private static string ReplaceWindowRoot(string xaml)
    {
        // Match the opening Window tag (self-closing or not).
        var openTag = Regex.Match(xaml, @"<Window(\s[^>]*)?>", RegexOptions.Singleline);
        if (!openTag.Success) return xaml;

        var attrs = openTag.Groups[1].Value;

        // Remove Window-only attributes.
        foreach (var attr in WindowOnlyAttributes)
            attrs = Regex.Replace(attrs, $@"\s+{attr}=""[^""]*""", string.Empty);

        // Rebuild the opening tag as Border.
        var newOpen = $"<Border{attrs}>";
        xaml = xaml[..openTag.Index] + newOpen + xaml[(openTag.Index + openTag.Length)..];

        // Replace closing tag (simple string replace is safe — XML is well-formed).
        xaml = xaml.Replace("</Window>", "</Border>");

        return xaml;
    }

    /// <summary>
    /// Pre-injects the standard WPF xmlns declarations if the XAML fragment
    /// does not already declare them. Prevents trivial "namespace not found"
    /// errors when the user is editing a partial snippet.
    /// </summary>
    private static string EnsureWpfNamespaces(string xaml)
    {
        const string wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        const string xNs   = "http://schemas.microsoft.com/winfx/2006/xaml";

        if (xaml.Contains(wpfNs) && xaml.Contains(xNs))
            return xaml;

        // Find the first tag opening so we can inject into it.
        int tagStart = xaml.IndexOf('<');
        if (tagStart < 0) return xaml;

        int insertPos = FindAttributeInsertPosition(xaml, tagStart);
        if (insertPos < 0) return xaml;

        string injection = string.Empty;

        if (!xaml.Contains(wpfNs))
            injection += $" xmlns=\"{wpfNs}\"";

        if (!xaml.Contains(xNs))
            injection += $" xmlns:x=\"{xNs}\"";

        return xaml.Insert(insertPos, injection);
    }

    private static int FindAttributeInsertPosition(string xaml, int tagStart)
    {
        // Skip the tag name to find the end of the element name.
        int i = tagStart + 1;
        while (i < xaml.Length && !char.IsWhiteSpace(xaml[i]) && xaml[i] != '>' && xaml[i] != '/')
            i++;
        return i < xaml.Length ? i : -1;
    }
}
