// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/DocumentTabHeader.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Tab header control for document tabs.
//     Binds to DocumentModel and shows Title + dirty indicator (●)
//     when IsDirty is true, and lock icon when IsReadOnly is true.
//
// Architecture Notes:
//     Pattern: View — pure data-binding, no business logic.
//     Theme: inherits from parent DockTabItem; DockAccentBrush for dirty dot.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Tab header for document tabs. Bind <see cref="FrameworkElement.DataContext"/>
/// to a <see cref="WpfHexEditor.Editor.Core.Documents.DocumentModel"/> instance.
/// </summary>
public partial class DocumentTabHeader : UserControl
{
    public DocumentTabHeader() => InitializeComponent();
}
