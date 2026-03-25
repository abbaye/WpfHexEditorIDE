// ==========================================================
// Project: WpfHexEditor.App
// File: Options/TabPreviewOptionsPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-25
// Description:
//     Placeholder options page for Tab Preview settings.
//     Tab preview configuration (hover thumbnail, delay, etc.) — to be implemented.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// Options page placeholder for Tab Preview settings.
/// </summary>
public sealed class TabPreviewOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    public TabPreviewOptionsPage()
    {
        Padding = new Thickness(16);
        Content = new TextBlock
        {
            Text       = "Tab Preview settings — coming soon.",
            FontStyle  = FontStyles.Italic,
            Opacity    = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public void Load(AppSettings s) { }
    public void Flush(AppSettings s) { }
}
