using Doctrina.Core.Entities;

namespace Doctrina.Modules.Content.Domain;

// Data model of the Content module (design D2 of add-content). OfferingId points into the Academic module and has no
// foreign key; Content checks it through IAcademicDirectory.

/// <summary>A section of the content of a course offering, e.g. "Week 1".</summary>
public sealed class ContentSection : ExchangeableEntity
{
    public Guid OfferingId { get; set; }
    public required string Title { get; set; }
    public int Position { get; set; }
    public bool Published { get; set; }
    public List<ContentItem> Items { get; } = [];
}

/// <summary>The kind of a content item; stored as text.</summary>
public enum ContentItemKind { Page, File, Link, ToolLink }

/// <summary>A page, file, link or tool link in a section. One table for all kinds; the kind decides which fields are used.</summary>
public sealed class ContentItem : ExchangeableEntity
{
    public Guid SectionId { get; set; }
    public ContentSection Section { get; set; } = null!;
    public ContentItemKind Kind { get; set; }
    public required string Title { get; set; }
    public int Position { get; set; }
    public bool Published { get; set; }

    /// <summary>Page: the text in Markdown.</summary>
    public string? Body { get; set; }

    /// <summary>Link: an absolute http(s) address.</summary>
    public string? Url { get; set; }

    /// <summary>Tool link: the registered LTI tool it starts. No foreign key; the Lti module owns the tool (design D4 of add-lti-platform).</summary>
    public Guid? ToolId { get; set; }

    /// <summary>File: the original file name, content type and size in bytes. The bytes are on disk (design D5).</summary>
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? Size { get; set; }

    /// <summary>Visible to participants: the item and its section are both published.</summary>
    public bool IsVisible => Published && Section.Published;
}
