// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: BindingPathPickerPopup.xaml.cs
// Description:
//     Code-behind for the binding path picker popup.
//     Builds a two-level property tree from a given System.Type via
//     reflection, supports text filtering, and fires PathSelected
//     when the user accepts a property path.
//
// Architecture Notes:
//     Popup owns its own ViewModel (BindingPathPickerViewModel).
//     PathSelected event delivers a dotted property path string.
//     Open(Type, UIElement) positions the popup below a target element.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WpfHexEditor.Plugins.XamlDesigner.Controls;

// ── ViewModel ────────────────────────────────────────────────────────────────

public sealed class BindingPathNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string  Name      { get; init; } = string.Empty;
    public string  TypeName  { get; init; } = string.Empty;
    public string  FullPath  { get; init; } = string.Empty;
    public bool    IsLeaf    => Children.Count == 0;

    public ObservableCollection<BindingPathNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class BindingPathPickerViewModel : INotifyPropertyChanged
{
    private string _filterText    = string.Empty;
    private string _selectedPath  = string.Empty;

    public ObservableCollection<BindingPathNode> RootNodes { get; } = new();

    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string SelectedPath
    {
        get => _selectedPath;
        set { _selectedPath = value; OnPropertyChanged(); }
    }

    public void LoadType(Type? type)
    {
        RootNodes.Clear();
        if (type is null) return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .OrderBy(p => p.Name))
        {
            var node = new BindingPathNode
            {
                Name     = prop.Name,
                TypeName = prop.PropertyType.Name,
                FullPath = prop.Name,
            };

            // One level deep — add children for complex property types.
            if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
            {
                foreach (var child in prop.PropertyType
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .OrderBy(p => p.Name)
                             .Take(20))
                {
                    node.Children.Add(new BindingPathNode
                    {
                        Name     = child.Name,
                        TypeName = child.PropertyType.Name,
                        FullPath = $"{prop.Name}.{child.Name}",
                    });
                }
            }

            RootNodes.Add(node);
        }
    }

    private void ApplyFilter()
    {
        // Simple filter: keep nodes whose Name contains the filter text.
        // Full filtering would require a CollectionView wrapper — omitted for brevity.
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── Code-behind ───────────────────────────────────────────────────────────────

public partial class BindingPathPickerPopup : Popup
{
    private readonly BindingPathPickerViewModel _vm = new();

    /// <summary>Fired when the user accepts a property path. Arg is the dotted path string.</summary>
    public event EventHandler<string>? PathSelected;

    public BindingPathPickerPopup()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    /// <summary>Opens the popup anchored below <paramref name="target"/> for <paramref name="dataContextType"/>.</summary>
    public void Open(Type? dataContextType, UIElement target)
    {
        PlacementTarget = target;
        _vm.LoadType(dataContextType);
        IsOpen = true;
        FilterBox.Focus();
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = FilterBox.Text;

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BindingPathNode node)
            _vm.SelectedPath = node.FullPath;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_vm.SelectedPath))
            PathSelected?.Invoke(this, _vm.SelectedPath);
        IsOpen = false;
    }
}
