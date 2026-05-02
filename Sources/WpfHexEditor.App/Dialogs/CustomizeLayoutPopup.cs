// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/CustomizeLayoutPopup.cs
// Description:
//     VS Code-style "Customize Layout" floating popup.
//     Allows real-time toggling of UI element visibility,
//     changing layout positions via radio pills, and activating
//     layout modes (Zen, Focused, Presentation, Full Screen).
//     Code-behind only — no XAML.
//
// Architecture Notes:
//     Non-modal Window (Show, not ShowDialog). Closes on Deactivated.
//     Uses CL_* resource tokens from the active theme, falls back to CP_*.
//     Changes apply immediately via callbacks — no OK/Cancel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Floating layout customization popup. Opened non-modally; closes on deactivated or Esc.
/// </summary>
public sealed class CustomizeLayoutPopup : Window
{
    // ── Callbacks ──────────────────────────────────────────────────────────

    private readonly Action<string, bool>   _onToggle;         // (elementId, isVisible)
    private readonly Action<string, string> _onRadioChange;    // (groupId, selectedValue)
    private readonly Action<string>         _onModeToggle;     // (modeId)
    private readonly Action                 _onSettingsChanged;

    // ── State ──────────────────────────────────────────────────────────────

    private readonly LayoutSettings _settings;
    private bool _closingStarted;

    // Eye icon glyphs (Segoe MDL2 Assets)
    private const string EyeOpen   = "\uE7B3";
    private const string EyeClosed = "\uE7B4";

    // Track toggle text blocks for eye icon updates
    private readonly Dictionary<string, TextBlock> _eyeIcons = new();
    // Track toggle row borders for visual state
    private readonly Dictionary<string, Border> _toggleBorders = new();
    // Track radio pill buttons per group
    private readonly Dictionary<string, List<(ToggleButton Btn, string Value)>> _radioGroups = new();
    // Track mode dot indicators
    private readonly Dictionary<string, Border> _modeDots = new();

    // ── Panel state tracking ──────────────────────────────────────────────

    private readonly Dictionary<string, bool> _panelVisibility = new();

    // ── Debounce save ─────────────────────────────────────────────────────
    private readonly DispatcherTimer _saveDebounce;

    // ── Radio event guard ─────────────────────────────────────────────────
    private bool _suppressRadioEvents;

    // ── Cached dark foreground for selected pills ─────────────────────────
    private static readonly SolidColorBrush SelectedPillForeground =
        new(Color.FromRgb(0x1E, 0x1E, 0x1E));

    // ── Cached pill template ──────────────────────────────────────────────
    private static ControlTemplate? _pillTemplate;

