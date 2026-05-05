// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentEditorHost.Contributors.cs
// Description:
//     IStatusBarContributor and IEditorToolbarContributor implementations
//     for DocumentEditorHost.
//     Status bar: format, version, block count, selection offset, alerts, mode.
//     Toolbar: view-mode toggles, forensic toggle, save, export dropdown.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;
using WpfHexEditor.Editor.DocumentEditor.Properties;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentEditorHost : IStatusBarContributor, IEditorToolbarContributor
{
    // ── StatusBar ────────────────────────────────────────────────────────────

    private StatusBarItem? _sbFormat;
    private StatusBarItem? _sbVersion;
    private StatusBarItem? _sbBlockCount;
    private StatusBarItem? _sbSelection;
    private StatusBarItem? _sbAlerts;
    private StatusBarItem? _sbViewMode;

    private ObservableCollection<StatusBarItem>? _statusBarItems;
    public ObservableCollection<StatusBarItem> StatusBarItems =>
        _statusBarItems ??= BuildStatusBarItems();

    private ObservableCollection<StatusBarItem> BuildStatusBarItems()
    {
        _sbFormat     = new StatusBarItem { Label = DocumentEditorResources.DocSb_FormatLabel,    Value = "—",          IsVisible = true };
        _sbVersion    = new StatusBarItem { Label = DocumentEditorResources.DocSb_VersionLabel,   Value = string.Empty, IsVisible = false };
        _sbBlockCount = new StatusBarItem { Label = DocumentEditorResources.DocSb_BlocksLabel,    Value = "0 blocks",   IsVisible = true };
        _sbSelection  = new StatusBarItem { Label = DocumentEditorResources.DocSb_SelectionLabel, Value = string.Empty, IsVisible = false };
        _sbAlerts     = new StatusBarItem { Label = DocumentEditorResources.DocSb_AlertsLabel,    Value = string.Empty, IsVisible = false };
        _sbViewMode   = new StatusBarItem { Label = DocumentEditorResources.DocSb_ViewLabel,      Value = "Text",       IsVisible = true };

        return [_sbFormat, _sbVersion, _sbBlockCount, _sbSelection, _sbAlerts, _sbViewMode];
    }

    public void RefreshStatusBarItems()
    {
        if (_vm?.Model is null) return;
        var model = _vm.Model;

        var meta = model.Metadata;
        string ext = Path.GetExtension(model.FilePath)?.TrimStart('.').ToUpperInvariant() ?? "DOC";

        if (_sbFormat is not null)
            _sbFormat.Value = ext;

        if (_sbVersion is not null)
        {
            _sbVersion.Value     = string.IsNullOrEmpty(meta.FormatVersion) ? string.Empty : $"v{meta.FormatVersion}";
            _sbVersion.IsVisible = !string.IsNullOrEmpty(meta.FormatVersion);
        }

        if (_sbBlockCount is not null)
        {
            int count = model.Blocks.Sum(b => 1 + b.DescendantsAndSelf().Count() - 1);
            _sbBlockCount.Value = $"{count} blocks";
        }

        if (_sbAlerts is not null)
        {
            int alertCount = model.ForensicAlerts.Count;
            _sbAlerts.IsVisible = alertCount > 0;
            _sbAlerts.Value     = alertCount > 0 ? $"⚠ {alertCount} alert{(alertCount == 1 ? "" : "s")}" : string.Empty;
        }

        if (_sbViewMode is not null)
            _sbViewMode.Value = ViewMode.ToString();
    }

    // ── Selection status ─────────────────────────────────────────────────────

    internal void UpdateSelectionStatus(DocumentBlock? block)
    {
        if (_sbSelection is null) return;
        if (block is null)
        {
            _sbSelection.IsVisible = false;
            return;
        }
        _sbSelection.Value     = $"Block: {block.Kind}  |  0x{block.RawOffset:X}";
        _sbSelection.IsVisible = true;
    }

    // ── Toolbar ──────────────────────────────────────────────────────────────

    private ObservableCollection<EditorToolbarItem>? _toolbarItems;
    public ObservableCollection<EditorToolbarItem> ToolbarItems =>
        _toolbarItems ??= BuildToolbarItems();

    private ObservableCollection<EditorToolbarItem> BuildToolbarItems()
    {
        return
        [
            new EditorToolbarItem
            {
                Icon     = "\uE8A5",
                Tooltip  = DocumentEditorResources.DocTb_TextView,
                IsToggle = true,
                Command  = new RelayAction(() => ViewMode = DocumentViewMode.TextOnly)
            },
            new EditorToolbarItem
            {
                Icon     = "\uE7C9",
                Tooltip  = DocumentEditorResources.DocTb_StructureView,
                IsToggle = true,
                Command  = new RelayAction(() => ViewMode = DocumentViewMode.Structure)
            },
            new EditorToolbarItem { IsSeparator = true },
            new EditorToolbarItem
            {
                Icon     = "\uE7C3",
                Tooltip  = DocumentEditorResources.DocTb_ForensicMode,
                IsToggle = true,
                Command  = new RelayAction(() => IsForensicMode = !IsForensicMode)
            },
            new EditorToolbarItem { IsSeparator = true },
            new EditorToolbarItem
            {
                Icon    = "\uE74E",
                Tooltip = DocumentEditorResources.DocTb_Save,
                Command = new RelayAction(Save)
            }
        ];
    }
}

// ── Simple relay command ──────────────────────────────────────────────────────

file sealed class RelayAction(Action execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
