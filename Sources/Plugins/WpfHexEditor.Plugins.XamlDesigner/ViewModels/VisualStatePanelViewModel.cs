// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: VisualStatePanelViewModel.cs
// Description:
//     ViewModel for the VisualStateManager dockable panel.
//     Parses VSM groups/states from XAML and exposes commands to
//     add/delete groups and states, and to preview a state's transition.
// Architecture Notes:
//     Plugin-owned panel ViewModel. Uses VisualStateManagerService from editor core.
//     StatePreviewRequested fires XAML for the plugin to play via AnimationPreviewService.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the VisualStateManager panel.
/// </summary>
public sealed class VisualStatePanelViewModel : ViewModelBase
{
    private readonly VisualStateManagerService _service = new();

    private string                    _xamlSource    = string.Empty;
    private VisualStateGroupModel?    _selectedGroup;
    private VisualStateEntryModel?    _selectedState;

    // ── Properties ────────────────────────────────────────────────────────────

    public ObservableCollection<VisualStateGroupModel>  Groups { get; } = new();
    public ObservableCollection<VisualStateEntryModel>  States { get; } = new();
    public ObservableCollection<VisualStateSetterModel> Setters { get; } = new();

    public VisualStateGroupModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup == value) return;
            _selectedGroup = value;
            OnPropertyChanged();
            RefreshStates();
        }
    }

    public VisualStateEntryModel? SelectedState
    {
        get => _selectedState;
        set
        {
            if (_selectedState == value) return;
            _selectedState = value;
            OnPropertyChanged();
            RefreshSetters();
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand PreviewStateCommand { get; }
    public ICommand AddGroupCommand     { get; }
    public ICommand DeleteGroupCommand  { get; }
    public ICommand AddStateCommand     { get; }
    public ICommand DeleteStateCommand  { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks Preview State. Arg is a Storyboard XAML snippet.</summary>
    public event EventHandler<string>? StatePreviewRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public VisualStatePanelViewModel()
    {
        PreviewStateCommand = new RelayCommand(_ => ExecutePreview(),  _ => _selectedState is not null);
        AddGroupCommand     = new RelayCommand(_ => ExecuteAddGroup());
        DeleteGroupCommand  = new RelayCommand(_ => ExecuteDeleteGroup(), _ => _selectedGroup is not null);
        AddStateCommand     = new RelayCommand(_ => ExecuteAddState(),    _ => _selectedGroup is not null);
        DeleteStateCommand  = new RelayCommand(_ => ExecuteDeleteState(), _ => _selectedState is not null);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetXamlSource(string xaml)
    {
        _xamlSource = xaml;
        RefreshGroups();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshGroups()
    {
        Groups.Clear();
        foreach (var g in _service.Parse(_xamlSource))
            Groups.Add(g);

        if (_selectedGroup is not null && !Groups.Contains(_selectedGroup))
            SelectedGroup = null;
        else
            RefreshStates();
    }

    private void RefreshStates()
    {
        States.Clear();
        if (_selectedGroup is null) return;
        foreach (var s in _selectedGroup.States)
            States.Add(s);

        if (_selectedState is not null && !States.Contains(_selectedState))
            SelectedState = null;
        else
            RefreshSetters();
    }

    private void RefreshSetters()
    {
        Setters.Clear();
        if (_selectedState is null) return;
        foreach (var s in _selectedState.Setters)
            Setters.Add(s);
    }

    private void ExecutePreview()
    {
        if (_selectedState is null || _selectedState.Setters.IsEmpty) return;

        // Build a minimal Storyboard XAML snippet for AnimationPreviewService.
        var ns  = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var xns = "http://schemas.microsoft.com/winfx/2006/xaml";
        var sb  = new System.Text.StringBuilder();
        sb.AppendLine($"<Storyboard xmlns=\"{ns}\" xmlns:x=\"{xns}\">");
        foreach (var setter in _selectedState.Setters)
        {
            sb.AppendLine(
                $"  <ObjectAnimationUsingKeyFrames " +
                $"Storyboard.TargetName=\"{setter.TargetName}\" " +
                $"Storyboard.TargetProperty=\"{setter.PropertyName}\">" +
                $"<DiscreteObjectKeyFrame KeyTime=\"0:0:0\" Value=\"{setter.Value}\"/>" +
                $"</ObjectAnimationUsingKeyFrames>");
        }
        sb.AppendLine("</Storyboard>");

        StatePreviewRequested?.Invoke(this, sb.ToString());
    }

    private void ExecuteAddGroup()
    {
        var model = new VisualStateGroupModel($"StateGroup{Groups.Count + 1}", []);
        Groups.Add(model);
        SelectedGroup = model;
    }

    private void ExecuteDeleteGroup()
    {
        if (_selectedGroup is null) return;
        Groups.Remove(_selectedGroup);
        SelectedGroup = null;
    }

    private void ExecuteAddState()
    {
        if (_selectedGroup is null) return;
        var newState  = new VisualStateEntryModel($"State{States.Count + 1}", []);
        var newStates = _selectedGroup.States.Add(newState);
        var updated   = _selectedGroup with { States = newStates };
        int idx = Groups.IndexOf(_selectedGroup);
        if (idx >= 0) Groups[idx] = updated;
        SelectedGroup = updated;
        SelectedState = newState;
    }

    private void ExecuteDeleteState()
    {
        if (_selectedState is null || _selectedGroup is null) return;
        var newStates = _selectedGroup.States.Remove(_selectedState);
        var updated   = _selectedGroup with { States = newStates };
        int idx = Groups.IndexOf(_selectedGroup);
        if (idx >= 0) Groups[idx] = updated;
        SelectedGroup = updated;
        SelectedState = null;
    }
}
