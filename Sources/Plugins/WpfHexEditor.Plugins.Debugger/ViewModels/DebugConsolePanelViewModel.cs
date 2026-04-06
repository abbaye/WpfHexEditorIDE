// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/DebugConsolePanelViewModel.cs
// Description: VM for the Debug Console panel â€” output log + REPL input.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class DebugConsolePanelViewModel : ViewModelBase
{
    private readonly StringBuilder _buffer = new();
    private string _outputText = string.Empty;

    public string OutputText
    {
        get => _outputText;
        private set { _outputText = value; OnPropertyChanged(); }
    }

    /// <summary>Called by the plugin on DebugOutputReceivedEvent.</summary>
    public void Append(string category, string output)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var prefix = category switch
            {
                "stderr"  => "[ERR] ",
                "console" => "[DBG] ",
                _         => ""
            };
            _buffer.Append(prefix).Append(output);
            if (!output.EndsWith('\n')) _buffer.AppendLine();
            OutputText = _buffer.ToString();
        });
    }

    public void Clear()
    {
        _buffer.Clear();
        OutputText = string.Empty;
    }

}
