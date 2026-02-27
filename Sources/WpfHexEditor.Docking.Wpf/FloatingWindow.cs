//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// A floating window that contains a dock group (one or more tabbed items).
/// </summary>
public class FloatingWindow : Window
{
    private readonly DockTabControl _tabControl;

    public DockGroupNode? Node { get; private set; }

    /// <summary>
    /// The main DockItem this floating window was created for.
    /// </summary>
    public DockItem? Item { get; private set; }

    public event Action<DockItem>? TabCloseRequested;
    public event Action<DockItem>? TabDragStarted;
    public event Action<DockItem>? TabFloatRequested;
    public event Action<DockItem>? TabAutoHideRequested;

    /// <summary>
    /// Raised when the user double-clicks the title bar to re-dock.
    /// </summary>
    public event Action<DockItem>? ReDockRequested;

    public FloatingWindow()
    {
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        Width = 400;
        Height = 300;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        _tabControl = new DockTabControl();
        _tabControl.TabCloseRequested += item => TabCloseRequested?.Invoke(item);
        _tabControl.TabDragStarted += item => TabDragStarted?.Invoke(item);
        _tabControl.TabFloatRequested += item => TabFloatRequested?.Invoke(item);
        _tabControl.TabAutoHideRequested += item => TabAutoHideRequested?.Invoke(item);

        Content = _tabControl;

        MouseDoubleClick += OnMouseDoubleClick;
    }

    public void Bind(DockGroupNode node, DockItem item, Func<DockItem, object>? contentFactory = null)
    {
        Node = node;
        Item = item;
        _tabControl.Bind(node, contentFactory);

        if (node.ActiveItem is not null)
            Title = node.ActiveItem.Title;
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Item is not null)
            ReDockRequested?.Invoke(Item);
    }
}

/// <summary>
/// Manages creation and lifecycle of floating windows.
/// </summary>
public class FloatingWindowManager
{
    private readonly DockControl _dockControl;
    private readonly List<FloatingWindow> _windows = [];

    public IReadOnlyList<FloatingWindow> Windows => _windows;

    public FloatingWindowManager(DockControl dockControl)
    {
        _dockControl = dockControl;
    }

    /// <summary>
    /// Creates a floating window for the given item.
    /// </summary>
    public FloatingWindow CreateFloatingWindow(DockItem item, Point? position = null)
    {
        var group = new DockGroupNode();
        group.AddItem(item);

        var window = new FloatingWindow();
        window.Bind(group, item, _dockControl.ContentFactory);

        if (position.HasValue)
        {
            window.Left = position.Value.X;
            window.Top = position.Value.Y;
        }
        else
        {
            // Center relative to the main window
            var owner = Window.GetWindow(_dockControl);
            if (owner is not null)
            {
                window.Left = owner.Left + (owner.Width - window.Width) / 2;
                window.Top = owner.Top + (owner.Height - window.Height) / 2;
            }
        }

        window.Closed += (_, _) => _windows.Remove(window);

        window.TabCloseRequested += i =>
        {
            _dockControl.Engine?.Close(i);
            if (window.Node?.IsEmpty == true)
                window.Close();
        };

        window.TabDragStarted += i =>
        {
            // Re-dock: dock back to MainDocumentHost and close the floating window
            if (_dockControl.Engine is not null)
            {
                _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, DockDirection.Center);
                _dockControl.RebuildVisualTree();
                window.Close();
            }
        };

        window.ReDockRequested += i =>
        {
            if (_dockControl.Engine is not null)
            {
                _dockControl.Engine.Dock(i, _dockControl.Layout!.MainDocumentHost, DockDirection.Center);
                _dockControl.RebuildVisualTree();
                window.Close();
            }
        };

        _windows.Add(window);
        window.Owner = Window.GetWindow(_dockControl);
        window.Show();

        return window;
    }

    /// <summary>
    /// Finds a floating window containing the given item.
    /// </summary>
    public FloatingWindow? FindWindowForItem(DockItem item)
    {
        return _windows.FirstOrDefault(w => w.Item == item);
    }

    /// <summary>
    /// Closes the floating window for the given item if it exists.
    /// </summary>
    public void CloseWindowForItem(DockItem item)
    {
        var window = FindWindowForItem(item);
        window?.Close();
    }

    /// <summary>
    /// Closes all floating windows.
    /// </summary>
    public void CloseAll()
    {
        foreach (var window in _windows.ToList())
            window.Close();
    }
}
