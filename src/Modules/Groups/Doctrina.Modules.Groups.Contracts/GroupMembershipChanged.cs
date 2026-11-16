using Doctrina.Core.Events;

namespace Doctrina.Modules.Groups.Contracts;

public enum MembershipChange { Added, Removed }

/// <summary>
/// A person was added to or removed from a group (spec groups, design D4 of add-groups). Moving a person gives a
/// <see cref="MembershipChange.Removed"/> for the old group followed by an <see cref="MembershipChange.Added"/> for the new one.
/// Published after the change is saved.
/// </summary>
public sealed record GroupMembershipChanged(Guid OfferingId, Guid GroupSetId, Guid GroupId, Guid PersonId, MembershipChange Change) : IDomainEvent;
