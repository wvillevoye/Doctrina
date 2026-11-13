using Doctrina.Core.Entities;
using Doctrina.Modules.Academic.Application;
using Doctrina.Modules.Academic.Domain;
using Doctrina.UI.Components;

namespace Doctrina.Modules.Academic.Components;

/// <summary>English display texts for types, statuses and roles.</summary>
internal static class Labels
{
    /// <summary><c>course-offerings</c> → "Course offerings".</summary>
    public static string Type(string typeName) => char.ToUpperInvariant(typeName[0]) + typeName[1..].Replace('-', ' ');

    public static string Type(AcademicType type) => Type(type.Name);

    public static string Status(EntityStatus status) => status == EntityStatus.Active ? "Active" : "To be deleted";

    public static DcBadgeVariant StatusVariant(EntityStatus status) =>
        status == EntityStatus.Active ? DcBadgeVariant.Success : DcBadgeVariant.Warning;

    public static string Role(AssociationRole role) => role switch
    {
        AssociationRole.Student => "Student",
        AssociationRole.Lecturer => "Lecturer",
        AssociationRole.TeachingAssistant => "Teaching assistant",
        _ => "Coordinator",
    };

    public static string Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "–";

    public static string Href(string typeName, Guid id) => $"/academic/{typeName}/{id:D}";
}
