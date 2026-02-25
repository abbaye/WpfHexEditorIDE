// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.ComponentModel;

namespace WpfHexEditor.Docking.Model;

/// <summary>
/// Abstract base for content items (documents and tool windows).
/// Provides common properties: Title, Content, close behavior, etc.
/// </summary>
public abstract class LayoutContent : LayoutElement
{
    private string _title = string.Empty;
    private object? _content;
    private object? _iconSource;
    private bool _isActive;
    private bool _isSelected;
    private bool _canClose = true;
    private string? _contentId;
    private string? _toolTip;

    /// <summary>Display title shown in tabs and headers.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>The actual UI content (FrameworkElement or ViewModel).</summary>
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>Optional icon (ImageSource or string path).</summary>
    public object? IconSource
    {
        get => _iconSource;
        set => SetProperty(ref _iconSource, value);
    }

    /// <summary>Whether this content is the currently focused/active item.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>Whether this content is the selected tab in its parent pane.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Whether the close button is available.</summary>
    public bool CanClose
    {
        get => _canClose;
        set => SetProperty(ref _canClose, value);
    }

    /// <summary>Identifier for serialization matching. Must be unique across the layout.</summary>
    public string? ContentId
    {
        get => _contentId;
        set => SetProperty(ref _contentId, value);
    }

    /// <summary>Optional tooltip for the tab header.</summary>
    public string? ToolTip
    {
        get => _toolTip;
        set => SetProperty(ref _toolTip, value);
    }

    /// <summary>Raised before the content is closed. Set Cancel to prevent closing.</summary>
    public event EventHandler<System.ComponentModel.CancelEventArgs>? Closing;

    /// <summary>Raised after the content has been closed.</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Closes this content item. Raises Closing event; if not cancelled, removes from parent.
    /// </summary>
    public virtual void Close()
    {
        var args = new System.ComponentModel.CancelEventArgs();
        Closing?.Invoke(this, args);
        if (args.Cancel) return;

        RemoveFromParent();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remove this item from whatever parent collection holds it.</summary>
    internal void RemoveFromParent()
    {
        switch (Parent)
        {
            case LayoutDocumentPane docPane:
                docPane.Children.Remove((this as LayoutDocument)!);
                break;
            case LayoutAnchorablePane anchPane:
                anchPane.Children.Remove((this as LayoutAnchorable)!);
                break;
            case LayoutAnchorGroup group:
                group.Children.Remove((this as LayoutAnchorable)!);
                break;
        }
    }
}

