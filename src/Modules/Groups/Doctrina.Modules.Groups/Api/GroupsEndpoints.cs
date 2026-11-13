using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Application;
using Doctrina.Modules.Groups.Domain;
using Doctrina.Modules.Groups.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Groups.Api;

/// <summary>
/// Read-only API for groups (change add-rest-api, design D9): groups are LMS data, so systems don't write them.
/// Offering and persons are shown by their ExternalId; members who are no longer enrolled are left out, as in the UI.
/// </summary>
internal static class GroupsEndpoints
{
    public const string OfferingRequiredCode = "Groups.Api.OfferingRequired";

    public static void Map(IEndpointRouteBuilder api)
    {
        var groups = api.MapGroup("groups").WithTags("groups");
        groups.MapGet("", ListAsync)
            .RequirePermission(GroupsPermissions.Read)
            .WithSummary("Groups of a course offering")
            .WithDescription("Query: offeringId (required, the Doctrina id of the offering).");
        groups.MapGet("{id:guid}", GetAsync)
            .RequirePermission(GroupsPermissions.Read)
            .WithSummary("One group by Doctrina id");
    }

    private static async Task<IResult> ListAsync(Guid? offeringId, GroupService service, IAcademicDirectory directory, CancellationToken ct)
    {
        if (offeringId is not { } id)
        {
            return Result.Failure(Error.Validation(OfferingRequiredCode, "Give the offering with ?offeringId=…",
                new Dictionary<string, string[]> { ["offeringId"] = ["Required."] })).ToHttpResult();
        }

        var offering = await directory.FindOfferingAsync(id, ct);
        if (offering.IsFailure)
        {
            return offering.ToHttpResult();
        }

        var people = await PeopleAsync(directory, id, ct);
        var sets = await service.SetsAsync(id, ct);
        return Results.Ok(sets.SelectMany(s => s.Groups.Select(g => Representation(g, s, offering.Value!, people))));
    }

    private static async Task<IResult> GetAsync(Guid id, GroupsDbContext db, IAcademicDirectory directory, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().Include(g => g.GroupSet).Include(g => g.Members)
            .SingleOrDefaultAsync(g => g.Id == id && g.Status == EntityStatus.Active && g.GroupSet.Status == EntityStatus.Active, ct);
        var offering = group is null ? null : await directory.FindOfferingAsync(group.GroupSet.OfferingId, ct);
        if (group is null || offering!.IsFailure)
        {
            return Result.Failure(Error.NotFound(GroupsErrors.GroupNotFound, $"Group {id} doesn't exist.")).ToHttpResult();
        }

        return Results.Ok(Representation(group, group.GroupSet, offering.Value!, await PeopleAsync(directory, group.GroupSet.OfferingId, ct)));
    }

    private static async Task<Dictionary<Guid, string>> PeopleAsync(IAcademicDirectory directory, Guid offeringId, CancellationToken ct) =>
        (await directory.MembersAsync(offeringId, ct)).ToDictionary(m => m.PersonId, m => m.ExternalId);

    private static object Representation(Group group, GroupSet set, OfferingInfo offering, Dictionary<Guid, string> people) => new
    {
        id = group.Id,
        groupId = group.ExternalId,
        name = group.Name,
        groupSetId = set.ExternalId,
        groupSetName = set.Name,
        offeringId = offering.ExternalId,
        members = group.Members.Where(m => people.ContainsKey(m.PersonId)).Select(m => people[m.PersonId]).Order(StringComparer.Ordinal),
        source = group.Source,
        dateLastModified = group.DateLastModified.ToUniversalTime(),
    };
}
