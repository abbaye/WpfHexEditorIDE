// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignDataPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
// Description:
//     Code-behind for the Design-Time Data panel.
//     Detects d:DesignInstance, instantiates the type, builds a property
//     TreeView (depth ≤ 3), JSON tab, and XAML snippet tab.
//     Added: DesignDataNodeViewModel nested class, BuildTree factory,
//            Generate Mock Data handler, Pick Type handler,
//            JsonPreview, XamlPreview, PropertyNodes.
//
// Architecture Notes:
//     VS-Like dockable panel.
//     Lifecycle rule: OnUnloaded must NOT null _xamlSource.
//     DesignDataNodeViewModel is a local nested class — self-contained.
//     BuildTree recurses up to depth 3 to avoid deep object graphs.
//     IEnumerable members shown as "(N items)" leaves with no children.
// ==========================================================

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Panel that inspects design-time data (d:DesignInstance) for the current XAML.
/// </summary>
public partial class DesignDataPanel : UserControl
{
    private const int MaxTreeDepth = 3;

    private readonly DesignTimeXamlPreprocessor _preprocessor   = new();
    private readonly MockDataGenerator           _mockGenerator  = new();
    private readonly DesignDataJsonSerializer    _jsonSerializer = new();

    private string  _xamlSource = string.Empty;
    private object? _currentInstance;

