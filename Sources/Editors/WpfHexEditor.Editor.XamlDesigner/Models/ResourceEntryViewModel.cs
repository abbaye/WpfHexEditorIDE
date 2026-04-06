// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceEntryViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-22 â€” Moved from ViewModels/ to Models/
//                        (used by ResourceScannerService and ResourceBrowserPanelViewModel).
// Description:
//     Domain model representing a single resource entry in the Resource Browser panel.
//     Holds the resource key, value type, scope, and a computed preview string.
//     Built by ResourceScannerService; consumed by ResourceBrowserPanelViewModel (plugin).
//
// Architecture Notes:
//     INPC record-like object.
//     Preview computed once at construction (potentially expensive for large values).
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// A single resource entry for display in the Resource Browser panel.
/// </summary>
public sealed class ResourceEntryViewModel : ViewModelBase
{
    private bool   _isSelected;
    private bool   _isEditing;
    private bool   _hasDuplicate;
    private int    _usageCount;
    private string _editKey = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceEntryViewModel(object key, object? value, string scope)
    {
        Key          = key?.ToString() ?? "(null)";
        Scope        = scope;
        ValueType    = value?.GetType().Name ?? "(null)";
        Value        = value;
        PreviewText  = BuildPreview(value);
        PreviewBrush = value is Brush b ? b : null;
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string   Key          { get; }
    public string   Scope        { get; }
    public string   ValueType    { get; }
    public object?  Value        { get; }
    public string   PreviewText  { get; }
    public Brush?   PreviewBrush { get; }

    /// <summary>Source line number in the XAML document (1-based). 0 when unknown.</summary>
    public int LineNumber { get; set; }

    /// <summary>Number of {StaticResource}/{DynamicResource} references to this key in the document.</summary>
    public int UsageCount
    {
        get => _usageCount;
        set { if (_usageCount == value) return; _usageCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsageLabel)); }
    }

    /// <summary>Human-readable usage label: "(1 usage)" / "(3 usages)" / "(unused)".</summary>
    public string UsageLabel => _usageCount switch
    {
        0 => "(unused)",
        1 => "(1 usage)",
        _ => $"({_usageCount} usages)"
    };

    /// <summary>True when another resource in the same scope has an identical value (duplicate detection).</summary>
    public bool HasDuplicate
    {
        get => _hasDuplicate;
        set { if (_hasDuplicate == value) return; _hasDuplicate = value; OnPropertyChanged(); }
    }

    /// <summary>True while the user is inline-editing the resource key.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing == value) return; _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>Temporary key during inline rename. Commit via CommitRename().</summary>
    public string EditKey
    {
        get => _editKey;
        set { _editKey = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>Begins inline renaming; seeds EditKey with the current Key.</summary>
    public void BeginRename()
    {
        EditKey   = Key;
        IsEditing = true;
    }

    /// <summary>Commits the rename; returns new key or null if unchanged/empty.</summary>
    public string? CommitRename()
    {
        IsEditing = false;
        var newKey = EditKey.Trim();
        return string.IsNullOrEmpty(newKey) || newKey == Key ? null : newKey;
    }

    // ── INPC ──────────────────────────────────────────────────────────────────



    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildPreview(object? value)
    {
        return value switch
        {
            null                 => "(null)",
            SolidColorBrush scb  => $"#{scb.Color.A:X2}{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}",
            System.Windows.Media.Color c
                                 => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
            string s             => $"\"{s}\"",
            double d             => d.ToString("G4"),
            System.Windows.Thickness t => $"{t.Left},{t.Top},{t.Right},{t.Bottom}",
            _                    => value.ToString() ?? string.Empty
        };
    }
}
