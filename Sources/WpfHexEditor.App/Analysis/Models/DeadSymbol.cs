// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/DeadSymbol.cs
// Description: A symbol that is declared but never referenced.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public enum DeadSymbolKind { Class, Struct, Interface, Method, Field, Property, Parameter, Variable }

public sealed class DeadSymbol
{
    public string         Name        { get; init; } = string.Empty;
    public DeadSymbolKind Kind        { get; init; }
    public string         FilePath    { get; init; } = string.Empty;
    public int            Line        { get; init; }
    public string         ProjectName { get; init; } = string.Empty;
    public bool           IsInternal  { get; init; }
}
