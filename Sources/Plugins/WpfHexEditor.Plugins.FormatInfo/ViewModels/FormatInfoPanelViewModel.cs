// ==========================================================
// Project: WpfHexEditor.Plugins.FormatInfo
// File: FormatInfoPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for EnrichedFormatInfoPanel — wraps EnrichedFormatViewModel
//     and exposes display state flags used by the panel code-behind.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.Plugins.FormatInfo.ViewModels;

public sealed class FormatInfoPanelViewModel : INotifyPropertyChanged
{
    private bool _hasFormat;
    private readonly EnrichedFormatViewModel _inner = new();

    /// <summary>Underlying enriched format data.</summary>
    public EnrichedFormatViewModel Inner => _inner;

    /// <summary>True when a format has been set and the info cards should be visible.</summary>
    public bool HasFormat
    {
        get => _hasFormat;
        set => SetField(ref _hasFormat, value);
    }

    public void Clear()
    {
        _inner.ClearData();
        HasFormat = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
