// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/DataTipPopup.cs
// Description:
//     VS-style Data Tip popup shown when hovering a token during a paused debug session.
//     Displays "token = value" with syntax-colored value text.
// Architecture:
//     Standalone WPF Popup — no dependency on IDebuggerService or SDK.
//     App layer evaluates via IDebugHoverProvider and calls Show() with the result string.
//     Grace-timer pattern: popup stays open 300ms after mouse leaves so user can read the value.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

internal sealed class DataTipPopup : Popup, IDisposable
{
    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInside;

    private readonly Border    _border;
    private readonly TextBlock _tokenText;
    private readonly TextBlock _valueText;

    public bool IsMouseOverPopup => _mouseInside;

    public DataTipPopup()
    {
        AllowsTransparency = true;
        Placement          = PlacementMode.AbsolutePoint;
        StaysOpen          = true;
        IsHitTestVisible   = true;

        _graceTimer          = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _graceTimer.Tick    += (_, _) => { _graceTimer.Stop(); if (!_mouseInside) IsOpen = false; };

        _tokenText = new TextBlock
        {
            FontWeight  = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _tokenText.SetResourceReference(TextBlock.ForegroundProperty, "EditorForeground");

        var eq = new TextBlock
        {
            Text = " = ",
            VerticalAlignment = VerticalAlignment.Center,
        };
        eq.SetResourceReference(TextBlock.ForegroundProperty, "EditorForeground");

        _valueText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping      = TextWrapping.NoWrap,
        };
        _valueText.SetResourceReference(TextBlock.ForegroundProperty, "DebuggerValueForeground");

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_tokenText);
        row.Children.Add(eq);
        row.Children.Add(_valueText);

        _border = new Border
        {
            Padding         = new Thickness(8, 4, 8, 4),
            CornerRadius    = new CornerRadius(3),
            Child           = row,
            Effect          = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.3 },
        };
        _border.SetResourceReference(Border.BackgroundProperty,   "ToolTipBackground");
        _border.SetResourceReference(Border.BorderBrushProperty,  "PopupBorder");
        _border.BorderThickness = new Thickness(1);

        Child = _border;

        MouseEnter += (_, _) => { _mouseInside = true;  _graceTimer.Stop(); };
        MouseLeave += (_, _) => { _mouseInside = false; _graceTimer.Start(); };
    }

    public void Show(UIElement placementTarget, string token, string value, Rect anchorRect)
    {
        _tokenText.Text = token;
        _valueText.Text = value;

        PlacementTarget    = placementTarget;
        PlacementRectangle = anchorRect;
        HorizontalOffset   = 0;
        VerticalOffset     = -(_border.ActualHeight > 0 ? _border.ActualHeight + 2 : 26);

        if (!IsOpen) IsOpen = true;
    }

    public void Hide()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }

    public void Dispose()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }
}
