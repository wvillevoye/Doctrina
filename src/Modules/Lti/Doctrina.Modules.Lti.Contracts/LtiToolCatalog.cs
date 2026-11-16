namespace Doctrina.Modules.Lti.Contracts;

/// <summary>A registered LTI tool, as other modules see it.</summary>
public sealed record LtiToolSummary(Guid Id, string Name, bool SupportsDeepLinking = false, bool SupportsGradeServices = false);

/// <summary>Public contract of the Lti module (design D1 of add-lti-platform).</summary>
public interface ILtiToolCatalog
{
    /// <summary>The active tools, ordered by name.</summary>
    Task<IReadOnlyList<LtiToolSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Whether an active tool with this id exists.</summary>
    Task<bool> ExistsAsync(Guid toolId, CancellationToken cancellationToken);
}