    public CustomizeLayoutPopup(
        LayoutSettings settings,
        Window owner,
        Point? anchor,
        IReadOnlyList<(string Id, string Title, bool IsVisible)> panelStates,
        Func<string, string?>? resolveGesture,
        Action<string, bool> onToggle,
        Action<string, string> onRadioChange,
        Action<string> onModeToggle,
        Action onSettingsChanged,
        double? overrideWidth = null)
    {
        _settings          = settings;
        _onToggle          = onToggle;
        _onRadioChange     = onRadioChange;
        _onModeToggle      = onModeToggle;
        _onSettingsChanged = onSettingsChanged;

        foreach (var (id, _, isVis) in panelStates)
            _panelVisibility[id] = isVis;

        // Debounce settings save (300ms) — avoids expensive JSON serialize on every click
        _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); _onSettingsChanged(); };

        // ─── Window chrome ────────────────────────────────────────────────
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ResizeMode         = ResizeMode.NoResize;
        ShowInTaskbar      = false;
        Topmost            = true;
        Width              = overrideWidth ?? 460;
        SizeToContent      = SizeToContent.Height;
        MaxHeight          = 600;

        // ─── Root border ──────────────────────────────────────────────────
        var root = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Direction   = 315,
                ShadowDepth = 6,
                BlurRadius  = 18,
                Opacity     = 0.55,
                Color       = Colors.Black
            }
        };
        root.SetResourceReference(Border.BackgroundProperty,  "CP_BackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "CP_BorderBrush");
        Content = root;

        // ─── Scrollable content ───────────────────────────────────────────
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0)
        };
        root.Child = scroll;

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        scroll.Content = stack;

        // ─── Header ──────────────────────────────────────────────────────
        var header = BuildHeader();
        stack.Children.Add(header);

        // ─── VISIBILITY section ──────────────────────────────────────────
        stack.Children.Add(BuildSectionHeader("VISIBILITY"));

        // Core UI elements
        stack.Children.Add(BuildToggleRow("menubar",   "Menu Bar",   settings.ShowMenuBar,   resolveGesture?.Invoke("Layout.ToggleMenuBar")));
        stack.Children.Add(BuildToggleRow("toolbar",   "Toolbar",    settings.ShowToolbar,   resolveGesture?.Invoke("Layout.ToggleToolbar")));
        stack.Children.Add(BuildToggleRow("statusbar", "Status Bar", settings.ShowStatusBar, resolveGesture?.Invoke("Layout.ToggleStatusBar")));

        // Separator before panels
        var sep = new Border { Height = 1, Margin = new Thickness(16, 4, 16, 4) };
        sep.SetResourceReference(Border.BackgroundProperty, "CP_BorderBrush");
        stack.Children.Add(sep);

        // Dynamic panels
        foreach (var (id, title, isVisible) in panelStates)
        {
            var gesture = resolveGesture?.Invoke($"View.{id}");
            stack.Children.Add(BuildToggleRow(id, title, isVisible, gesture));
        }

        // ─── POSITION section ────────────────────────────────────────────
        stack.Children.Add(BuildSectionHeader("POSITION"));

        stack.Children.Add(BuildRadioRow("toolbar-position", "Toolbar Position",
            new[] { ("Top", "Top"), ("Bottom", "Bottom") },
            settings.ToolbarPosition));

        stack.Children.Add(BuildRadioRow("panel-dock-side", "Panel Default Side",
            new[] { ("Left", "Left"), ("Right", "Right"), ("Bottom", "Bottom") },
            settings.DefaultPanelDockSide));

        stack.Children.Add(BuildRadioRow("tab-position", "Tab Bar Position",
            new[] { ("Top", "Top"), ("Bottom", "Bottom") },
            settings.TabBarPosition));

        // ─── LAYOUT MODES section ────────────────────────────────────────
        stack.Children.Add(BuildSectionHeader("LAYOUT MODES"));

        stack.Children.Add(BuildModeRow("fullscreen",    "Full Screen",        false,                     resolveGesture?.Invoke("Layout.FullScreen")       ?? "F11",       "\uE740"));
        stack.Children.Add(BuildModeRow("zen",           "Zen Mode",           settings.IsZenMode,        resolveGesture?.Invoke("Layout.ZenMode")          ?? "Ctrl+K Z",  "\uE78B"));
        stack.Children.Add(BuildModeRow("focused",       "Focused Mode",       settings.IsFocusedMode,    resolveGesture?.Invoke("Layout.FocusedMode")      ?? "Ctrl+K F",  "\uE71D"));
        stack.Children.Add(BuildModeRow("presentation",  "Presentation Mode",  settings.IsPresentationMode, resolveGesture?.Invoke("Layout.PresentationMode") ?? "Ctrl+K P", "\uE8A3"));

        // ─── Footer ─────────────────────────────────────────────────────
        var footer = new TextBlock
        {
            Text              = "Changes apply immediately",
            FontSize          = 11,
            FontStyle         = FontStyles.Italic,
            Margin            = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footer.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        stack.Children.Add(footer);

        // ─── Positioning (center horizontally under anchor, like CommandPalette) ──
        if (anchor.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = anchor.Value.X - Width / 2;
            Top  = anchor.Value.Y;
        }
        else
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        // ─── Events ────────────────────────────────────────────────────
        Deactivated += (_, _) => SafeClose();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UI builders
    // ═══════════════════════════════════════════════════════════════════════

    private Border BuildHeader()
    {
        var dock = new DockPanel { Margin = new Thickness(16, 12, 12, 4) };

        // Reset button (right)
        var resetBtn = new Button
        {
            Content           = "\uE10E",   // MDL2 "Undo" glyph
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 14,
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Cursor            = Cursors.Hand,
            ToolTip           = AppResources.App_Options_ResetToDefaults,
            Padding           = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Template          = BuildIconButtonTemplate()
        };
        resetBtn.SetResourceReference(ForegroundProperty, "CP_SecondaryTextBrush");
        resetBtn.MouseEnter += (_, _) => resetBtn.SetResourceReference(BackgroundProperty, "CP_HoverBrush");
        resetBtn.MouseLeave += (_, _) => resetBtn.Background = Brushes.Transparent;
        resetBtn.Click += OnResetDefaults;
        DockPanel.SetDock(resetBtn, Dock.Right);
        dock.Children.Add(resetBtn);

        // Title
        var title = new TextBlock
        {
            Text              = "Customize Layout",
            FontSize          = 14,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        dock.Children.Add(title);

        var border = new Border { Child = dock };
        return border;
    }

    private Border BuildSectionHeader(string text)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(16, 10, 16, 4)
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        return new Border { Child = tb };
    }

    private Border BuildToggleRow(string elementId, string label, bool isVisible, string? gestureText)
    {
        var rowBorder = new Border
        {
            Padding = new Thickness(16, 5, 16, 5),
            Cursor  = Cursors.Hand
        };
        rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_BackgroundBrush");
        _toggleBorders[elementId] = rowBorder;

        var dock = new DockPanel { LastChildFill = true };

        // Gesture text (right)
        if (!string.IsNullOrEmpty(gestureText))
        {
            var gestureTb = new TextBlock
            {
                Text              = gestureText,
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(8, 0, 0, 0)
            };
            gestureTb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
            DockPanel.SetDock(gestureTb, Dock.Right);
            dock.Children.Add(gestureTb);
        }

        // Visibility eye icon (right)
        var eyeIcon = new TextBlock
        {
            Text              = isVisible ? EyeOpen : EyeClosed,
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0)
        };
        eyeIcon.SetResourceReference(TextBlock.ForegroundProperty,
            isVisible ? "CL_ToggleActiveBrush" : "CL_ToggleInactiveBrush");
        _eyeIcons[elementId] = eyeIcon;
        DockPanel.SetDock(eyeIcon, Dock.Right);
        dock.Children.Add(eyeIcon);

        // Label
        var labelTb = new TextBlock
        {
            Text              = label,
            FontSize          = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelTb.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        dock.Children.Add(labelTb);

        rowBorder.Child = dock;

        // Hover
        rowBorder.MouseEnter += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_HoverBrush");
        rowBorder.MouseLeave += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_BackgroundBrush");

        // Click → toggle
        rowBorder.MouseLeftButtonDown += (_, _) =>
        {
            var newState = !GetToggleState(elementId);
            SetToggleState(elementId, newState);
            UpdateEyeIcon(elementId, newState);
            _onToggle(elementId, newState);
            DebounceSave();
        };

        return rowBorder;
    }

    private Border BuildRadioRow(string groupId, string label, (string Value, string Label)[] options, string selectedValue)
    {
        var rowBorder = new Border
        {
            Padding = new Thickness(16, 5, 16, 5)
        };

        var dock = new DockPanel { LastChildFill = true };

        // Radio pills (right)
        var pillStack = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var groupButtons = new List<(ToggleButton Btn, string Value)>();

        foreach (var (value, optLabel) in options)
        {
            var pill = new ToggleButton
            {
                Content   = optLabel,
                IsChecked = value == selectedValue,
                FontSize  = 11,
                Padding   = new Thickness(8, 3, 8, 3),
                Margin    = new Thickness(2, 0, 2, 0),
                Cursor    = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Tag       = value
            };

            // Style the pill
            ApplyRadioPillStyle(pill, value == selectedValue);

            // Hover — highlight background + ensure readable foreground
            pill.MouseEnter += (_, _) =>
            {
                pill.SetResourceReference(BackgroundProperty, "CP_HighlightBrush");
                pill.SetResourceReference(ForegroundProperty, "CP_TextBrush");
            };
            pill.MouseLeave += (_, _) =>
                ApplyRadioPillStyle(pill, pill.IsChecked == true);

            var capturedValue = value;
            pill.Checked += (_, _) =>
            {
                if (_suppressRadioEvents) return;
                _suppressRadioEvents = true;

                // Uncheck others in group
                foreach (var (btn, _) in groupButtons)
                {
                    if (btn != pill)
                    {
                        btn.IsChecked = false;
                        ApplyRadioPillStyle(btn, false);
                    }
                }
                ApplyRadioPillStyle(pill, true);

                _suppressRadioEvents = false;

                _onRadioChange(groupId, capturedValue);
                DebounceSave();
            };

            pill.Unchecked += (_, _) =>
            {
                if (_suppressRadioEvents) return;

                // Prevent unchecking the only checked pill
                var anyChecked = groupButtons.Any(b => b.Btn.IsChecked == true);
                if (!anyChecked)
                    pill.IsChecked = true;
            };

            groupButtons.Add((pill, value));
            pillStack.Children.Add(pill);
        }

        _radioGroups[groupId] = groupButtons;

        DockPanel.SetDock(pillStack, Dock.Right);
        dock.Children.Add(pillStack);

        // Label
        var labelTb = new TextBlock
        {
            Text              = label,
            FontSize          = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelTb.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        dock.Children.Add(labelTb);

        rowBorder.Child = dock;

        // Hover
        rowBorder.MouseEnter += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_HoverBrush");
        rowBorder.MouseLeave += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_BackgroundBrush");

        return rowBorder;
    }

    private Border BuildModeRow(string modeId, string label, bool isActive, string? gestureText, string iconGlyph)
    {
        var rowBorder = new Border
        {
            Padding = new Thickness(16, 5, 16, 5),
            Cursor  = Cursors.Hand
        };
        rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_BackgroundBrush");

        var dock = new DockPanel { LastChildFill = true };

        // Gesture text (right)
        if (!string.IsNullOrEmpty(gestureText))
        {
            var gestureTb = new TextBlock
            {
                Text              = gestureText,
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(8, 0, 0, 0)
            };
            gestureTb.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
            DockPanel.SetDock(gestureTb, Dock.Right);
            dock.Children.Add(gestureTb);
        }

        // Active dot indicator (right)
        var dot = new Border
        {
            Width         = 8,
            Height        = 8,
            CornerRadius  = new CornerRadius(4),
            Margin        = new Thickness(8, 0, 0, 0),
            Visibility    = isActive ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
        dot.SetResourceReference(Border.BackgroundProperty, "CL_ModeBadgeBrush");
        _modeDots[modeId] = dot;
        DockPanel.SetDock(dot, Dock.Right);
        dock.Children.Add(dot);

        // Icon (left)
        var icon = new TextBlock
        {
            Text              = iconGlyph,
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0)
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "CP_SecondaryTextBrush");
        DockPanel.SetDock(icon, Dock.Left);
        dock.Children.Add(icon);

        // Label
        var labelTb = new TextBlock
        {
            Text              = label,
            FontSize          = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelTb.SetResourceReference(TextBlock.ForegroundProperty, "CP_TextBrush");
        dock.Children.Add(labelTb);

        rowBorder.Child = dock;

        // Hover
        rowBorder.MouseEnter += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_HoverBrush");
        rowBorder.MouseLeave += (_, _) =>
            rowBorder.SetResourceReference(Border.BackgroundProperty, "CP_BackgroundBrush");

        // Click → toggle mode
        rowBorder.MouseLeftButtonDown += (_, _) =>
        {
            var newActive = !(_modeDots[modeId].Visibility == Visibility.Visible);
            _modeDots[modeId].Visibility = newActive ? Visibility.Visible : Visibility.Collapsed;
            _onModeToggle(modeId);
            DebounceSave();
        };

        return rowBorder;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private void DebounceSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void ApplyRadioPillStyle(ToggleButton pill, bool isSelected)
    {
        // Ensure custom template is applied (default ToggleButton template ignores Background)
        pill.Template ??= BuildPillTemplate();

        pill.SetResourceReference(BackgroundProperty,
            isSelected ? "CL_RadioSelectedBrush" : "CL_RadioUnselectedBrush");

        // Foreground from theme tokens — each theme controls contrast
        pill.SetResourceReference(ForegroundProperty,
            isSelected ? "CL_RadioSelectedForegroundBrush" : "CP_TextBrush");

        // Border on unselected pills for visibility against popup bg
        if (isSelected)
        {
            pill.BorderThickness = new Thickness(0);
        }
        else
        {
            pill.BorderThickness = new Thickness(1);
            pill.SetResourceReference(BorderBrushProperty, "CP_BorderBrush");
        }
    }

    /// <summary>Minimal button template that respects Background and has no default chrome.</summary>
    private static ControlTemplate BuildIconButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.PaddingProperty, new Binding("Padding")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;
        return template;
    }

    private static ControlTemplate BuildPillTemplate()
    {
        if (_pillTemplate is not null) return _pillTemplate;

        var template = new ControlTemplate(typeof(ToggleButton));
        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.PaddingProperty, new Binding("Padding")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
            { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;
        _pillTemplate = template;
        return template;
    }

    private bool GetToggleState(string elementId) => elementId switch
    {
        "menubar"   => _settings.ShowMenuBar,
        "toolbar"   => _settings.ShowToolbar,
        "statusbar" => _settings.ShowStatusBar,
        _           => _panelVisibility.TryGetValue(elementId, out var v) && v
    };

    private void SetToggleState(string elementId, bool value)
    {
        switch (elementId)
        {
            case "menubar":   _settings.ShowMenuBar   = value; break;
            case "toolbar":   _settings.ShowToolbar   = value; break;
            case "statusbar": _settings.ShowStatusBar = value; break;
            default:          _panelVisibility[elementId] = value; break;
        }
    }

    private void UpdateEyeIcon(string elementId, bool isVisible)
    {
        if (!_eyeIcons.TryGetValue(elementId, out var icon)) return;
        icon.Text = isVisible ? EyeOpen : EyeClosed;
        icon.SetResourceReference(TextBlock.ForegroundProperty,
            isVisible ? "CL_ToggleActiveBrush" : "CL_ToggleInactiveBrush");
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        // Reset visibility
        _settings.ShowMenuBar   = true;
        _settings.ShowToolbar   = true;
        _settings.ShowStatusBar = true;
        UpdateEyeIcon("menubar",   true); _onToggle("menubar",   true);
        UpdateEyeIcon("toolbar",   true); _onToggle("toolbar",   true);
        UpdateEyeIcon("statusbar", true); _onToggle("statusbar", true);

        // Reset positions
        _settings.ToolbarPosition      = "Top";
        _settings.DefaultPanelDockSide = "Right";
        _settings.TabBarPosition       = "Top";
        SetRadioGroup("toolbar-position", "Top");     _onRadioChange("toolbar-position", "Top");
        SetRadioGroup("panel-dock-side",  "Right");   _onRadioChange("panel-dock-side",  "Right");
        SetRadioGroup("tab-position",     "Top");     _onRadioChange("tab-position",     "Top");

        _onSettingsChanged();
    }

    private void SetRadioGroup(string groupId, string value)
    {
        if (!_radioGroups.TryGetValue(groupId, out var buttons)) return;
        foreach (var (btn, val) in buttons)
        {
            btn.IsChecked = val == value;
            ApplyRadioPillStyle(btn, val == value);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SafeClose();
            e.Handled = true;
        }
    }

    private void SafeClose()
    {
        if (_closingStarted) return;
        _closingStarted = true;
        Close();
    }
}
