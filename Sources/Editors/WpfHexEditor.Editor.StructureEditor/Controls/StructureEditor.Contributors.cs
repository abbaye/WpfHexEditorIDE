//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/StructureEditor.Contributors.cs
// Description: IEditorToolbarContributor + IStatusBarContributor + IDiagnosticSource implementation.
//              Provides contextual toolbar buttons and status bar items to the IDE shell.
//              Publishes validation diagnostics to the IDE Error List panel.
//////////////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Validation;
using WpfHexEditor.Editor.StructureEditor.Properties;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

public sealed partial class StructureEditor : IEditorToolbarContributor, IStatusBarContributor, IDiagnosticSource
{
    // ── Toolbar ──────────────────────────────────────────────────────────────
    // NOTE: Save, Undo, Redo are global IDE commands (Ctrl+S/Z/Y delegate to
    //       ActiveDocumentEditor). Only domain-specific actions belong here.

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];

    private EditorToolbarItem? _tbAddBlock;
    private EditorToolbarItem? _tbCodeView;
    private EditorToolbarItem? _tbLayout;

    // Tracks whether the live code view split pane is open
    private bool   _codeViewVisible = true;
    private string _codeViewDock = "Right"; // "Left" | "Right" | "Top" | "Bottom"

    private void InitToolbarItems()
    {
        var tbValidate = new EditorToolbarItem
        {
            Icon    = "\uE73E",
            Tooltip = StructureEditorResources.StrTb_Validate,
            Command = new ViewModels.RelayCommand(() => _vm.TriggerValidationNow()),
        };
        _tbAddBlock = new EditorToolbarItem
        {
            Icon      = "\uE710",
            Tooltip   = StructureEditorResources.StrTb_AddBlock,
            Command   = new ViewModels.RelayCommand(() => BlocksTabCtrl.RequestAddBlock()),
            IsEnabled = false,
        };
        _tbCodeView = new EditorToolbarItem
        {
            Icon    = "\uE943",
            Tooltip = StructureEditorResources.StrTb_ToggleLiveCode,
            Command = new ViewModels.RelayCommand(ToggleCodeView),
        };

        var layoutItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = StructureEditorResources.StrLayout_CodeRight,  Icon = "\uE8A0", Command = new ViewModels.RelayCommand(() => ApplyCodeViewDock("Right"))  },
            new() { Label = StructureEditorResources.StrLayout_CodeLeft,   Icon = "\uE8A0", Command = new ViewModels.RelayCommand(() => ApplyCodeViewDock("Left"))   },
            new() { Label = StructureEditorResources.StrLayout_CodeBottom, Icon = "\uE8A0", Command = new ViewModels.RelayCommand(() => ApplyCodeViewDock("Bottom")) },
            new() { Label = StructureEditorResources.StrLayout_CodeTop,    Icon = "\uE8A0", Command = new ViewModels.RelayCommand(() => ApplyCodeViewDock("Top"))    },
        };
        _tbLayout = new EditorToolbarItem
        {
            Icon          = "\uF57E",
            Label         = StructureEditorResources.StrTb_LayoutLabel,
            Tooltip       = StructureEditorResources.StrTb_LayoutTooltip,
            IsEnabled     = false,   // enabled only when code view is visible
            DropdownItems = layoutItems,
        };

        ToolbarItems.Add(tbValidate);
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(_tbAddBlock);
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(_tbCodeView);
        ToolbarItems.Add(_tbLayout);
    }

    private void UpdateToolbarState()
    {
        if (_tbAddBlock is not null)
            _tbAddBlock.IsEnabled = MainTabs.SelectedIndex == 2; // Blocks tab
    }

    private void ToggleCodeView()
    {
        _codeViewVisible = !_codeViewVisible;
        if (_tbLayout is not null) _tbLayout.IsEnabled = _codeViewVisible;

        if (_codeViewVisible)
        {
            ApplyCodeViewDock(_codeViewDock);
            PushJsonToCodeView();
        }
        else
        {
            CollapseCodeView();
        }
    }

    /// <summary>
    /// Reconfigures the split Grid for the requested dock position.
    /// The SplitGrid starts life as a 3-column grid (Right layout).
    /// For Top/Bottom we switch to a 3-row grid; for Left we reorder columns.
    /// </summary>
    internal void ApplyCodeViewDock(string dock)
    {
        _codeViewDock = dock;
        if (!_codeViewVisible) return;

        var star    = new GridLength(1, GridUnitType.Star);
        var splSize = new GridLength(4);
        var zero    = new GridLength(0);

        // Reset both axes to zero first
        SplitGrid.ColumnDefinitions.Clear();
        SplitGrid.RowDefinitions.Clear();

        switch (dock)
        {
            case "Right":
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = star, MinWidth = 260 });
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = splSize });
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = star });

                Grid.SetColumn(MainTabs,        0); Grid.SetRow(MainTabs,        0);
                Grid.SetColumn(SplitSplitter,   1); Grid.SetRow(SplitSplitter,   0);
                Grid.SetColumn(CodeViewBorder,  2); Grid.SetRow(CodeViewBorder,  0);

                SplitSplitter.Width            = 4;
                SplitSplitter.Height           = double.NaN;
                SplitSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                SplitSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                SplitSplitter.ResizeDirection  = GridResizeDirection.Columns;
                break;

            case "Left":
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = star });
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = splSize });
                SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = star, MinWidth = 260 });

                Grid.SetColumn(CodeViewBorder,  0); Grid.SetRow(CodeViewBorder,  0);
                Grid.SetColumn(SplitSplitter,   1); Grid.SetRow(SplitSplitter,   0);
                Grid.SetColumn(MainTabs,        2); Grid.SetRow(MainTabs,        0);

                SplitSplitter.Width            = 4;
                SplitSplitter.Height           = double.NaN;
                SplitSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                SplitSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                SplitSplitter.ResizeDirection  = GridResizeDirection.Columns;
                break;

            case "Top":
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = star });
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = splSize });
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = star, MinHeight = 120 });

                Grid.SetRow(CodeViewBorder,  0); Grid.SetColumn(CodeViewBorder,  0);
                Grid.SetRow(SplitSplitter,   1); Grid.SetColumn(SplitSplitter,   0);
                Grid.SetRow(MainTabs,        2); Grid.SetColumn(MainTabs,        0);

                SplitSplitter.Height           = 4;
                SplitSplitter.Width            = double.NaN;
                SplitSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                SplitSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                SplitSplitter.ResizeDirection  = GridResizeDirection.Rows;
                break;

            case "Bottom":
            default:
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = star, MinHeight = 120 });
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = splSize });
                SplitGrid.RowDefinitions.Add(new RowDefinition { Height = star });

                Grid.SetRow(MainTabs,        0); Grid.SetColumn(MainTabs,        0);
                Grid.SetRow(SplitSplitter,   1); Grid.SetColumn(SplitSplitter,   0);
                Grid.SetRow(CodeViewBorder,  2); Grid.SetColumn(CodeViewBorder,  0);

                SplitSplitter.Height           = 4;
                SplitSplitter.Width            = double.NaN;
                SplitSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                SplitSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                SplitSplitter.ResizeDirection  = GridResizeDirection.Rows;
                break;
        }

        // Overlays (pop-toolbar trigger + popup + dirty indicator) always stay in the TabControl cell
        var tabCol = dock == "Left" ? 2 : 0;
        var tabRow = (dock == "Top" || dock == "Bottom") ? (dock == "Top" ? 2 : 0) : 0;
        Grid.SetColumn(PopToolbarTrigger, tabCol); Grid.SetRow(PopToolbarTrigger, tabRow);
        Grid.SetColumn(PopToolbarPopup,   tabCol); Grid.SetRow(PopToolbarPopup,   tabRow);
        Grid.SetColumn(DirtyIndicator,    tabCol); Grid.SetRow(DirtyIndicator,    tabRow);
    }

    private void CollapseCodeView()
    {
        // Restore original 3-column layout (collapsed)
        SplitGrid.RowDefinitions.Clear();
        SplitGrid.ColumnDefinitions.Clear();
        SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        SplitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Grid.SetColumn(MainTabs,        0); Grid.SetRow(MainTabs,        0);
        Grid.SetColumn(SplitSplitter,   1); Grid.SetRow(SplitSplitter,   0);
        Grid.SetColumn(CodeViewBorder,  2); Grid.SetRow(CodeViewBorder,  0);
        Grid.SetColumn(PopToolbarTrigger, 0); Grid.SetRow(PopToolbarTrigger, 0);
        Grid.SetColumn(PopToolbarPopup,   0); Grid.SetRow(PopToolbarPopup,   0);
        Grid.SetColumn(DirtyIndicator,    0); Grid.SetRow(DirtyIndicator,    0);
    }

    // ── Diagnostics (IDE Error List) ─────────────────────────────────────────

    private List<DiagnosticEntry> _diagnostics = [];

    public IReadOnlyList<DiagnosticEntry> GetDiagnostics() => _diagnostics;

    public event EventHandler? DiagnosticsChanged;

    public string SourceLabel => "WHFMT";

    private void PublishDiagnostics()
    {
        var fileName = string.IsNullOrEmpty(_filePath) ? null : Path.GetFileName(_filePath);
        _diagnostics = _vm.ValidationSummary
            .Select(v => new DiagnosticEntry(
                Severity    : v.Severity == ValidationSeverity.Error
                                  ? DiagnosticSeverity.Error
                                  : DiagnosticSeverity.Warning,
                Code        : "WHFMT",
                Description : v.Message,
                ProjectName : "Format Definitions",
                FileName    : fileName,
                FilePath    : _filePath,
                Line        : v.Line,
                Column      : v.Column))
            .ToList();
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearDiagnostics()
    {
        _diagnostics = [];
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Status Bar ───────────────────────────────────────────────────────────

    public ObservableCollection<StatusBarItem> StatusBarItems { get; } = [];

    private StatusBarItem? _sbFormat;
    private StatusBarItem? _sbTab;
    private StatusBarItem? _sbBlocks;
    private StatusBarItem? _sbValidation;
    private StatusBarItem? _sbDirty;

    private void InitStatusBarItems()
    {
        _sbFormat     = new StatusBarItem { Label = StructureEditorResources.StrSb_FormatLabel,     Value = "WHFMT" };
        _sbTab        = new StatusBarItem { Label = StructureEditorResources.StrSb_TabLabel,        Value = "Metadata" };
        _sbBlocks     = new StatusBarItem { Label = StructureEditorResources.StrSb_BlocksLabel,     Value = "0" };
        _sbValidation = new StatusBarItem { Label = StructureEditorResources.StrSb_ValidationLabel, Value = "—" };
        _sbDirty = new StatusBarItem { Label = "", Value = "", IsVisible = false };

        StatusBarItems.Add(_sbFormat);
        StatusBarItems.Add(_sbTab);
        StatusBarItems.Add(_sbBlocks);
        StatusBarItems.Add(_sbValidation);
        StatusBarItems.Add(_sbDirty);
    }

    public void RefreshStatusBarItems()
    {
        if (_sbTab is not null)
            _sbTab.Value = MainTabs.SelectedItem is System.Windows.Controls.TabItem ti
                ? ti.Header?.ToString() ?? "" : "";

        if (_sbBlocks is not null)
            _sbBlocks.Value = $"{_vm.Blocks.BlockTree.Count}";

        UpdateValidationStatus();
        UpdateDirtyStatus();
    }

    private void UpdateValidationStatus()
    {
        if (_sbValidation is null) return;
        _sbValidation.Value = _vm.ErrorCount > 0
            ? $"{_vm.ErrorCount} error(s), {_vm.WarningCount} warning(s)"
            : _vm.WarningCount > 0
                ? $"{_vm.WarningCount} warning(s)"
                : "Valid";
    }

    private void UpdateDirtyStatus()
    {
        if (_sbDirty is null) return;
        _sbDirty.Value = _vm.IsDirty ? "Modified" : "";
        _sbDirty.IsVisible = _vm.IsDirty;
    }
}
