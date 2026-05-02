//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// A thin, dismissible banner shown above any non-hex viewer (ImageViewer, TblEditor, etc.)
/// to surface quick "View in …" cross-editor action buttons.
/// <para>
/// Call <see cref="Configure"/> once after creation.  The bar fires
/// <see cref="OpenWithRequested"/> when the user clicks an action button.
/// </para>
/// </summary>
public partial class DocumentInfoBar : UserControl
{

    private string _filePath        = string.Empty;
    private string _sourceContentId = string.Empty;

    /// <summary>
    /// Fired when the user clicks one of the "View in …" action buttons.
    /// </summary>
    public event EventHandler<OpenWithEditorRequestedEventArgs>? OpenWithRequested;

    public DocumentInfoBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configures the bar content.
    /// </summary>
    /// <param name="filePath">Absolute path of the document being viewed.</param>
    /// <param name="sourceContentId">ContentId of the host <c>DockItem</c> tab.</param>
    /// <param name="currentEditorName">Display name of the currently active editor.</param>
    /// <param name="currentEditorId">Factory id of the current editor (used for icon lookup).</param>
    /// <param name="alternatives">Alternative editor factories to offer as action buttons.</param>
    public void Configure(
        string filePath,
        string sourceContentId,
        string currentEditorName,
        string currentEditorId,
        IEnumerable<IEditorFactory> alternatives)
    {
        _filePath        = filePath;
        _sourceContentId = sourceContentId;

        EditorNameText.Text = currentEditorName;

        // Remove previously added dynamic buttons (keep ViewInLabel)
        while (ActionButtons.Children.Count > 1)
            ActionButtons.Children.RemoveAt(ActionButtons.Children.Count - 1);

        // Only offer "Hex Editor" when the current editor is not already the hex editor
        if (currentEditorId != "hex-editor")
            ActionButtons.Children.Add(MakeButton(AppResources.App_InfoBar_HexEditor, null));

        // Add alternatives from registry
        foreach (var factory in alternatives)
            ActionButtons.Children.Add(MakeButton(factory.Descriptor.DisplayName, factory.Descriptor.Id));
    }

    // -- Event handlers ------------------------------------------------------

    private void OnDismiss(object sender, RoutedEventArgs e)
        => Visibility = Visibility.Collapsed;

    private void OnActionButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            OpenWithRequested?.Invoke(this, new OpenWithEditorRequestedEventArgs
            {
                FactoryId       = btn.Tag as string,   // null ⇒ Hex Editor fallback (handled by MainWindow)
                FilePath        = _filePath,
                SourceContentId = _sourceContentId,
            });
    }

    // -- Helpers -------------------------------------------------------------

    private Button MakeButton(string label, string? factoryId)
    {
        var btn = new Button
        {
            Content    = label,
            Tag        = factoryId,     // null ⇒ Hex Editor fallback
            Style      = (Style)FindResource("InfoBarButtonStyle"),
            Focusable  = false,         // Prevent these buttons from intercepting Enter/Space keyboard events
            IsTabStop  = false,
        };
        btn.Click += OnActionButtonClick;
        return btn;
    }
}
