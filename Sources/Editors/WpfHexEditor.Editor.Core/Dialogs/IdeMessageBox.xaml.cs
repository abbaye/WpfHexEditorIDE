// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Dialogs/IdeMessageBox.xaml.cs
// Description:
//     Themed replacement for System.Windows.MessageBox.
//     Inherits IDE color theme via DynamicResource (Dark / Light / High Contrast).
//
// Architecture Notes:
//     Extends ThemedDialog (custom VS-style chrome + multi-monitor maximize).
//     Button layout is built in code-behind so button labels can be localized later.
//     Static Show() mirrors the MessageBox.Show() API for easy call-site migration.
//     Lives in Editor.Core (not App) so Shell panels and plugins can reference it
//     without creating a dependency on the WpfHexEditor.App assembly.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Editor.Core.Dialogs;

/// <summary>
/// Themed modal message dialog. Use <see cref="Show"/> (or <see cref="ShowAsync"/>
/// via <c>IDialogService</c>) instead of <c>System.Windows.MessageBox</c>.
/// </summary>
public sealed partial class IdeMessageBox : ThemedDialog
{
    // -- Icon glyphs (Segoe MDL2 Assets) ------------------------------------
    private const string GlyphError       = "";  // ErrorBadge
    private const string GlyphWarning     = "";  // Warning
    private const string GlyphInformation = "";  // Info
    private const string GlyphQuestion    = "";  // Help

    // -- Pre-built icon brushes (frozen — avoids per-call allocation) -------
    private static readonly SolidColorBrush BrushError       = Frozen(0xE0, 0x50, 0x50);
    private static readonly SolidColorBrush BrushWarning     = Frozen(0xE0, 0xA0, 0x30);
    private static readonly SolidColorBrush BrushInformation = Frozen(0x40, 0x90, 0xD0);
    private static readonly SolidColorBrush BrushQuestion    = Frozen(0xA0, 0x70, 0xD0);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }

    // -- Result -------------------------------------------------------------
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    // -- Constructor --------------------------------------------------------

    private IdeMessageBox()
    {
        InitializeComponent();
        // Map X-button close to Cancel so callers see Result.Cancel, not Result.None.
        Closing += (_, _) =>
        {
            if (DialogResult is null)
            {
                Result       = MessageBoxResult.Cancel;
                DialogResult = false;
            }
        };
    }

    // -- Static entry points ------------------------------------------------

    /// <summary>
    /// Shows a themed message dialog. Must be called on the UI thread.
    /// </summary>
    public static MessageBoxResult Show(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null)
    {
        var dlg = Build(message, title, button, icon, owner);
        dlg.ShowDialog();
        return dlg.Result;
    }

    /// <summary>
    /// Async version — marshals to the UI thread.
    /// Safe to call from background tasks or async ViewModels.
    /// </summary>
    public static async Task<MessageBoxResult> ShowAsync(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null)
        => await Application.Current.Dispatcher.InvokeAsync(
            () => Show(message, title, button, icon, owner));

    // -- Builder ------------------------------------------------------------

    private static IdeMessageBox Build(
        string           message,
        string           title,
        MessageBoxButton button,
        MessageBoxImage  icon,
        Window?          owner)
    {
        var dlg = new IdeMessageBox
        {
            Title = title,
            Owner = owner ?? TryGetMainWindow(),
        };

        dlg.MessageText.Text = message;
        dlg.ApplyIcon(icon);
        dlg.BuildButtons(button);

        return dlg;
    }

    private static Window? TryGetMainWindow()
    {
        try { return Application.Current?.MainWindow; }
        catch { return null; }
    }

    // -- Icon ---------------------------------------------------------------

    private void ApplyIcon(MessageBoxImage icon)
    {
        if (icon == MessageBoxImage.None) return;

        IconGlyph.Visibility = Visibility.Visible;

        (IconGlyph.Text, IconGlyph.Foreground) = icon switch
        {
            MessageBoxImage.Error       => (GlyphError,       BrushError),
            MessageBoxImage.Warning     => (GlyphWarning,     BrushWarning),
            MessageBoxImage.Information => (GlyphInformation, BrushInformation),
            MessageBoxImage.Question    => (GlyphQuestion,    BrushQuestion),
            _                           => (GlyphInformation, BrushInformation),
        };
    }

    // -- Buttons ------------------------------------------------------------

    private void BuildButtons(MessageBoxButton config)
    {
        switch (config)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, isDefault: true, isPrimary: true);
                break;

            case MessageBoxButton.OKCancel:
                AddButton("OK",     MessageBoxResult.OK,     isDefault: true, isPrimary: true);
                AddButton("Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;

            case MessageBoxButton.YesNo:
                AddButton("Yes", MessageBoxResult.Yes, isDefault: true, isPrimary: true);
                AddButton("No",  MessageBoxResult.No);
                break;

            case MessageBoxButton.YesNoCancel:
                AddButton("Yes",    MessageBoxResult.Yes,    isDefault: true, isPrimary: true);
                AddButton("No",     MessageBoxResult.No);
                AddButton("Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;
        }
    }

    private void AddButton(
        string           label,
        MessageBoxResult result,
        bool             isDefault = false,
        bool             isCancel  = false,
        bool             isPrimary = false)
    {
        var btn = new Button
        {
            Content   = label,
            IsDefault = isDefault,
            IsCancel  = isCancel,
        };

        if (isPrimary)
        {
            btn.Background  = new SolidColorBrush(Color.FromRgb(0x6B, 0x3F, 0xA0));
            btn.Foreground  = Brushes.White;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7B, 0x4F, 0xB0));
        }

        btn.Click += (_, _) =>
        {
            Result       = result;
            DialogResult = result != MessageBoxResult.Cancel && result != MessageBoxResult.No;
            Close();
        };

        ButtonPanel.Children.Add(btn);
    }
}
