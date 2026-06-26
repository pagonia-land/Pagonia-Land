using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>One line in a detail section. When <see cref="TargetGuid"/> is set the line is a
/// link the user can click to navigate to that entity. Lines lay out as an aligned icon|text
/// column block (an invisible table), the block itself centred in the detail pane.</summary>
public sealed record DetailLine(string Text, string? TargetGuid, Bitmap? Icon = null)
{
    public bool IsLink => !string.IsNullOrEmpty(TargetGuid);
    public bool IsPlain => !IsLink;
    public bool HasIcon => Icon is not null;
}

/// <summary>A labelled block in the detail pane: a heading and its lines (plain or links).</summary>
public sealed record DetailSection(string Label, IReadOnlyList<DetailLine> Lines);

/// <summary>
/// Base for a catalog grid row. The grid shows a slim summary (icon + name + a couple of
/// key columns on the derived type); the detail pane reads <see cref="Name"/>,
/// <see cref="Icon"/> and <see cref="Detail"/> generically. Reference fields become clickable
/// <see cref="DetailLine"/>s (cross-navigation); <see cref="Guid"/> is copied on double-click.
/// </summary>
public abstract class CatalogRow
{
    public Bitmap? Icon { get; init; }

    public abstract string Name { get; }

    /// <summary>The entity GUID — copied to the clipboard on a row double-click.</summary>
    public abstract string Guid { get; }

    public abstract IReadOnlyList<DetailSection> Detail { get; }

    // Lower-cased blob of name + GUID + every detail line, built once, for free-text filtering.
    private string? _searchText;

    private string SearchText => _searchText ??= BuildSearchText();

    private string BuildSearchText()
    {
        var builder = new StringBuilder().Append(Name).Append(' ').Append(Guid);
        foreach (var section in Detail)
        {
            foreach (var line in section.Lines)
            {
                builder.Append(' ').Append(line.Text);
            }
        }

        return builder.ToString().ToLowerInvariant();
    }

    /// <summary>True if this row matches the already-lower-cased query (empty matches all).</summary>
    public bool Matches(string lowerQuery) => lowerQuery.Length == 0 || SearchText.Contains(lowerQuery);

    /// <summary>GUID → the target entity's icon, so a reference line can show what it points at.
    /// Set once per generation (rows and their icons are built together); only resources, buildings
    /// and units have icons, so references to recipes/objectives/categories resolve to none.</summary>
    public static IReadOnlyDictionary<string, Bitmap?>? IconsByGuid { get; set; }

    private static Bitmap? IconFor(string? guid) =>
        guid is not null && IconsByGuid is not null && IconsByGuid.TryGetValue(guid, out var icon) ? icon : null;

    /// <summary>A one-value plain section, omitted when the value is blank.</summary>
    protected static DetailSection? Field(string label, string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new DetailSection(label, new[] { new DetailLine(value, null) });

    /// <summary>A plain multi-line section, omitted when the list is empty.</summary>
    protected static DetailSection? List(string label, IReadOnlyList<string> values) =>
        values.Count == 0 ? null : new DetailSection(label, values.Select(v => new DetailLine(v, null)).ToList());

    /// <summary>A single navigable reference section, omitted when there is none.</summary>
    protected static DetailSection? Ref(string label, Reference? reference) =>
        reference is null ? null : new DetailSection(label, new[] { new DetailLine(reference.Display, reference.Guid, IconFor(reference.Guid)) });

    /// <summary>A multi-line navigable reference section, omitted when empty.</summary>
    protected static DetailSection? Refs(string label, IReadOnlyList<Reference> references) =>
        references.Count == 0 ? null : new DetailSection(label, references.Select(r => new DetailLine(r.Display, r.Guid, IconFor(r.Guid))).ToList());

    /// <summary>Collect the non-null sections in order.</summary>
    protected static IReadOnlyList<DetailSection> Sections(params DetailSection?[] sections) =>
        sections.Where(s => s is not null).Select(s => s!).ToList();
}
