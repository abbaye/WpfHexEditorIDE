// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for DataInspectorPanel — exposes byte-interpretation
//     fields updated when the active hex-editor selection changes.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.DataInspector.ViewModels;

public sealed class DataInspectorPanelViewModel : INotifyPropertyChanged
{
    private long   _position;
    private string _int8Value   = string.Empty;
    private string _uint8Value  = string.Empty;
    private string _int16Value  = string.Empty;
    private string _uint16Value = string.Empty;
    private string _int32Value  = string.Empty;
    private string _uint32Value = string.Empty;
    private string _int64Value  = string.Empty;
    private string _uint64Value = string.Empty;
    private string _floatValue  = string.Empty;
    private string _doubleValue = string.Empty;
    private string _hexValue    = string.Empty;
    private string _binaryValue = string.Empty;
    private string _asciiValue  = string.Empty;
    private string _utf8Value   = string.Empty;
    private string _utf16Value  = string.Empty;
    private bool   _isLittleEndian = true;

    public long   Position     { get => _position;     set => SetField(ref _position, value); }
    public string Int8Value    { get => _int8Value;    set => SetField(ref _int8Value, value); }
    public string UInt8Value   { get => _uint8Value;   set => SetField(ref _uint8Value, value); }
    public string Int16Value   { get => _int16Value;   set => SetField(ref _int16Value, value); }
    public string UInt16Value  { get => _uint16Value;  set => SetField(ref _uint16Value, value); }
    public string Int32Value   { get => _int32Value;   set => SetField(ref _int32Value, value); }
    public string UInt32Value  { get => _uint32Value;  set => SetField(ref _uint32Value, value); }
    public string Int64Value   { get => _int64Value;   set => SetField(ref _int64Value, value); }
    public string UInt64Value  { get => _uint64Value;  set => SetField(ref _uint64Value, value); }
    public string FloatValue   { get => _floatValue;   set => SetField(ref _floatValue, value); }
    public string DoubleValue  { get => _doubleValue;  set => SetField(ref _doubleValue, value); }
    public string HexValue     { get => _hexValue;     set => SetField(ref _hexValue, value); }
    public string BinaryValue  { get => _binaryValue;  set => SetField(ref _binaryValue, value); }
    public string AsciiValue   { get => _asciiValue;   set => SetField(ref _asciiValue, value); }
    public string Utf8Value    { get => _utf8Value;    set => SetField(ref _utf8Value, value); }
    public string Utf16Value   { get => _utf16Value;   set => SetField(ref _utf16Value, value); }
    public bool   IsLittleEndian { get => _isLittleEndian; set => SetField(ref _isLittleEndian, value); }

    public void Clear()
    {
        var empty = string.Empty;
        Int8Value = UInt8Value = Int16Value = UInt16Value = empty;
        Int32Value = UInt32Value = Int64Value = UInt64Value = empty;
        FloatValue = DoubleValue = HexValue = BinaryValue = empty;
        AsciiValue = Utf8Value = Utf16Value = empty;
        Position = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
