using System.Globalization;
using System.Text.RegularExpressions;
using Doctrina.Core.Entities;
using Doctrina.Core.Results;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Domain;
using Doctrina.Modules.Groups.Infrastructure;
using Group = Doctrina.Modules.Groups.Domain.Group;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Groups.Application;

/// <summary>Error codes of the Groups module (titles in Resources/GroupsErrors.resx).</summary>
internal static class GroupsErrors
{
    public const string SetInvalid = "Groups.GroupSet.Invalid";
    public const string SetNotFound = "Groups.GroupSet.NotFound";
    public const string GroupInvalid = "Groups.Group.Invalid";
    public const string GroupNotFound = "Groups.Group.NotFound";
    public const string GroupFull = "Groups.Group.Full";
    public const string NotAStudent = "Groups.Member.NotAStudent";
    public const string DistributionInvalid = "Groups.Distribution.Invalid";

    public static readonly string[] All = [SetInvalid, SetNotFound, GroupInvalid, GroupNotFound, GroupFull, NotAStudent, DistributionInvalid];

    public static Error Invalid(string code, string field, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>
/// All changes to group sets, groups and members (design D4, D6 of add-groups). Every method saves once and returns a
/// <see cref="Result"/>; membership events are published after saving.
/// </summary>
internal sealed partial class GroupService(GroupsDbContext db, IAcademicDirectory directory)
{
    public const string DoctrinaSource = "Doctrina";

    /// <summary>Active sets of the offering with their active groups and members, ordered by name.</summary>
    public async Task<IReadOnlyList<GroupSet>> SetsAsync(Guid offeringId, CancellationToken ct)
    {
        var sets = await db.GroupSets.AsNoTracking()
            .Where(s => s.OfferingId == offeringId && s.Status == EntityStatus.Active)
            .Include(s => s.Groups.Where(g => g.Status == EntityStatus.Active)).ThenInclude(g => g.Members)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        foreach (var set in sets)
        {
            set.Groups.Sort((a, b) => NaturalCompare(a.Name, b.Name));
        }

        return sets;
    }

    public async Task<Result<GroupSet>> CreateSetAsync(Guid offeringId, string? name, bool visibleToStudents, int? maxGroupSize, CancellationToken ct)
    {
        if ((await directory.FindOfferingAsync(offeringId, ct)).Error is { } notFound)
        {
            return notFound;
        }

        if (CheckSet(name, maxGroupSize) is { } invalid)
        {
            return invalid;
        }

        var set = New(new GroupSet { ExternalId = string.Empty, Source = DoctrinaSource, Name = name!.Trim() });
        set.OfferingId = offeringId;
        set.VisibleToStudents = visibleToStudents;
        set.MaxGroupSize = maxGroupSize;
        await db.SaveChangesAsync(ct);
        return set;
    }

    public async Task<Result> UpdateSetAsync(Guid setId, string? name, bool visibleToStudents, int? maxGroupSize, CancellationToken ct)
    {
        var set = await ActiveSetAsync(setId, ct);
        if (set is null)
        {
            return SetNotFound(setId);
        }

        if (CheckSet(name, maxGroupSize) is { } invalid)
        {
            return invalid;
        }

        if (maxGroupSize is { } max && set.Groups.Any(g => g.Members.Count > max))
        {
            return GroupsErrors.Invalid(GroupsErrors.SetInvalid, "maxGroupSize", $"A group in this set already has more than {max} members.");
        }

        set.Name = name!.Trim();
        set.VisibleToStudents = visibleToStudents;
        set.MaxGroupSize = maxGroupSize;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Marks the set and its groups as to be deleted and removes all members (design D3).</summary>
    public async Task<Result> DeleteSetAsync(Guid setId, CancellationToken ct)
    {
        var set = await ActiveSetAsync(setId, ct);
        if (set is null)
        {
            return SetNotFound(setId);
        }

        set.Status = EntityStatus.ToBeDeleted;
        foreach (var group in set.Groups)
        {
            Empty(group, set.OfferingId);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Group>> CreateGroupAsync(Guid setId, string? name, CancellationToken ct)
    {
        var set = await ActiveSetAsync(setId, ct);
        if (set is null)
        {
            return SetNotFound(setId);
        }

        if (CheckName(GroupsErrors.GroupInvalid, name) is { } invalid)
        {
            return invalid;
        }

        var group = NewGroup(set, name!.Trim());
        await db.SaveChangesAsync(ct);
        return group;
    }

    public async Task<Result> RenameGroupAsync(Guid groupId, string? name, CancellationToken ct)
    {
        var group = await ActiveGroupAsync(groupId, ct);
        if (group is null)
        {
            return GroupNotFound(groupId);
        }

        if (CheckName(GroupsErrors.GroupInvalid, name) is { } invalid)
        {
            return invalid;
        }

        group.Name = name!.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Marks the group as to be deleted and removes its members (design D3).</summary>
    public async Task<Result> DeleteGroupAsync(Guid groupId, CancellationToken ct)
    {
        var group = await ActiveGroupAsync(groupId, ct);
        if (group is null)
        {
            return GroupNotFound(groupId);
        }

        Empty(group, group.GroupSet.OfferingId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Puts a student in the group; moves the student if already in another group of the same set.</summary>
    public async Task<Result> AssignAsync(Guid groupId, Guid personId, CancellationToken ct)
    {
        var group = await ActiveGroupAsync(groupId, ct);
        if (group is null)
        {
            return GroupNotFound(groupId);
        }

        var offeringId = group.GroupSet.OfferingId;
        var members = await directory.MembersAsync(offeringId, ct);
        if (!members.Any(m => m.PersonId == personId && m.Role == OfferingRole.Student))
        {
            return GroupsErrors.Invalid(GroupsErrors.NotAStudent, "personId", "Only students enrolled in this offering can be in a group.");
        }

        var current = await db.GroupMembers.SingleOrDefaultAsync(m => m.GroupSetId == group.GroupSetId && m.PersonId == personId, ct);
        if (current?.GroupId == group.Id)
        {
            return Result.Success();
        }

        if (group.GroupSet.MaxGroupSize is { } max && group.Members.Count >= max)
        {
            return GroupsErrors.Invalid(GroupsErrors.GroupFull, "groupId", $"{group.Name} is full ({max} members).");
        }

        if (current is null)
        {
            group.AddMember(personId, offeringId);
        }
        else
        {
            var from = await db.Groups.Include(g => g.Members).SingleAsync(g => g.Id == current.GroupId, ct);
            group.MoveIn(from, from.Members.Single(m => m.PersonId == personId), offeringId);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Takes the student out of whatever group of the set they are in.</summary>
    public async Task<Result> UnassignAsync(Guid setId, Guid personId, CancellationToken ct)
    {
        var set = await ActiveSetAsync(setId, ct);
        if (set is null)
        {
            return SetNotFound(setId);
        }

        var group = set.Groups.FirstOrDefault(g => g.Members.Any(m => m.PersonId == personId));
        if (group is not null)
        {
            group.RemoveMember(group.Members.Single(m => m.PersonId == personId), set.OfferingId);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Distributes the students of the offering who aren't in a group of the set yet, at random, over
    /// <paramref name="groupCount"/> new groups or new groups of <paramref name="groupSize"/> (design D6).
    /// Returns the number of new groups.
    /// </summary>
    public async Task<Result<int>> DistributeAsync(Guid setId, int? groupCount, int? groupSize, CancellationToken ct)
    {
        if ((groupCount is null) == (groupSize is null) || groupCount < 1 || groupSize < 1)
        {
            return GroupsErrors.Invalid(GroupsErrors.DistributionInvalid, groupCount is null ? "groupSize" : "groupCount",
                "Enter either a number of groups or a group size, of at least 1.");
        }

        var set = await ActiveSetAsync(setId, ct);
        if (set is null)
        {
            return SetNotFound(setId);
        }

        var assigned = set.Groups.SelectMany(g => g.Members).Select(m => m.PersonId).ToHashSet();
        var candidates = (await directory.MembersAsync(set.OfferingId, ct))
            .Where(m => m.Role == OfferingRole.Student && !assigned.Contains(m.PersonId))
            .Select(m => m.PersonId)
            .ToArray();
        if (candidates.Length == 0)
        {
            return GroupsErrors.Invalid(GroupsErrors.DistributionInvalid, "groupCount", "Every student is already in a group of this set.");
        }

        var count = groupCount ?? (candidates.Length + groupSize!.Value - 1) / groupSize.Value;
        var largest = (candidates.Length + count - 1) / count;
        if (set.MaxGroupSize is { } max && largest > max)
        {
            return GroupsErrors.Invalid(GroupsErrors.GroupFull, "groupCount", $"Groups would get {largest} members; the maximum for this set is {max}.");
        }

        var next = set.Groups.Select(g => GroupNumber().Match(g.Name)).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max() + 1;
        var groups = Enumerable.Range(next, count).Select(n => NewGroup(set, $"Group {n}")).ToList();
        Random.Shared.Shuffle(candidates);
        for (var i = 0; i < candidates.Length; i++)
        {
            groups[i % count].AddMember(candidates[i], set.OfferingId);
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    private TEntity New<TEntity>(TEntity entity)
        where TEntity : ExchangeableEntity
    {
        // EF Core generates the sequential Id on Add; the Doctrina-owned ExternalId is that Id (design D3).
        db.Add(entity);
        entity.ExternalId = entity.Id.ToString("D");
        return entity;
    }

    private Group NewGroup(GroupSet set, string name)
    {
        var group = New(new Group { ExternalId = string.Empty, Source = DoctrinaSource, Name = name, GroupSet = set });
        group.GroupSetId = set.Id;
        set.Groups.Add(group);
        return group;
    }

    private static void Empty(Group group, Guid offeringId)
    {
        group.Status = EntityStatus.ToBeDeleted;
        foreach (var member in group.Members.ToList())
        {
            group.RemoveMember(member, offeringId);
        }
    }

    private Task<GroupSet?> ActiveSetAsync(Guid setId, CancellationToken ct) =>
        db.GroupSets
            .Include(s => s.Groups.Where(g => g.Status == EntityStatus.Active)).ThenInclude(g => g.Members)
            .SingleOrDefaultAsync(s => s.Id == setId && s.Status == EntityStatus.Active, ct);

    private Task<Group?> ActiveGroupAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.Include(g => g.GroupSet).Include(g => g.Members)
            .SingleOrDefaultAsync(g => g.Id == groupId && g.Status == EntityStatus.Active && g.GroupSet.Status == EntityStatus.Active, ct);

    private static Error? CheckSet(string? name, int? maxGroupSize) =>
        CheckName(GroupsErrors.SetInvalid, name)
        ?? (maxGroupSize < 1 ? GroupsErrors.Invalid(GroupsErrors.SetInvalid, "maxGroupSize", "Must be at least 1.") : null);

    private static Error? CheckName(string code, string? name) =>
        string.IsNullOrWhiteSpace(name) ? GroupsErrors.Invalid(code, "name", "Enter a name.")
        : name.Trim().Length > GroupsDbContext.NameMaxLength ? GroupsErrors.Invalid(code, "name", $"Must be at most {GroupsDbContext.NameMaxLength} characters.")
        : null;

    private static Error SetNotFound(Guid id) => Error.NotFound(GroupsErrors.SetNotFound, $"Group set {id} doesn't exist.");

    private static Error GroupNotFound(Guid id) => Error.NotFound(GroupsErrors.GroupNotFound, $"Group {id} doesn't exist.");

    /// <summary>"Group 2" before "Group 10".</summary>
    internal static int NaturalCompare(string a, string b)
    {
        var (ma, mb) = (GroupNumber().Match(a), GroupNumber().Match(b));
        return ma.Success && mb.Success
            ? int.Parse(ma.Groups[1].Value, CultureInfo.InvariantCulture).CompareTo(int.Parse(mb.Groups[1].Value, CultureInfo.InvariantCulture))
            : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^Group (\d+)$")]
    private static partial Regex GroupNumber();
}

/// <summary>Who may manage the groups of an offering (design D5).</summary>
internal sealed class GroupsAccess(IAcademicDirectory directory)
{
    public async Task<bool> CanManageAsync(System.Security.Claims.ClaimsPrincipal user, Guid offeringId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.HasClaim(Doctrina.Infrastructure.Authentication.DoctrinaClaimTypes.Permission, GroupsPermissions.Manage))
        {
            return true;
        }

        var personId = await directory.CurrentPersonIdAsync(user, ct);
        return personId is not null && (await directory.MembersAsync(offeringId, ct))
            .Any(m => m.PersonId == personId && m.Role is OfferingRole.Lecturer or OfferingRole.Coordinator);
    }
}
