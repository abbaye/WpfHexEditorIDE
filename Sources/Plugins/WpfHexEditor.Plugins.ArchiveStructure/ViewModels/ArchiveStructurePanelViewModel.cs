// ==========================================================
// Project: WpfHexEditor.Plugins.ArchiveStructure
// File: ArchiveStructurePanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for ArchiveStructurePanel — exposes archive stats
//     and the root node for the hierarchical tree view.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.ArchiveStructure.Views;

namespace WpfHexEditor.Plugins.ArchiveStructure.ViewModels;

public sealed class ArchiveStructurePanelViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ArchiveNode> _rootNodes = new();
    private string  _archiveInfo     = "No archive loaded";
    private string  _fileCount       = "0 files";
    private string  _folderCount     = "0 folders";
    private string  _totalSize       = "0 bytes";
    private string  _compressionRatio = "Ratio: N/A";
    private bool    _isLoading;

    public ObservableCollection<ArchiveNode> RootNodes
    {
        get => _rootNodes;
        set => SetField(ref _rootNodes, value);
    }

    public string ArchiveInfo      { get => _archiveInfo;      set => SetField(ref _archiveInfo, value); }
    public string FileCount        { get => _fileCount;        set => SetField(ref _fileCount, value); }
    public string FolderCount      { get => _folderCount;      set => SetField(ref _folderCount, value); }
    public string TotalSize        { get => _totalSize;        set => SetField(ref _totalSize, value); }
    public string CompressionRatio { get => _compressionRatio; set => SetField(ref _compressionRatio, value); }
    public bool   IsLoading        { get => _isLoading;        set => SetField(ref _isLoading, value); }

    public void Clear()
    {
        RootNodes.Clear();
        ArchiveInfo      = "No archive loaded";
        FileCount        = "0 files";
        FolderCount      = "0 folders";
        TotalSize        = "0 bytes";
        CompressionRatio = "Ratio: N/A";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
