// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/GoToMetadataTokenDialog.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Code-behind-only modal dialog for Go-To-Metadata-Token (Ctrl+Shift+M).
//     Accepts a hex token (0x06000001) or a table+row shorthand (MethodDef #1),
//     displays a live preview label, and on OK selects + navigates to the token.
//
// Architecture Notes:
//     Pattern: MVVM-friendly dialog — thin code-behind, delegates to ViewModel.
//     No XAML file: all UI is built in the constructor (codebase convention).
//     Token parsing is self-contained so no Core dependency is added.
// ==========================================================

using System.Reflection.Metadata.Ecma335;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Go-To-Metadata-Token dialog (Ctrl+Shift+M).
/// Parses a token expressed as a hex integer or "TableName #Row" shorthand,
/// shows a live preview label, and navigates the tree + hex editor on OK.
/// </summary>
public sealed class GoToMetadataTokenDialog : Window
{
    // ── Known table name → ECMA-335 table index ───────────────────────────────
    private static readonly Dictionary<string, int> s_tableByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Module",          0x00 },
            { "TypeRef",         0x01 },
            { "TypeDef",         0x02 },
            { "Field",           0x04 },
            { "FieldDef",        0x04 },
            { "MethodDef",       0x06 },
            { "Method",          0x06 },
            { "Param",           0x08 },
            { "InterfaceImpl",   0x09 },
            { "MemberRef",       0x0A },
            { "Constant",        0x0B },
            { "CustomAttribute", 0x0C },
            { "FieldMarshal",    0x0D },
            { "DeclSecurity",    0x0E },
            { "ClassLayout",     0x0F },
            { "FieldLayout",     0x10 },
            { "StandAloneSig",   0x11 },
            { "EventMap",        0x12 },
            { "Event",           0x14 },
            { "PropertyMap",     0x15 },
            { "Property",        0x17 },
            { "MethodSemantics", 0x18 },
            { "MethodImpl",      0x19 },
            { "ModuleRef",       0x1A },
            { "TypeSpec",        0x1B },
            { "ImplMap",         0x1C },
            { "FieldRva",        0x1D },
            { "FieldRVA",        0x1D },
            { "Assembly",        0x20 },
            { "AssemblyRef",     0x23 },
            { "File",            0x26 },
            { "ExportedType",    0x27 },
            { "ManifestResource",0x28 },
            { "NestedClass",     0x29 },
            { "GenericParam",    0x2A },
            { "MethodSpec",      0x2B },
            { "GenericParamConstraint", 0x2C }
        };

    // ── Table index → friendly display name ───────────────────────────────────
    private static readonly Dictionary<int, string> s_nameByTable =
        new()
        {
            { 0x00, "Module" },          { 0x01, "TypeRef" },
            { 0x02, "TypeDef" },         { 0x04, "FieldDef" },
            { 0x06, "MethodDef" },       { 0x08, "Param" },
            { 0x09, "InterfaceImpl" },   { 0x0A, "MemberRef" },
            { 0x0B, "Constant" },        { 0x0C, "CustomAttribute" },
            { 0x0D, "FieldMarshal" },    { 0x0E, "DeclSecurity" },
            { 0x0F, "ClassLayout" },     { 0x10, "FieldLayout" },
            { 0x11, "StandAloneSig" },   { 0x12, "EventMap" },
            { 0x14, "Event" },           { 0x15, "PropertyMap" },
            { 0x17, "Property" },        { 0x18, "MethodSemantics" },
            { 0x19, "MethodImpl" },      { 0x1A, "ModuleRef" },
            { 0x1B, "TypeSpec" },        { 0x1C, "ImplMap" },
            { 0x1D, "FieldRVA" },        { 0x20, "Assembly" },
            { 0x23, "AssemblyRef" },     { 0x26, "File" },
            { 0x27, "ExportedType" },    { 0x28, "ManifestResource" },
            { 0x29, "NestedClass" },     { 0x2A, "GenericParam" },
            { 0x2B, "MethodSpec" },      { 0x2C, "GenericParamConstraint" }
        };

    private readonly AssemblyExplorerViewModel _vm;
    private readonly TextBox                   _inputBox;
    private readonly TextBlock                 _previewLabel;
    private readonly Button                    _okButton;

    private int? _parsedToken;

    public GoToMetadataTokenDialog(AssemblyExplorerViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));

        Title               = "Go To Metadata Token";
        Width               = 440;
        SizeToContent       = SizeToContent.Height;
        ResizeMode          = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar       = false;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Hint label ─────────────────────────────────────────────────────
        var hintLabel = new TextBlock
        {
            Text      = "Enter token (hex) or table name + row number:",
            Margin    = new Thickness(0, 0, 0, 6),
            FontSize  = 12
        };
        Grid.SetRow(hintLabel, 0);
        root.Children.Add(hintLabel);

        // ── Hint examples ──────────────────────────────────────────────────
        var examplesLabel = new TextBlock
        {
            Text       = "Examples:  0x06000001  |  MethodDef #1  |  TypeDef 3",
            Margin     = new Thickness(0, 0, 0, 10),
            FontSize   = 11,
            Foreground = Brushes.Gray
        };
        Grid.SetRow(examplesLabel, 1);
        root.Children.Add(examplesLabel);

        // ── Input text box ─────────────────────────────────────────────────
        _inputBox = new TextBox
        {
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize   = 14,
            Padding    = new Thickness(6, 4, 6, 4),
            Margin     = new Thickness(0, 0, 0, 8)
        };
        _inputBox.TextChanged += OnTextChanged;
        _inputBox.KeyDown     += OnKeyDown;
        Grid.SetRow(_inputBox, 2);
        root.Children.Add(_inputBox);

        // ── Preview label ──────────────────────────────────────────────────
        _previewLabel = new TextBlock
        {
            Margin   = new Thickness(0, 0, 0, 14),
            FontSize = 12,
            Text     = " "
        };
        Grid.SetRow(_previewLabel, 3);
        root.Children.Add(_previewLabel);

        // ── Buttons ────────────────────────────────────────────────────────
        var buttonRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 4, 0, 0)
        };

        _okButton = new Button
        {
            Content   = "Go To",
            Width     = 90,
            Height    = 28,
            Margin    = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            IsEnabled = false
        };
        _okButton.Click += OnOkClick;

        var cancelButton = new Button
        {
            Content    = "Cancel",
            Width      = 90,
            Height     = 28,
            IsCancel   = true
        };
        cancelButton.Click += (_, _) => DialogResult = false;

        buttonRow.Children.Add(_okButton);
        buttonRow.Children.Add(cancelButton);

        var rowDef = new RowDefinition { Height = GridLength.Auto };
        root.RowDefinitions.Add(rowDef);
        Grid.SetRow(buttonRow, 4);
        root.Children.Add(buttonRow);

        Content = root;
        Loaded  += (_, _) => _inputBox.Focus();
    }

    // ── Input handling ────────────────────────────────────────────────────────

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _parsedToken = TryParseInput(_inputBox.Text.Trim(), out var preview);
        _previewLabel.Text      = preview;
        _previewLabel.Foreground = _parsedToken.HasValue ? Brushes.LimeGreen : Brushes.OrangeRed;
        _okButton.IsEnabled     = _parsedToken.HasValue;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!_parsedToken.HasValue) return;

        _vm.SelectNode(_parsedToken.Value);
        _vm.NavigateToOffset(_parsedToken.Value);
        DialogResult = true;
    }

    // ── Token parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the user input as either a hex token or a "TableName #Row" shorthand.
    /// Returns the integer token on success, null on failure.
    /// Sets <paramref name="preview"/> to a human-readable description.
    /// </summary>
    private static int? TryParseInput(string input, out string preview)
    {
        preview = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return null;

        // ── Hex token: 0x06000001 ──────────────────────────────────────────
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(input[2..], System.Globalization.NumberStyles.HexNumber,
                    null, out var token))
            {
                return FormatTokenPreview(token, out preview);
            }
            preview = "Invalid hex value";
            return null;
        }

        // ── TableName [#]Row: MethodDef #1  or  TypeDef 3 ─────────────────
        var parts = input.Split([' ', '#', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2
            && s_tableByName.TryGetValue(parts[0], out var tableIdx)
            && int.TryParse(parts[^1].TrimStart('#'), out var rowNum)
            && rowNum >= 1)
        {
            var token = (tableIdx << 24) | rowNum;
            return FormatTokenPreview(token, out preview);
        }

        // ── Bare decimal integer ───────────────────────────────────────────
        if (int.TryParse(input, out var decToken) && decToken > 0)
            return FormatTokenPreview(decToken, out preview);

        preview = "Unrecognized format";
        return null;
    }

    private static int? FormatTokenPreview(int token, out string preview)
    {
        var tableIdx = (token >> 24) & 0xFF;
        var row      = token & 0x00FFFFFF;

        if (row == 0)
        {
            preview = "Row 0 is invalid";
            return null;
        }

        var tableName = s_nameByTable.TryGetValue(tableIdx, out var n)
            ? n
            : $"Table 0x{tableIdx:X2}";

        preview = $"Table: {tableName}   Row: {row}   Token: 0x{token:X8}";
        return token;
    }
}
