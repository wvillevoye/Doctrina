using System.Security.Claims;
using Doctrina.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Doctrina.Modules.Academic.Contracts;

/// <summary>An active course offering (design D1 of add-groups).</summary>
public sealed record OfferingInfo(Guid Id, string ExternalId, string Name, string CourseName, string SessionName);

/// <summary>The role of a person in a course offering (OOAPI association role).</summary>
public enum OfferingRole { Student, Lecturer, TeachingAssistant, Coordinator }

/// <summary>An active enrolment of an active person in a course offering.</summary>
public sealed record OfferingMember(Guid PersonId, string ExternalId, string Name, OfferingRole Role);

/// <summary>Read access to academic data for other modules. Never exposes Academic's entities or tables.</summary>
public interface IAcademicDirectory
{
    /// <summary>The offering, or <c>Academic.CourseOffering.NotFound</c> if it doesn't exist or is to be deleted.</summary>
    Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken);

    /// <summary>The person of the signed-in user (linked via personId, then issuer + oid), or <see langword="null"/>.</summary>
    Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>Active enrolments of active persons in the offering, ordered by name.</summary>
    Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken);
}

/// <summary>
/// A component shown on the page of a course offering (<c>/courses/{offering}</c>). The component gets the parameter
/// <c>[Parameter] public Guid OfferingId { get; set; }</c>.
/// </summary>
public sealed record OfferingPageSection(Type Component, int Order);

public static class OfferingPageSectionExtensions
{
    /// <summary>Registers <typeparamref name="TComponent"/> as a section of the offering page; call from <c>ConfigureServices</c>.</summary>
    public static IServiceCollection AddOfferingPageSection<TComponent>(this IServiceCollection services, int order) =>
        services.AddSingleton(new OfferingPageSection(typeof(TComponent), order));
}
