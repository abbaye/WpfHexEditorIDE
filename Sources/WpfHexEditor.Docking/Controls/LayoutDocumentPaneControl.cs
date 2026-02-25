// Apache 2.0 - 2026
// WpfHexEditor.Docking - VS-Like Docking Framework

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Model;

namespace WpfHexEditor.Docking.Controls;

/// <summary>
/// Renders a LayoutDocumentPane as a tabbed area with document tab headers and close buttons.
/// Layout: [tab strip at top] | [content area]
/// </summary>
public class LayoutDocumentPaneControl : Control
{
    private LayoutDocumentPane? _subscribedModel;

    static LayoutDocumentPaneControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LayoutDocumentPaneControl),
            new FrameworkPropertyMetadata(typeof(LayoutDocumentPaneControl)));
    }

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(LayoutDocumentPane), typeof(LayoutDocumentPaneControl),
            new PropertyMetadata(null, OnModelChanged));

    public LayoutDocumentPane? Model
    {
        get => (LayoutDocumentPane?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (LayoutDocumentPaneControl)d;
        if (ctrl._subscribedModel != null)
        {
            ctrl._subscribedModel.PropertyChanged -= ctrl.OnModelPropertyChanged;
            ctrl._subscribedModel.Children.CollectionChanged -= ctrl.OnChildrenChanged;
        }

        ctrl._subscribedModel = e.NewValue as LayoutDocumentPane;

        if (ctrl._subscribedModel != null)
        {
            ctrl._subscribedModel.PropertyChanged += ctrl.OnModelPropertyChanged;
            ctrl._subscribedModel.Children.CollectionChanged += ctrl.OnChildrenChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Template bindings will auto-update via Model's INPC
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Template bindings handle this via ItemsSource
    }

    /// <summary>Select a document tab.</summary>
    internal void SelectDocument(LayoutDocument document)
    {
        if (Model != null)
            Model.SelectedContent = document;
    }

    /// <summary>Close a document tab.</summary>
    internal void CloseDocument(LayoutDocument document)
    {
        document.Close();
    }
}
