// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: VisualStateManagerService.cs
// Description:
//     Parses VisualStateManager.VisualStateGroups from XAML and
//     patches/applies visual state transitions for the design canvas.
// Architecture Notes:
//     Stateless service — pure XLinq. No WPF rendering dependency.
// ==========================================================

using System.Collections.Immutable;
using System.Xml.Linq;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Parses and manipulates VisualStateManager data from XAML text.
/// </summary>
public sealed class VisualStateManagerService
{
    private static readonly XNamespace WpfNs =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    // ── Parse ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses all VisualStateGroups from <paramref name="xaml"/>.
    /// Returns an empty list on parse error or when no VSM is found.
    /// </summary>
    public IReadOnlyList<VisualStateGroupModel> Parse(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return [];

        try
        {
            var doc    = XDocument.Parse(xaml);
            var groups = doc.Descendants()
                .Where(e => e.Name.LocalName == "VisualStateGroup")
                .Select(ParseGroup)
                .ToList();
            return groups;
        }
        catch { return []; }
    }

    // ── Patch ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the setters of an existing VisualState in <paramref name="xaml"/>.
    /// Matches by group name and state name. Returns <paramref name="xaml"/> unchanged on error.
    /// </summary>
    public string PatchState(string xaml, string groupName, VisualStateEntryModel model)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return xaml;

        try
        {
            var doc   = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var group = FindGroup(doc, groupName);
            if (group is null) return xaml;

            var stateEl = group.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "VisualState"
                    && (string?)e.Attribute(XamlNs + "Name") == model.Name
                    || (string?)e.Attribute("Name") == model.Name);

            if (stateEl is null) return xaml;

            // Replace Storyboard content with new setters.
            var sb = stateEl.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Storyboard");
            sb?.Remove();

            if (model.Setters.Length > 0)
            {
                var newSb = BuildStoryboard(model.Setters);
                stateEl.Add(newSb);
            }

            return Serialize(doc);
        }
        catch { return xaml; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly XNamespace XamlNs =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static VisualStateGroupModel ParseGroup(XElement groupEl)
    {
        var name = (string?)groupEl.Attribute(XamlNs + "Name")
                ?? (string?)groupEl.Attribute("Name")
                ?? string.Empty;

        var states = groupEl.Elements()
            .Where(e => e.Name.LocalName == "VisualState")
            .Select(ParseState)
            .ToImmutableArray();

        return new VisualStateGroupModel(name, states);
    }

    private static VisualStateEntryModel ParseState(XElement stateEl)
    {
        var name = (string?)stateEl.Attribute(XamlNs + "Name")
                ?? (string?)stateEl.Attribute("Name")
                ?? string.Empty;

        var setters = stateEl.Descendants()
            .Where(e => e.Name.LocalName is "ObjectAnimationUsingKeyFrames"
                     or "DoubleAnimationUsingKeyFrames"
                     or "ColorAnimationUsingKeyFrames"
                     or "DiscreteObjectKeyFrame")
            .Select(ParseSetter)
            .Where(s => s is not null)
            .Cast<VisualStateSetterModel>()
            .ToImmutableArray();

        return new VisualStateEntryModel(name, setters);
    }

    private static VisualStateSetterModel? ParseSetter(XElement animEl)
    {
        var targetName = (string?)animEl.Attribute(
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation" + ".TargetName")
            ?? (string?)animEl.Attribute("Storyboard.TargetName")
            ?? string.Empty;

        var targetProp = (string?)animEl.Attribute("Storyboard.TargetProperty") ?? string.Empty;

        var valueEl = animEl.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "DiscreteObjectKeyFrame"
                             || e.Name.LocalName == "DiscreteDoubleKeyFrame"
                             || e.Name.LocalName == "DiscreteColorKeyFrame");

        var value = (string?)valueEl?.Attribute("Value") ?? string.Empty;

        return new VisualStateSetterModel(targetName, targetProp, value);
    }

    private static XElement? FindGroup(XDocument doc, string groupName)
        => doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "VisualStateGroup"
                && ((string?)e.Attribute(XamlNs + "Name") == groupName
                 || (string?)e.Attribute("Name") == groupName));

    private static XElement BuildStoryboard(ImmutableArray<VisualStateSetterModel> setters)
    {
        var sb = new XElement(WpfNs + "Storyboard");
        foreach (var setter in setters)
        {
            var anim = new XElement(WpfNs + "ObjectAnimationUsingKeyFrames");
            anim.SetAttributeValue("Storyboard.TargetName",     setter.TargetName);
            anim.SetAttributeValue("Storyboard.TargetProperty", setter.PropertyName);
            var kf = new XElement(WpfNs + "DiscreteObjectKeyFrame",
                new XAttribute("KeyTime", "0:0:0"),
                new XAttribute("Value",   setter.Value));
            anim.Add(kf);
            sb.Add(anim);
        }
        return sb;
    }

    private static string Serialize(XDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        using var writer = System.Xml.XmlWriter.Create(sb,
            new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent             = false,
                NewLineHandling    = System.Xml.NewLineHandling.None
            });
        doc.WriteTo(writer);
        writer.Flush();
        return sb.ToString();
    }
}
