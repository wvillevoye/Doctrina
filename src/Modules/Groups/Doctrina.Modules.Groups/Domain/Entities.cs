using Doctrina.Core.Entities;
using Doctrina.Modules.Groups.Contracts;

namespace Doctrina.Modules.Groups.Domain;

// Data model of the Groups module (design D3 of add-groups). OfferingId and PersonId point into the Academic module
// and have no foreign key; Groups checks them through IAcademicDirectory.

/// <summary>A set of groups in a course offering, e.g. "Project teams".</summary>
public sealed class GroupSet : ExchangeableEntity
{
    public Guid OfferingId { get; set; }
    public required string Name { get; set; }
    public bool VisibleToStudents { get; set; }
    public int? MaxGroupSize { get; set; }
    public List<Group> Groups { get; } = [];
}

/// <summary>A group in a set; the aggregate for its members, so membership events are raised here (design D4).</summary>
public sealed class Group : ExchangeableEntity
{
    public Guid GroupSetId { get; set; }
    public GroupSet GroupSet { get; set; } = null!;
    public required string Name { get; set; }
    public List<GroupMember> Members { get; } = [];

    public void AddMember(Guid personId, Guid offeringId)
    {
        Members.Add(new GroupMember { GroupSetId = GroupSetId, GroupId = Id, PersonId = personId });
        RaiseEvent(new GroupMembershipChanged(offeringId, GroupSetId, Id, personId, MembershipChange.Added));
    }

    public void RemoveMember(GroupMember member, Guid offeringId)
    {
        ArgumentNullException.ThrowIfNull(member);
        Members.Remove(member);
        RaiseEvent(new GroupMembershipChanged(offeringId, GroupSetId, Id, member.PersonId, MembershipChange.Removed));
    }

    /// <summary>Moves <paramref name="member"/> from <paramref name="from"/> into this group: "removed" then "added".</summary>
    public void MoveIn(Group from, GroupMember member, Guid offeringId)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(member);
        from.Members.Remove(member);
        RaiseEvent(new GroupMembershipChanged(offeringId, GroupSetId, from.Id, member.PersonId, MembershipChange.Removed));
        Members.Add(new GroupMember { GroupSetId = GroupSetId, GroupId = Id, PersonId = member.PersonId });
        RaiseEvent(new GroupMembershipChanged(offeringId, GroupSetId, Id, member.PersonId, MembershipChange.Added));
    }
}

/// <summary>Membership of a student; at most one per set (unique on GroupSetId + PersonId).</summary>
public sealed class GroupMember : Entity
{
    public Guid GroupSetId { get; set; }
    public Guid GroupId { get; set; }
    public Guid PersonId { get; set; }
}
