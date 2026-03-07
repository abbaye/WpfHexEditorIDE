// ==========================================================
// Project: WpfHexEditor.Plugins.CustomParserTemplate
// File: CustomParserTemplatePanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for CustomParserTemplatePanel — exposes the template
//     list, selected template, and editor state.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.CustomParserTemplate.Views;

namespace WpfHexEditor.Plugins.CustomParserTemplate.ViewModels;

public sealed class CustomParserTemplatePanelViewModel : INotifyPropertyChanged
{
    private ObservableCollection<CustomTemplate> _templates = new();
    private CustomTemplate? _selectedTemplate;
    private string          _statusText = "No template selected";
    private bool            _hasUnsavedChanges;

    public ObservableCollection<CustomTemplate> Templates
    {
        get => _templates;
        set => SetField(ref _templates, value);
    }

    public CustomTemplate? SelectedTemplate   { get => _selectedTemplate;    set => SetField(ref _selectedTemplate, value); }
    public string          StatusText         { get => _statusText;          set => SetField(ref _statusText, value); }
    public bool            HasUnsavedChanges  { get => _hasUnsavedChanges;   set => SetField(ref _hasUnsavedChanges, value); }

    public void Clear()
    {
        Templates.Clear();
        SelectedTemplate  = null;
        HasUnsavedChanges = false;
        StatusText        = "No template selected";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
