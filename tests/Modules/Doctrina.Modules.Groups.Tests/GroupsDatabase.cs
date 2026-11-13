using System.Security.Claims;
using Doctrina.Core.Events;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Events;
using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Contracts;
using Doctrina.Modules.Groups.Contracts;
using Doctrina.Modules.Groups.Infrastructure;
using Doctrina.TestKit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Doctrina.Modules.Groups.Tests;

/// <summary>
/// An empty LocalDB database with the Groups migrations applied, per test, with an event bus that records
/// <see cref="GroupMembershipChanged"/>. The base connection string comes from <c>src/Doctrina.Host/appsettings.Test.json</c>.
/// </summary>
internal sealed class GroupsDatabase : IAsyncDisposable
{
    private static readonly Lazy<string> BaseConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepoRoot(), "src", "Doctrina.Host", "appsettings.Test.json"), optional: false)
            .Build()
            .GetConnectionString(ModuleDatabase.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Doctrina is missing in appsettings.Test.json."));

    private readonly LocalDbTestDatabase _database;
    private readonly InProcessEventBus _events;

    private GroupsDatabase(LocalDbTestDatabase database)
    {
        _database = database;
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IEventHandler<GroupMembershipChanged>>(Events)
            .BuildServiceProvider();
        _events = new InProcessEventBus(services, NullLogger<InProcessEventBus>.Instance);
    }

    public EventRecorder Events { get; } = new();

    public static async Task<GroupsDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var database = new GroupsDatabase(await LocalDbTestDatabase.CreateAsync(BaseConnectionString.Value, cancellationToken));
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    public LocalDbTestDatabase Database => _database;

    /// <summary>A fresh context (own change tracker), like a new request.</summary>
    public GroupsDbContext CreateContext() =>
        new(ModuleDatabase.CreateOptions<GroupsDbContext>(_database.ConnectionString, GroupsModule.ModuleName, events: _events));

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Doctrina.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Doctrina.slnx not found above " + AppContext.BaseDirectory);
    }
}

/// <summary>Records published membership events.</summary>
internal sealed class EventRecorder : IEventHandler<GroupMembershipChanged>
{
    public List<GroupMembershipChanged> Published { get; } = [];

    public Task HandleAsync(GroupMembershipChanged domainEvent, CancellationToken cancellationToken)
    {
        Published.Add(domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="IAcademicDirectory"/> (task 2.2): one offering with members, so the Groups tests don't need
/// the Academic database. The "current person" is read from the claim <c>personGuid</c>.
/// </summary>
internal sealed class FakeDirectory : IAcademicDirectory
{
    public const string PersonClaim = "personGuid";

    public Guid OfferingId { get; } = new("11111111-0000-0000-0000-000000000001");

    public List<OfferingMember> Members { get; } = [];

    public OfferingMember Add(string externalId, OfferingRole role = OfferingRole.Student)
    {
        var member = new OfferingMember(new Guid($"22222222-0000-0000-0000-{Members.Count + 1:D12}"), externalId, externalId + " Name", role);
        Members.Add(member);
        return member;
    }

    public List<OfferingMember> AddStudents(int count) => [.. Enumerable.Range(1, count).Select(i => Add($"I6{i:D6}"))];

    public static ClaimsPrincipal User(Guid? personId, params string[] permissions) =>
        new(new ClaimsIdentity(
            [.. personId is { } id ? [new Claim(PersonClaim, id.ToString())] : Array.Empty<Claim>(),
             .. permissions.Select(p => new Claim(Doctrina.Infrastructure.Authentication.DoctrinaClaimTypes.Permission, p))],
            "test"));

    public Task<Result<OfferingInfo>> FindOfferingAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult(offeringId == OfferingId
            ? Result<OfferingInfo>.Success(new OfferingInfo(OfferingId, "BIO101-2026-P1", "Cell biology (2026-P1)", "Cell biology", "Period 1"))
            : Result<OfferingInfo>.Failure(Error.NotFound("Academic.CourseOffering.NotFound", "Not found.")));

    public Task<Guid?> CurrentPersonIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(Guid.TryParse(user.FindFirst(PersonClaim)?.Value, out var id) ? id : (Guid?)null);

    public Task<IReadOnlyList<OfferingMember>> MembersAsync(Guid offeringId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OfferingMember>>(offeringId == OfferingId ? [.. Members] : []);
}
