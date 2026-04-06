// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassNodeViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Observable wrapper around ClassNode for WPF data binding.
//     Mirrors all ClassNode structural and layout properties as
//     bindable properties. Adds IsSelected and IsHovered for
//     canvas interaction state.
//
// Architecture Notes:
//     Pattern: ViewModel wrapper (Adapter).
//     The wrapped ClassNode is mutated in-place on property set
//     so the domain model stays as the source of truth.
//     Layout properties (X, Y, Width, Height) are kept in sync
//     bidirectionally with the underlying node.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// Observable wrapper around a <see cref="ClassNode"/> for WPF data binding.
/// </summary>
public sealed class ClassNodeViewModel : ViewModelBase
{
    private readonly ClassNode _node;
    private bool _isSelected;
    private bool _isHovered;

    public ClassNodeViewModel(ClassNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    /// <summary>Underlying domain model node.</summary>
    public ClassNode Node => _node;

    // ---------------------------------------------------------------------------
    // Identity / structural
    // ---------------------------------------------------------------------------

    public string Id
    {
        get => _node.Id;
        set { if (_node.Id == value) return; _node.Id = value; OnPropertyChanged(); }
    }

    public string Name => _node.Name;

    public ClassKind Kind
    {
        get => _node.Kind;
        set { if (_node.Kind == value) return; _node.Kind = value; OnPropertyChanged(); OnPropertyChanged(nameof(KindLabel)); }
    }

    public bool IsAbstract
    {
        get => _node.IsAbstract;
        set { if (_node.IsAbstract == value) return; _node.IsAbstract = value; OnPropertyChanged(); }
    }

    /// <summary>Display label for the kind (e.g. "Â«interfaceÂ»").</summary>
    public string KindLabel => _node.Kind switch
    {
        ClassKind.Interface => "Â«interfaceÂ»",
        ClassKind.Enum      => "Â«enumÂ»",
        ClassKind.Struct    => "Â«structÂ»",
        ClassKind.Abstract  => "Â«abstractÂ»",
        _                   => string.Empty
    };

    public IEnumerable<ClassMember> Members => _node.Members;

    // ---------------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------------

    public double X
    {
        get => _node.X;
        set { if (_node.X == value) return; _node.X = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _node.Y;
        set { if (_node.Y == value) return; _node.Y = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _node.Width;
        set { if (_node.Width == value) return; _node.Width = value; OnPropertyChanged(); }
    }

    public double Height
    {
        get => _node.Height;
        set { if (_node.Height == value) return; _node.Height = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // Interaction state
    // ---------------------------------------------------------------------------

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsHovered
    {
        get => _isHovered;
        set { if (_isHovered == value) return; _isHovered = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}