    public ObservableCollection<DesignDataNodeViewModel> PropertyNodes { get; } = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignDataPanel()
    {
        InitializeComponent();
        PropertyTree.ItemsSource = PropertyNodes;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Sets the XAML source and refreshes the panel display.</summary>
    public void SetXamlSource(string xaml)
    {
        _xamlSource = xaml;
        Refresh();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => Refresh();

    private void OnGenerateMockDataClick(object sender, RoutedEventArgs e)
    {
        if (_currentInstance is null) return;
        _mockGenerator.PopulateMockData(_currentInstance);
        Refresh();
    }

    // ── Type Picker ───────────────────────────────────────────────────────────

    private List<Type> _allKnownTypes = [];
    private List<Type> _filteredTypes = [];

    private void EnsureKnownTypes()
    {
        if (_allKnownTypes.Count > 0) return;
        _allKnownTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetExportedTypes(); } catch { return []; } })
            .Where(t => t.IsClass && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) is not null
                        && t.Namespace?.StartsWith("System")    != true
                        && t.Namespace?.StartsWith("Microsoft") != true)
            .OrderBy(t => t.Name)
            .Take(500)
            .ToList();
    }

    private void OnPickTypeClick(object sender, RoutedEventArgs e)
    {
        EnsureKnownTypes();
        _filteredTypes = _allKnownTypes;
        TypePickerList.ItemsSource = _filteredTypes.Select(t => t.FullName).ToList();
        TypeSearchBox.Text = string.Empty;
        TypePickerPopup.IsOpen = true;
        TypeSearchBox.Focus();
    }

    private void OnTypeSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = TypeSearchBox.Text.Trim();
        _filteredTypes = string.IsNullOrEmpty(query)
            ? _allKnownTypes
            : _allKnownTypes.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        TypePickerList.ItemsSource = _filteredTypes
            .Select(t => $"{t.Name}  ({t.Namespace})")
            .ToList();
    }

    private void CommitTypePick()
    {
        int idx = TypePickerList.SelectedIndex;
        if (idx < 0 || idx >= _filteredTypes.Count) return;
        TypePickerPopup.IsOpen = false;
        _currentInstance = Activator.CreateInstance(_filteredTypes[idx]);
        StatusTypeLabel.Text = _filteredTypes[idx].Name;
        RefreshFromInstance();
    }

    private void OnTypePickerDoubleClick(object sender, MouseButtonEventArgs e)
        => CommitTypePick();

    private void OnTypePickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  CommitTypePick();
        else if (e.Key == Key.Escape) TypePickerPopup.IsOpen = false;
    }

    // ── New toolbar handlers ──────────────────────────────────────────────────

    private void OnCopyJsonClick(object sender, RoutedEventArgs e)
    {
        var text = JsonTextBox?.Text;
        if (!string.IsNullOrEmpty(text))
            System.Windows.Clipboard.SetText(text);
    }

    private void OnClearDataClick(object sender, RoutedEventArgs e)
    {
        _currentInstance = null;
        ShowPlaceholders("(no design data)");
    }

    private void OnPasteJsonClick(object sender, RoutedEventArgs e)
    {
        var json = System.Windows.Clipboard.GetText();
        if (string.IsNullOrEmpty(json) || _currentInstance is null) return;

        try
        {
            var pasted = System.Text.Json.JsonSerializer.Deserialize(
                json, _currentInstance.GetType());
            if (pasted is null) return;
            _currentInstance = pasted;
            RefreshFromInstance();
        }
        catch { /* silently ignore malformed JSON */ }
    }

    /// <summary>
    /// Populates all tabs from the current <see cref="_currentInstance"/> directly,
    /// bypassing the XAML-source check used by <see cref="Refresh"/>.
    /// Used when the instance was set via the type picker or clipboard paste.
    /// </summary>
    private void RefreshFromInstance()
    {
        if (_currentInstance is null) return;
        PopulatePropertyTab(_currentInstance);
        PopulateJsonTab(_currentInstance);
        PopulateXamlTab(_currentInstance);
        StatusTypeLabel.Text = _currentInstance.GetType().Name;
    }

    // ── Private: refresh pipeline ─────────────────────────────────────────────

    private void Refresh()
    {
        if (!DesignTimeXamlPreprocessor.HasDesignNamespace(_xamlSource))
        {
            ShowPlaceholders("No d:DesignInstance detected in current XAML.");
            return;
        }

        _preprocessor.Process(_xamlSource, out object? instance);

        if (instance is null)
        {
            ShowPlaceholders("d:DesignInstance found but type could not be resolved.");
            return;
        }

        _currentInstance = instance;
        PopulatePropertyTab(instance);
        PopulateJsonTab(instance);
        PopulateXamlTab(instance);
        StatusTypeLabel.Text = instance.GetType().Name;
    }

    // ── Properties tab ────────────────────────────────────────────────────────

    private void PopulatePropertyTab(object instance)
    {
        PropertyNodes.Clear();
        foreach (var node in DesignDataNodeViewModel.BuildTree(instance, 0))
            PropertyNodes.Add(node);

        PropertiesPlaceholder.Visibility = PropertyNodes.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        PropertyTree.Visibility = PropertyNodes.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── JSON tab ──────────────────────────────────────────────────────────────

    private void PopulateJsonTab(object instance)
    {
        string json = _jsonSerializer.Serialize(instance);
        JsonTextBox.Text        = json;
        JsonTextBox.Visibility  = Visibility.Visible;
        JsonPlaceholder.Visibility = Visibility.Collapsed;
    }

    // ── XAML tab ──────────────────────────────────────────────────────────────

    private void PopulateXamlTab(object instance)
    {
        string typeName = instance.GetType().FullName ?? instance.GetType().Name;
        string snippet  = BuildDesignInstanceSnippet(typeName);
        XamlTextBox.Text        = snippet;
        XamlTextBox.Visibility  = Visibility.Visible;
        XamlPlaceholder.Visibility = Visibility.Collapsed;
    }

    private static string BuildDesignInstanceSnippet(string typeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Add to your UserControl/Window root element: -->");
        sb.AppendLine("xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"");
        sb.AppendLine("xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
        sb.AppendLine("mc:Ignorable=\"d\"");
        sb.AppendLine();
        sb.AppendLine($"d:DataContext=\"{{d:DesignInstance Type={{x:Type local:{GetLocalName(typeName)}}}}}\"");
        return sb.ToString();
    }

    private static string GetLocalName(string fullName)
    {
        int dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    // ── Placeholders ─────────────────────────────────────────────────────────

    private void ShowPlaceholders(string message)
    {
        _currentInstance = null;
        StatusTypeLabel.Text = "(no design data)";

        PropertyNodes.Clear();
        PropertiesPlaceholder.Text       = message;
        PropertiesPlaceholder.Visibility = Visibility.Visible;
        PropertyTree.Visibility          = Visibility.Collapsed;

        JsonTextBox.Visibility           = Visibility.Collapsed;
        JsonPlaceholder.Visibility       = Visibility.Visible;

        XamlTextBox.Visibility           = Visibility.Collapsed;
        XamlPlaceholder.Visibility       = Visibility.Visible;
    }
}

// ==========================================================
// DesignDataNodeViewModel — local nested class
// Represents one node in the Properties TreeView.
// ==========================================================

/// <summary>
/// A single node in the Design Data Properties tree.
/// Supports recursive child expansion up to depth 3.
/// </summary>
public sealed class DesignDataNodeViewModel : INotifyPropertyChanged
{
    // ── Properties ────────────────────────────────────────────────────────────

    public string Name         { get; }
    public string ValueDisplay { get; }
    public bool   HasChildren  => Children.Count > 0;

    public ObservableCollection<DesignDataNodeViewModel> Children { get; } = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    private DesignDataNodeViewModel(string name, string valueDisplay)
    {
        Name         = name;
        ValueDisplay = valueDisplay;
    }

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively builds tree nodes from <paramref name="instance"/> up to depth 3.
    /// </summary>
    public static IEnumerable<DesignDataNodeViewModel> BuildTree(object? instance, int depth)
    {
        if (instance is null || depth >= 3) yield break;

        var props = instance.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
            yield return BuildNode(prop, instance, depth);
    }

    private static DesignDataNodeViewModel BuildNode(
        PropertyInfo prop, object instance, int depth)
    {
        object? value = null;
        try { value = prop.GetValue(instance); }
        catch { /* silently skip */ }

        string display = BuildValueDisplay(prop.PropertyType, value);
        var node = new DesignDataNodeViewModel(prop.Name, display);

        if (value is not null && IsExpandable(prop.PropertyType, value))
            AddChildren(node, value, depth);

        return node;
    }

    private static string BuildValueDisplay(Type type, object? value)
    {
        if (value is null) return "(null)";
        if (IsEnumerableType(type)) return $"({CountItems(value)} items)";
        if (IsComplexObject(type)) return $"[{type.Name}]";
        return value.ToString() ?? "(null)";
    }

    private static bool IsExpandable(Type type, object value)
        => IsComplexObject(type) && !IsEnumerableType(type);

    private static void AddChildren(
        DesignDataNodeViewModel parent, object value, int depth)
    {
        foreach (var child in BuildTree(value, depth + 1))
            parent.Children.Add(child);
    }

    private static bool IsComplexObject(Type type)
        => !type.IsPrimitive && type != typeof(string)
        && !type.IsEnum && type != typeof(DateTime)
        && type != typeof(TimeSpan) && type != typeof(Guid);

    private static bool IsEnumerableType(Type type)
        => type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type);

    private static int CountItems(object value)
    {
        int count = 0;
        if (value is IEnumerable enumerable)
            foreach (var _ in enumerable) count++;
        return count;
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
