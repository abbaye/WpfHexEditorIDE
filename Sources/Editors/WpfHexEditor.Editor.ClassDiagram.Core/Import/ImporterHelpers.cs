// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Import/ImporterHelpers.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Shared utilities used by Mermaid + PlantUML importers (and any
//     future DSL importer) to avoid duplicating visibility-token and
//     placeholder-node logic. Lifted in /simplify pass after the
//     Phase 4 commit.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Import;

internal static class ImporterHelpers
{
    /// <summary>
    /// Attempts to consume a leading +/-/#/~ visibility token from <paramref name="body"/>.
    /// Returns true and sets <paramref name="vis"/> when a token is consumed,
    /// advancing <paramref name="body"/> past it (and any leading whitespace).
    /// Returns false and leaves both arguments unchanged when no token is present.
    /// </summary>
    public static bool TryConsumeVisibilityPrefix(ref string body, out MemberVisibility vis)
    {
        if (body.Length > 0)
        {
            switch (body[0])
            {
                case '+': vis = MemberVisibility.Public;    body = body[1..].TrimStart(); return true;
                case '-': vis = MemberVisibility.Private;   body = body[1..].TrimStart(); return true;
                case '#': vis = MemberVisibility.Protected; body = body[1..].TrimStart(); return true;
                case '~': vis = MemberVisibility.Internal;  body = body[1..].TrimStart(); return true;
            }
        }
        vis = MemberVisibility.Public;
        return false;
    }

    /// <summary>
    /// Returns the <see cref="ClassNode"/> in <paramref name="byName"/> with the
    /// given name; creates an empty placeholder and registers it in both maps
    /// when missing. Used by importers to resolve relationship endpoints that
    /// reference types not yet declared.
    /// </summary>
    public static ClassNode GetOrAddPlaceholder(
        string name, Dictionary<string, ClassNode> byName, DiagramDocument doc)
    {
        if (byName.TryGetValue(name, out var existing)) return existing;
        var node = new ClassNode { Name = name, Id = Guid.NewGuid().ToString() };
        byName[name] = node;
        doc.Classes.Add(node);
        return node;
    }
}
