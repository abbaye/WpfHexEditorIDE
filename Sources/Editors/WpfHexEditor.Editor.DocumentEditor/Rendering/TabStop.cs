// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Rendering/TabStop.cs
// Description: Tab stop descriptor with position and alignment type.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Rendering;

internal enum TabAlign { Left, Right, Center, Decimal }

internal readonly record struct TabStop(double Pos, TabAlign Align);
