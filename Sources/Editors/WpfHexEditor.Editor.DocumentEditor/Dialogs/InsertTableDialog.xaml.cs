// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Dialogs/InsertTableDialog.xaml.cs
// Description: Modal dialog for inserting a table block.
//     Features an interactive 8×8 grid picker and manual text inputs.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfHexEditor.Editor.DocumentEditor.Dialogs;

public partial class InsertTableDialog : Window
{
    public int Rows    { get; private set; } = 3;
    public int Columns { get; private set; } = 3;

    // Current hover selection on the grid (1-based, 0 = none)
    private int _hoverRow = 3;
    private int _hoverCol = 3;

    private const int GridMax   = 8;
    private const int CellSize  = 24; // px per cell in the picker grid

    // Lazily cached cell rectangles [row, col] (0-based)
    private readonly Rectangle[,] _cells = new Rectangle[GridMax, GridMax];

    private static readonly Brush HoverFill   = new SolidColorBrush(Color.FromArgb(180, 65, 130, 215));
    private static readonly Brush EmptyFill   = new SolidColorBrush(Color.FromArgb(40,  120, 120, 120));
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromArgb(100, 80, 80, 80));

    static InsertTableDialog()
    {
        ((SolidColorBrush)HoverFill).Freeze();
        ((SolidColorBrush)EmptyFill).Freeze();
        ((SolidColorBrush)BorderBrush).Freeze();
    }

    public InsertTableDialog()
    {
        InitializeComponent();
        BuildGrid();
        UpdatePicker();
        PART_Rows.Focus();
    }

    private void BuildGrid()
    {
        var borderPen = new Pen(BorderBrush, 0.5);
        borderPen.Freeze();

        for (int r = 0; r < GridMax; r++)
        {
            for (int c = 0; c < GridMax; c++)
            {
                var rect = new Rectangle
                {
                    Width           = CellSize - 2,
                    Height          = CellSize - 2,
                    Margin          = new Thickness(1),
                    Stroke          = BorderBrush,
                    StrokeThickness = 0.5,
                    Fill            = EmptyFill,
                    Cursor          = Cursors.Hand,
                    Tag             = (r, c)
                };
                rect.MouseEnter     += OnCellMouseEnter;
                rect.MouseLeftButtonUp += OnCellClick;

                _cells[r, c] = rect;
                PART_GridPicker.Children.Add(rect);
            }
        }
    }

    private void OnCellMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Rectangle rect || rect.Tag is not ValueTuple<int,int> tag) return;
        _hoverRow = tag.Item1 + 1;
        _hoverCol = tag.Item2 + 1;
        UpdatePicker();
        SyncTextInputs();
    }

    private void OnCellClick(object sender, MouseButtonEventArgs e)
    {
        Rows         = _hoverRow;
        Columns      = _hoverCol;
        DialogResult = true;
    }

    private void OnPickerMouseLeave(object sender, MouseEventArgs e)
    {
        // Keep selection at last hover position — don't reset
    }

    private void UpdatePicker()
    {
        for (int r = 0; r < GridMax; r++)
            for (int c = 0; c < GridMax; c++)
                _cells[r, c].Fill = (r < _hoverRow && c < _hoverCol) ? HoverFill : EmptyFill;

        if (PART_DimLabel is not null)
            PART_DimLabel.Text = $"{_hoverRow} × {_hoverCol}";
    }

    private void SyncTextInputs()
    {
        PART_Rows.TextChanged    -= OnManualInputChanged;
        PART_Columns.TextChanged -= OnManualInputChanged;
        PART_Rows.Text    = _hoverRow.ToString();
        PART_Columns.Text = _hoverCol.ToString();
        PART_Rows.TextChanged    += OnManualInputChanged;
        PART_Columns.TextChanged += OnManualInputChanged;
    }

    private void OnManualInputChanged(object sender, TextChangedEventArgs e)
    {
        if (PART_Rows is null || PART_Columns is null) return;
        if (!int.TryParse(PART_Rows.Text,    out int r) || r < 1) return;
        if (!int.TryParse(PART_Columns.Text, out int c) || c < 1) return;
        _hoverRow = Math.Clamp(r, 1, GridMax);
        _hoverCol = Math.Clamp(c, 1, GridMax);
        UpdatePicker();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        Rows    = int.TryParse(PART_Rows.Text,    out int r) ? Math.Clamp(r, 1, 50) : _hoverRow;
        Columns = int.TryParse(PART_Columns.Text, out int c) ? Math.Clamp(c, 1, 20) : _hoverCol;
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}
