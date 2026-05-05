// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: ViewModels/WatchesPanelViewModel.cs
// Description: VM for the Watch panel â€” editable expression list + eval.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.App.Debug.ViewModels;

public sealed class WatchRow : ViewModelBase
{
    private string _expression = string.Empty;
    private string _value      = "â€”";
    private string? _type;

    public string Expression
    {
        get => _expression;
        set { _expression = value; OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public string? Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    public bool HasError { get; set; }

}

public sealed class WatchesPanelViewModel : ViewModelBase
{
    public ObservableCollection<WatchRow> Rows { get; } = [new WatchRow { Expression = "" }]; // trailing empty row

    public ICommand AddCommand     { get; }
    public ICommand RemoveCommand  { get; }
    public ICommand ClearCommand   { get; }
    public ICommand CopyCommand    { get; }

    public WatchesPanelViewModel(IDebuggerService _)
    {
        AddCommand    = new RelayCommand(_ => Rows.Insert(Rows.Count - 1, new WatchRow { Expression = "" }));
        RemoveCommand = new RelayCommand(p => { if (p is WatchRow row) Rows.Remove(row); });
        ClearCommand  = new RelayCommand(_ =>
        {
            Rows.Clear();
            Rows.Add(new WatchRow { Expression = "" });
        });
        CopyCommand   = new RelayCommand(p =>
        {
            if (p is WatchRow row && !string.IsNullOrEmpty(row.Value))
                System.Windows.Clipboard.SetText(row.Value);
        });
    }

    /// <summary>Append a new watch expression (used by Add to Watch from Locals/Autos).</summary>
    public void AddWatch(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return;
        if (Rows.Any(r => r.Expression == expression)) return;
        Rows.Insert(Rows.Count - 1, new WatchRow { Expression = expression });
    }

    public async Task RefreshAsync(IDebuggerService debugger)
    {
        foreach (var row in Rows.Where(r => !string.IsNullOrWhiteSpace(r.Expression)))
        {
            try
            {
                var result    = await debugger.EvaluateAsync(row.Expression);
                row.Value     = result;
                row.HasError  = false;
            }
            catch
            {
                row.Value    = "<error>";
                row.HasError = true;
            }
        }
    }

}
