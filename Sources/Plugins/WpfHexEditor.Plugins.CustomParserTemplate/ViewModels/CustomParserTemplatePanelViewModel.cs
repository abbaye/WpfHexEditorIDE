// ==========================================================
// Project: WpfHexEditor.Plugins.CustomParserTemplate
// File: CustomParserTemplatePanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for CustomParserTemplatePanel â€” exposes the template
//     list, selected template, and editor state.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.CustomParserTemplate.Properties;
using WpfHexEditor.Plugins.CustomParserTemplate.Views;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.CustomParserTemplate.ViewModels;

public sealed class CustomParserTemplatePanelViewModel : ViewModelBase
{
    private ObservableCollection<CustomTemplate> _templates = new();
    private CustomTemplate? _selectedTemplate;
    private string          _statusText = CustomParserTemplateResources.CustomParser_NoTemplateSelected;
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
        StatusText        = CustomParserTemplateResources.CustomParser_NoTemplateSelected;
    }


}
