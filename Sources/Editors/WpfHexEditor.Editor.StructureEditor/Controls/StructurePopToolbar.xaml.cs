//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/StructurePopToolbar.xaml.cs
// Description: Code-behind for floating popup toolbar.
//////////////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

public sealed partial class StructurePopToolbar : UserControl
{
    public StructurePopToolbar() => InitializeComponent();

    // ── Events (raised to parent) ────────────────────────────────────────────

    public event EventHandler? SaveRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? UndoRequested;
    public event EventHandler? RedoRequested;
    public event EventHandler? AddBlockRequested;
    public event EventHandler? DuplicateRequested;
    public event EventHandler? ToggleCodeViewRequested;

    // ── Block operations visibility ──────────────────────────────────────────

    public void SetBlockOperationsVisible(bool visible)
    {
        var vis = visible ? Visibility.Visible : Visibility.Collapsed;
        PART_BlockSep.Visibility    = vis;
        PART_AddBlockBtn.Visibility = vis;
        PART_DuplicateBtn.Visibility = vis;
    }

    public void UpdateButtonStates(bool canSave, bool canUndo, bool canRedo, bool canDuplicate)
    {
        PART_SaveBtn.IsEnabled      = canSave;
        PART_UndoBtn.IsEnabled      = canUndo;
        PART_RedoBtn.IsEnabled      = canRedo;
        PART_DuplicateBtn.IsEnabled = canDuplicate;
    }

    // ── Click handlers ───────────────────────────────────────────────────────

    private void OnSaveClicked(object s, RoutedEventArgs e)           => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void OnValidateClicked(object s, RoutedEventArgs e)       => ValidateRequested?.Invoke(this, EventArgs.Empty);
    private void OnUndoClicked(object s, RoutedEventArgs e)           => UndoRequested?.Invoke(this, EventArgs.Empty);
    private void OnRedoClicked(object s, RoutedEventArgs e)           => RedoRequested?.Invoke(this, EventArgs.Empty);
    private void OnAddBlockClicked(object s, RoutedEventArgs e)       => AddBlockRequested?.Invoke(this, EventArgs.Empty);
    private void OnDuplicateClicked(object s, RoutedEventArgs e)      => DuplicateRequested?.Invoke(this, EventArgs.Empty);
    private void OnToggleCodeViewClicked(object s, RoutedEventArgs e) => ToggleCodeViewRequested?.Invoke(this, EventArgs.Empty);
}
