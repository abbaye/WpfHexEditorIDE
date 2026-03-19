// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignDataSource.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Describes a design-time data source — a type name and optional
//     sample data instance for the XAML design canvas DataContext.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>A registered design-time data source.</summary>
/// <param name="TypeName">Short or fully-qualified CLR type name.</param>
/// <param name="Instance">Pre-built sample instance, or null if instantiated on demand.</param>
/// <param name="IsAutoDetected">True if the source was extracted from d:DesignInstance.</param>
public sealed record DesignDataSource(
    string  TypeName,
    object? Instance,
    bool    IsAutoDetected = false);
