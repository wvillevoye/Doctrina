using System.Globalization;
using Doctrina.Modules.Content.Domain;

namespace Doctrina.Modules.Content.Components;

/// <summary>English labels for content (design D8 of add-content).</summary>
internal static class ContentLabels
{
    public static string Kind(ContentItemKind kind) => kind switch
    {
        ContentItemKind.Page => "Page",
        ContentItemKind.File => "File",
        ContentItemKind.ToolLink => "Tool",
        _ => "Link",
    };

    /// <summary>A size in bytes as "12 KB" or "1.5 MB".</summary>
    public static string Size(long bytes) => bytes < 1024 * 1024
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, bytes / 1024)} KB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB");

    public static string LaunchUrl(Guid itemId) => $"/lti/launch/{itemId:D}";

    public static string ItemUrl(Guid offeringId, Guid itemId) => $"/courses/{offeringId:D}/content/{itemId:D}";
}
