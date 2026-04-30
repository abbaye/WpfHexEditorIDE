// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: FileComparisonPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for FileComparisonPanel â€” tracks the two file paths,
//     diff statistics, and comparison status.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.FileComparison.Properties;

namespace WpfHexEditor.Plugins.FileComparison.ViewModels;

public sealed class FileComparisonPanelViewModel : ViewModelBase
{
    private string _file1Name   = "No file selected";
    private string _file2Name   = "No file selected";
    private string _statusText  = FileComparisonResources.FileComparison_SelectFiles;
    private int    _matching;
    private int    _added;
    private int    _modified;
    private int    _removed;
    private bool   _isComparing;

    public string File1Name   { get => _file1Name;   set => SetField(ref _file1Name, value); }
    public string File2Name   { get => _file2Name;   set => SetField(ref _file2Name, value); }
    public string StatusText  { get => _statusText;  set => SetField(ref _statusText, value); }
    public int    Matching    { get => _matching;    set => SetField(ref _matching, value); }
    public int    Added       { get => _added;       set => SetField(ref _added, value); }
    public int    Modified    { get => _modified;    set => SetField(ref _modified, value); }
    public int    Removed     { get => _removed;     set => SetField(ref _removed, value); }
    public bool   IsComparing { get => _isComparing; set => SetField(ref _isComparing, value); }

    public void Clear()
    {
        File1Name  = File2Name = "No file selected";
        StatusText = FileComparisonResources.FileComparison_SelectFiles;
        Matching   = Added = Modified = Removed = 0;
    }


}
