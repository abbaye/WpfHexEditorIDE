// ==========================================================
// Project: WpfHexEditor.Plugins.StructureOverlay
// File: StructureOverlayPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for StructureOverlayPanel â€” exposes overlay structures,
//     selected field, and toolbar-bound commands.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.Models.StructureOverlay;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.StructureOverlay.ViewModels;

public sealed class StructureOverlayPanelViewModel : ViewModelBase
{
    private ObservableCollection<OverlayStructure> _structures = new();
    private OverlayField?   _selectedField;
    private OverlayStructure? _selectedStructure;
    private string          _statusText  = "No structures loaded";
    private bool            _isLoading;

    public ObservableCollection<OverlayStructure> Structures
    {
        get => _structures;
        set => SetField(ref _structures, value);
    }

    public OverlayStructure? SelectedStructure { get => _selectedStructure; set => SetField(ref _selectedStructure, value); }
    public OverlayField?     SelectedField     { get => _selectedField;     set => SetField(ref _selectedField, value); }
    public string            StatusText        { get => _statusText;        set => SetField(ref _statusText, value); }
    public bool              IsLoading         { get => _isLoading;         set => SetField(ref _isLoading, value); }

    public void Clear()
    {
        Structures.Clear();
        SelectedField     = null;
        SelectedStructure = null;
        StatusText        = "No structures loaded";
    }


}
