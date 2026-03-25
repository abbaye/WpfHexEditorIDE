// ==========================================================
// Project: WpfHexEditor.App
// File: Options/LspServersOptionsPage.cs
// Description:
//     Options page for configuring Language Server Protocol (LSP) server entries.
//     Provides Add / Remove / Browse commands and persists entries via ILspServerRegistry.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML) registered via OptionsPageRegistry.RegisterDynamic.
//     Receives ILspServerRegistry via constructor injection from MainWindow.PluginSystem.cs.
//     IOptionsPage.Load/Flush are no-ops — the page drives the registry directly.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE Options page — Language Server Protocol › Servers.
/// Lets the user configure which LSP executable handles which language / extensions.
/// </summary>
public sealed class LspServersOptionsPage : UserControl, IOptionsPage
{
    // ── Dependencies ────────────────────────────────────────────────────────────

    private readonly ILspServerRegistry _registry;

    // ── UI elements ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<LspServerRow> _rows = [];
    private readonly DataGrid                           _grid;

    // ── IOptionsPage ────────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    public void Load(AppSettings settings)
    {
        _rows.Clear();
        foreach (var entry in _registry.Entries)
            _rows.Add(new LspServerRow(entry));
    }

    public void Flush(AppSettings settings)
    {
        // Entries are persisted immediately on Add/Remove — nothing to flush.
    }

    // ── Construction ────────────────────────────────────────────────────────────

    public LspServersOptionsPage(ILspServerRegistry registry)
    {
        _registry = registry;

        // Merge DialogStyles locally so KSP_* keys survive ApplyTheme() clearing App resources.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WpfHexEditor.App;component/Themes/DialogStyles.xaml")
        });

        // ── Toolbar ────────────────────────────────────────────────────────────
        var addBtn    = MakeButton("Add",    OnAdd);
        var removeBtn = MakeButton("Remove", OnRemove);
        var browseBtn = MakeButton("Browse", OnBrowse);
        var toolbar   = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 0, 0, 6),
        };
        toolbar.Children.Add(addBtn);
        toolbar.Children.Add(removeBtn);
        toolbar.Children.Add(browseBtn);

        // ── DataGrid ───────────────────────────────────────────────────────────
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            SelectionMode       = DataGridSelectionMode.Single,
            ItemsSource         = _rows,
        };

        _grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header  = "Enabled",
            Binding = new Binding(nameof(LspServerRow.IsEnabled)) { Mode = BindingMode.TwoWay },
            Width   = new DataGridLength(60),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Language ID",
            Binding = new Binding(nameof(LspServerRow.LanguageId)) { Mode = BindingMode.TwoWay },
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Extensions",
            Binding = new Binding(nameof(LspServerRow.Extensions)) { Mode = BindingMode.TwoWay },
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Executable",
            Binding = new Binding(nameof(LspServerRow.ExecutablePath)) { Mode = BindingMode.TwoWay },
            Width   = new DataGridLength(2, DataGridLengthUnitType.Star),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Arguments",
            Binding = new Binding(nameof(LspServerRow.Arguments)) { Mode = BindingMode.TwoWay },
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
        });

        _grid.SetResourceReference(DataGrid.StyleProperty,    "KSP_DataGridStyle");
        _grid.SetResourceReference(DataGrid.RowStyleProperty, "KSP_DataGridRowStyle");
        _grid.ClearValue(DataGrid.RowBackgroundProperty);

        _grid.CellEditEnding += OnCellEditEnding;

        // ── Root layout ────────────────────────────────────────────────────────
        var root = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(_grid);

        Content = root;
        Load(null!); // populate immediately
    }

    // ── Command handlers ────────────────────────────────────────────────────────

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var row = new LspServerRow(new LspServerEntry
        {
            LanguageId     = "new-language",
            FileExtensions = Array.Empty<string>(),
            ExecutablePath = string.Empty,
            IsEnabled      = true,
        });
        _rows.Add(row);
        _grid.SelectedItem = row;
        _grid.ScrollIntoView(row);
        PersistAll();
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is not LspServerRow row) return;
        _registry.Unregister(row.LanguageId);
        _rows.Remove(row);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is not LspServerRow row) return;

        var dlg = new OpenFileDialog
        {
            Title  = "Select LSP server executable",
            Filter = "Executable|*.exe;*.cmd;*.bat|All files|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        row.ExecutablePath = dlg.FileName;
        PersistAll();
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        => PersistAll();

    // ── Persistence ─────────────────────────────────────────────────────────────

    private void PersistAll()
    {
        foreach (var row in _rows)
            _registry.Register(row.ToEntry());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static Button MakeButton(string label, RoutedEventHandler handler)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(10, 4, 10, 4),
            Margin  = new Thickness(0, 0, 4, 0),
        };
        btn.Click += handler;
        return btn;
    }

    // ── View-model row ───────────────────────────────────────────────────────────

    /// <summary>Mutable view-model row bound to the DataGrid.</summary>
    private sealed class LspServerRow(LspServerEntry entry)
        : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private bool   _isEnabled      = entry.IsEnabled;
        private string _languageId     = entry.LanguageId;
        private string _extensions     = string.Join(", ", entry.FileExtensions);
        private string _executablePath = entry.ExecutablePath;
        private string _arguments      = entry.Arguments ?? string.Empty;

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); }
        }

        public string LanguageId
        {
            get => _languageId;
            set { _languageId = value; PropertyChanged?.Invoke(this, new(nameof(LanguageId))); }
        }

        public string Extensions
        {
            get => _extensions;
            set { _extensions = value; PropertyChanged?.Invoke(this, new(nameof(Extensions))); }
        }

        public string ExecutablePath
        {
            get => _executablePath;
            set { _executablePath = value; PropertyChanged?.Invoke(this, new(nameof(ExecutablePath))); }
        }

        public string Arguments
        {
            get => _arguments;
            set { _arguments = value; PropertyChanged?.Invoke(this, new(nameof(Arguments))); }
        }

        public LspServerEntry ToEntry() => new()
        {
            LanguageId     = _languageId.Trim(),
            FileExtensions = ParseExtensions(_extensions),
            ExecutablePath = _executablePath.Trim(),
            Arguments      = string.IsNullOrWhiteSpace(_arguments) ? null : _arguments.Trim(),
            IsEnabled      = _isEnabled,
        };

        private static IReadOnlyList<string> ParseExtensions(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
