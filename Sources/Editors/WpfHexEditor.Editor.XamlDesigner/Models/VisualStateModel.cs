// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: VisualStateModel.cs
// Description:
//     Immutable domain records for VisualStateManager data.
//     VisualStateGroupModel → VisualStateModel → VisualStateSetterModel.
// Architecture Notes:
//     Pure records — no WPF dependency. Consumed by VisualStateManagerService
//     and VisualStatePanelViewModel.
// ==========================================================

using System.Collections.Immutable;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

public sealed record VisualStateGroupModel(
    string Name,
    ImmutableArray<VisualStateEntryModel> States);

public sealed record VisualStateEntryModel(
    string Name,
    ImmutableArray<VisualStateSetterModel> Setters);

public sealed record VisualStateSetterModel(
    string TargetName,
    string PropertyName,
    string Value);
