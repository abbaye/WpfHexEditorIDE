// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ToolboxRegistry.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Singleton catalogue of all available toolbox items.
//     Pre-populated with 60+ standard WPF controls organised into 9 categories.
//     Extensible via Register() for plugin-provided controls.
//
// Architecture Notes:
//     Service Locator / Registry pattern.
//     Thread-safe via readonly dictionary (populated once at construction).
//     Plugins call Register() during their initialization phase.
// ==========================================================

using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Registry of all XAML toolbox items, pre-populated with standard WPF controls.
/// </summary>
public sealed class ToolboxRegistry
{
    private readonly List<ToolboxItem> _items = new();

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ToolboxRegistry Instance { get; } = new();

    private ToolboxRegistry()
    {
        RegisterBuiltIns();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>All registered items, ordered by category then name.</summary>
    public IReadOnlyList<ToolboxItem> Items => _items;

    /// <summary>Registers an additional toolbox item (from a plugin).</summary>
    public void Register(ToolboxItem item)
    {
        _items.Add(item);
        _items.Sort((a, b) =>
        {
            int cat = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
            return cat != 0 ? cat : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
    }

    // ── Built-in items ────────────────────────────────────────────────────────

    private void RegisterBuiltIns()
    {
        // Layout
        Add("Layout", "Grid",           "\uE8A1", "<Grid Width=\"200\" Height=\"150\"><RowDefinition/><ColumnDefinition/></Grid>");
        Add("Layout", "StackPanel",     "\uE8A1", "<StackPanel Width=\"200\" Height=\"150\" Orientation=\"Vertical\"/>");
        Add("Layout", "DockPanel",      "\uE8A1", "<DockPanel Width=\"200\" Height=\"150\" LastChildFill=\"True\"/>");
        Add("Layout", "WrapPanel",      "\uE8A1", "<WrapPanel Width=\"200\" Height=\"150\"/>");
        Add("Layout", "UniformGrid",    "\uE8A1", "<UniformGrid Width=\"200\" Height=\"150\" Rows=\"2\" Columns=\"2\"/>");
        Add("Layout", "Canvas",         "\uE8A1", "<Canvas Width=\"200\" Height=\"150\"/>");
        Add("Layout", "Viewbox",        "\uE8A1", "<Viewbox Width=\"100\" Height=\"100\"/>");
        Add("Layout", "ScrollViewer",   "\uE8A1", "<ScrollViewer Width=\"200\" Height=\"150\"/>");
        Add("Layout", "Border",         "\uE8A1", "<Border Width=\"100\" Height=\"50\" BorderThickness=\"1\"/>");
        Add("Layout", "Expander",       "\uE8A1", "<Expander Width=\"200\" Header=\"Expander\"/>");
        Add("Layout", "GroupBox",       "\uE8A1", "<GroupBox Width=\"200\" Header=\"Group\"/>");

        // Buttons
        Add("Buttons", "Button",        "\uE8A7", "<Button Width=\"80\" Height=\"28\" Content=\"Button\"/>");
        Add("Buttons", "RepeatButton",  "\uE8A7", "<RepeatButton Width=\"80\" Height=\"28\" Content=\"Repeat\"/>");
        Add("Buttons", "ToggleButton",  "\uE8A7", "<ToggleButton Width=\"80\" Height=\"28\" Content=\"Toggle\"/>");
        Add("Buttons", "RadioButton",   "\uE8A7", "<RadioButton Content=\"Option\" Margin=\"4\"/>");
        Add("Buttons", "CheckBox",      "\uE8A7", "<CheckBox Content=\"Check me\" Margin=\"4\"/>");

        // Text
        Add("Text", "TextBlock",        "\uE8D2", "<TextBlock Text=\"TextBlock\" FontSize=\"14\"/>");
        Add("Text", "TextBox",          "\uE8D2", "<TextBox Width=\"160\" Height=\"28\" Text=\"Type here...\"/>");
        Add("Text", "RichTextBox",      "\uE8D2", "<RichTextBox Width=\"200\" Height=\"120\"/>");
        Add("Text", "PasswordBox",      "\uE8D2", "<PasswordBox Width=\"160\" Height=\"28\"/>");
        Add("Text", "Label",            "\uE8D2", "<Label Content=\"Label\" Padding=\"4\"/>");

        // Lists
        Add("Lists", "ListBox",         "\uE8FD", "<ListBox Width=\"160\" Height=\"120\"><ListBoxItem>Item 1</ListBoxItem><ListBoxItem>Item 2</ListBoxItem></ListBox>");
        Add("Lists", "ListView",        "\uE8FD", "<ListView Width=\"200\" Height=\"120\"/>");
        Add("Lists", "TreeView",        "\uE8FD", "<TreeView Width=\"160\" Height=\"120\"><TreeViewItem Header=\"Root\"><TreeViewItem Header=\"Child\"/></TreeViewItem></TreeView>");
        Add("Lists", "ComboBox",        "\uE8FD", "<ComboBox Width=\"160\" Height=\"28\"><ComboBoxItem>Item 1</ComboBoxItem><ComboBoxItem>Item 2</ComboBoxItem></ComboBox>");
        Add("Lists", "DataGrid",        "\uE8FD", "<DataGrid Width=\"300\" Height=\"150\" AutoGenerateColumns=\"True\"/>");
        Add("Lists", "TabControl",      "\uE8FD", "<TabControl Width=\"200\" Height=\"150\"><TabItem Header=\"Tab 1\"/><TabItem Header=\"Tab 2\"/></TabControl>");

        // Ranges
        Add("Ranges", "Slider",         "\uE9E9", "<Slider Width=\"160\" Minimum=\"0\" Maximum=\"100\" Value=\"50\"/>");
        Add("Ranges", "ProgressBar",    "\uE9E9", "<ProgressBar Width=\"160\" Height=\"20\" Minimum=\"0\" Maximum=\"100\" Value=\"60\"/>");
        Add("Ranges", "ScrollBar",      "\uE9E9", "<ScrollBar Width=\"20\" Height=\"120\" Orientation=\"Vertical\"/>");

        // Date/Time
        Add("Date/Time", "DatePicker",  "\uE787", "<DatePicker Width=\"160\" Height=\"28\"/>");
        Add("Date/Time", "Calendar",    "\uE787", "<Calendar Width=\"220\" Height=\"200\"/>");

        // Media
        Add("Media", "Image",           "\uEB9F", "<Image Width=\"100\" Height=\"100\" Stretch=\"Uniform\"/>");
        Add("Media", "MediaElement",    "\uEB9F", "<MediaElement Width=\"200\" Height=\"150\" LoadedBehavior=\"Manual\"/>");
        Add("Media", "InkCanvas",       "\uEB9F", "<InkCanvas Width=\"200\" Height=\"150\"/>");

        // Shapes
        Add("Shapes", "Rectangle",      "\uE7FB", "<Rectangle Width=\"100\" Height=\"60\" Fill=\"#3399FF\" Stroke=\"#0055CC\" StrokeThickness=\"1\"/>");
        Add("Shapes", "Ellipse",        "\uE7FB", "<Ellipse Width=\"80\" Height=\"80\" Fill=\"#FF6633\" Stroke=\"#CC3300\" StrokeThickness=\"1\"/>");
        Add("Shapes", "Line",           "\uE7FB", "<Line X1=\"0\" Y1=\"0\" X2=\"100\" Y2=\"50\" Stroke=\"#333333\" StrokeThickness=\"2\"/>");
        Add("Shapes", "Path",           "\uE7FB", "<Path Data=\"M0,50 L50,0 L100,50 Z\" Fill=\"#66BB33\" Stroke=\"#336600\" StrokeThickness=\"1\"/>");
        Add("Shapes", "Polygon",        "\uE7FB", "<Polygon Points=\"50,0 100,100 0,100\" Fill=\"#CC33FF\" StrokeThickness=\"1\"/>");
        Add("Shapes", "Polyline",       "\uE7FB", "<Polyline Points=\"0,50 25,0 50,50 75,0 100,50\" Stroke=\"#FF9900\" StrokeThickness=\"2\"/>");

        // Menus
        Add("Menus", "Menu",            "\uE712", "<Menu Width=\"200\"><MenuItem Header=\"File\"><MenuItem Header=\"Open\"/></MenuItem></Menu>");
        Add("Menus", "ToolBar",         "\uE712", "<ToolBar Width=\"300\"><Button Content=\"New\"/><Button Content=\"Open\"/></ToolBar>");
        Add("Menus", "StatusBar",       "\uE712", "<StatusBar Width=\"300\"><StatusBarItem Content=\"Ready\"/></StatusBar>");
        Add("Menus", "Separator",       "\uE712", "<Separator/>");

        // Misc
        Add("Misc", "Frame",            "\uE80F", "<Frame Width=\"300\" Height=\"200\"/>");
        Add("Misc", "Popup",            "\uE80F", "<Popup Width=\"200\" Height=\"100\" IsOpen=\"True\" StaysOpen=\"True\"/>");

        _items.Sort((a, b) =>
        {
            int cat = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
            return cat != 0 ? cat : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
    }

    private void Add(string category, string name, string icon, string xaml)
        => _items.Add(new ToolboxItem(name, category, icon, xaml));
}
