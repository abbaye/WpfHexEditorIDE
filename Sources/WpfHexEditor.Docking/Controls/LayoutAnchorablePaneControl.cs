// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Renders a LayoutAnchorablePane as a tool window with header bar, tab strip, and content.
/// Layout: [header with title+buttons] | [tab strip if multiple] | [content area]
/// </summary>
public class LayoutAnchorablePaneControl : Control
{
    private LayoutAnchorablePane? _subscribedModel;

    static LayoutAnchorablePaneControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LayoutAnchorablePaneControl),
            new FrameworkPropertyMetadata(typeof(LayoutAnchorablePaneControl)));
    }

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(LayoutAnchorablePane), typeof(LayoutAnchorablePaneControl),
            new PropertyMetadata(null, OnModelChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LayoutAnchorablePaneControl),
            new PropertyMetadata(false));

    public LayoutAnchorablePane? Model
    {
        get => (LayoutAnchorablePane?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (LayoutAnchorablePaneControl)d;
        if (ctrl._subscribedModel != null)
        {
            ctrl._subscribedModel.PropertyChanged -= ctrl.OnModelPropertyChanged;
            ctrl._subscribedModel.Children.CollectionChanged -= ctrl.OnChildrenChanged;
        }

        ctrl._subscribedModel = e.NewValue as LayoutAnchorablePane;

        if (ctrl._subscribedModel != null)
        {
            ctrl._subscribedModel.PropertyChanged += ctrl.OnModelPropertyChanged;
            ctrl._subscribedModel.Children.CollectionChanged += ctrl.OnChildrenChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutAnchorablePane.SelectedContent))
        {
            IsActive = Model?.SelectedContent?.IsActive ?? false;
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
    }

    /// <summary>Select an anchorable tab.</summary>
    internal void SelectAnchorable(LayoutAnchorable anchorable)
    {
        if (Model != null)
            Model.SelectedContent = anchorable;
    }

    /// <summary>Close/hide the selected anchorable.</summary>
    internal void CloseSelectedAnchorable()
    {
        Model?.SelectedContent?.Hide();
    }

    /// <summary>Toggle auto-hide on the selected anchorable.</summary>
    internal void ToggleAutoHide()
    {
        Model?.SelectedContent?.ToggleAutoHide();
    }
}
